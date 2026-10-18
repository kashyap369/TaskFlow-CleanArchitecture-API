using FluentValidation;
using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Attendance;

/// <summary>
/// A batch of fixes from a tracked member's phone. Without a session id the points belong to the
/// open session; with one, a phone that was offline at punch-out can still deliver that session's
/// points for <see cref="FieldTrackingPolicy.LateUploadWindow"/>.
/// </summary>
public sealed record RecordLocationPointsCommand(int OrganizationId, int? AttendanceSessionId,
    IReadOnlyList<LocationPointInput> Points) : IRequest<RecordLocationPointsResultDto>;

public sealed record GiveFieldTrackingConsentCommand(int OrganizationId, int PolicyVersion) : IRequest;

public sealed record WithdrawFieldTrackingConsentCommand(int OrganizationId) : IRequest;

public sealed record UpdateFieldTrackingSettingsCommand(int OrganizationId, bool TrackingEnabled, int LocationRetentionDays,
    IReadOnlyList<int> TrackedRoleIds) : IRequest;

public sealed class RecordLocationPointsCommandValidator : AbstractValidator<RecordLocationPointsCommand>
{
    public RecordLocationPointsCommandValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x.AttendanceSessionId).GreaterThan(0).When(x => x.AttendanceSessionId is not null);
        RuleFor(x => x.Points).NotEmpty()
            .Must(x => x is null || x.Count <= FieldTrackingPolicy.MaxPointsPerUpload)
            .WithMessage($"Send at most {FieldTrackingPolicy.MaxPointsPerUpload} points at a time.");
        RuleForEach(x => x.Points).ChildRules(point =>
        {
            point.RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d);
            point.RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d);
            point.RuleFor(x => x.BatteryPercent).InclusiveBetween(0, 100).When(x => x.BatteryPercent is not null);
            point.RuleFor(x => x.AccuracyMeters).GreaterThanOrEqualTo(0d).When(x => x.AccuracyMeters is not null);
        });
    }
}

public sealed class GiveFieldTrackingConsentCommandValidator : AbstractValidator<GiveFieldTrackingConsentCommand>
{
    public GiveFieldTrackingConsentCommandValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x.PolicyVersion).GreaterThan(0);
    }
}

public sealed class UpdateFieldTrackingSettingsCommandValidator : AbstractValidator<UpdateFieldTrackingSettingsCommand>
{
    public UpdateFieldTrackingSettingsCommandValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x.LocationRetentionDays).InclusiveBetween(7, 365);
        RuleFor(x => x.TrackedRoleIds).NotNull();
        RuleForEach(x => x.TrackedRoleIds).GreaterThan(0);
    }
}

/// <summary>Whether a member's next punch-in starts a tracked session. The one place that rule lives.</summary>
internal static class FieldTrackingRules
{
    public static async Task<bool> MustTrackAsync(IAttendanceSettingsRepository settings,
        IOrganizationMemberRepository members, IAttendanceTrackedRoleRepository trackedRoles,
        int organizationId, int userId, CancellationToken cancellationToken)
    {
        var current = await settings.GetByOrganizationAsync(organizationId, cancellationToken);
        if (current is not { TrackingEnabled: true }) return false;
        // The owner without a member row has no role, so is never tracked.
        var member = await members.GetMemberAsync(organizationId, userId, cancellationToken);
        if (member is null || !member.IsActive) return false;
        return await trackedRoles.IsTrackedAsync(organizationId, member.OrganizationRoleId, cancellationToken);
    }

    public static DateTime ToStoredUtc(DateTimeOffset value)
    {
        // PostgreSQL keeps microseconds; trimming here makes a resent point compare equal to the stored one.
        var ticks = value.UtcDateTime.Ticks;
        return new DateTime(ticks - ticks % 10, DateTimeKind.Utc);
    }
}

public sealed class RecordLocationPointsCommandHandler : IRequestHandler<RecordLocationPointsCommand, RecordLocationPointsResultDto>
{
    private readonly IAttendanceSessionRepository _sessions; private readonly IFieldTrackingConsentRepository _consents;
    private readonly ILocationPointRepository _points; private readonly IOrganizationAccessGuard _access;
    private readonly ICurrentUserService _user; private readonly IUnitOfWork _unitOfWork;
    public RecordLocationPointsCommandHandler(IAttendanceSessionRepository sessions, IFieldTrackingConsentRepository consents,
        ILocationPointRepository points, IOrganizationAccessGuard access, ICurrentUserService user, IUnitOfWork unitOfWork) =>
        (_sessions, _consents, _points, _access, _user, _unitOfWork) = (sessions, consents, points, access, user, unitOfWork);
    public async Task<RecordLocationPointsResultDto> Handle(RecordLocationPointsCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureOrganizationAsync(request.OrganizationId, cancellationToken);
        var now = DateTime.UtcNow;

        AttendanceSession session;
        if (request.AttendanceSessionId is int sessionId)
        {
            session = await _sessions.GetByIdAsync(sessionId, cancellationToken) ??
                throw new NotFoundException("ATTENDANCE_SESSION_NOT_FOUND", "Attendance record not found.");
            // Someone else's session is reported as missing, not as forbidden.
            if (session.UserId != _user.UserId || session.OrganizationId != request.OrganizationId)
                throw new NotFoundException("ATTENDANCE_SESSION_NOT_FOUND", "Attendance record not found.");
            if (!session.IsOpen && session.PunchOutAtUtc < now - FieldTrackingPolicy.LateUploadWindow)
                throw new ConflictException("TRACKING_SESSION_CLOSED", "This attendance session closed too long ago to add locations to it.");
        }
        else
        {
            session = await _sessions.GetOpenAsync(request.OrganizationId, _user.UserId, cancellationToken) ??
                throw new ConflictException("ATTENDANCE_NOT_PUNCHED_IN", "You are not punched in, so your location is not being recorded.");
        }

        // The app stops tracking on either of these.
        if (!session.IsTracked)
            throw new ConflictException("TRACKING_NOT_REQUIRED", "Location is not recorded for this attendance session.");
        if (await _consents.GetAsync(request.OrganizationId, _user.UserId, FieldTrackingPolicy.CurrentVersion, cancellationToken) is null)
            throw new ConflictException("TRACKING_CONSENT_REQUIRED", "Location sharing has not been agreed to.");

        // Only fixes inside the session are kept; a minute of slack covers clock drift between phone and server.
        var from = session.PunchInAtUtc.AddMinutes(-1);
        var to = (session.PunchOutAtUtc ?? now).AddMinutes(1);
        if (to > now.AddMinutes(2)) to = now.AddMinutes(2);

        var candidates = request.Points
            .Select(x => (Input: x, At: FieldTrackingRules.ToStoredUtc(x.RecordedAt)))
            .Where(x => x.At >= from && x.At <= to)
            .GroupBy(x => x.At).Select(x => x.First())
            .ToList();
        var existing = candidates.Count == 0
            ? new HashSet<DateTime>()
            : await _points.GetRecordedTimesAsync(session.Id, candidates.Min(x => x.At), candidates.Max(x => x.At), cancellationToken);
        var fresh = candidates.Where(x => !existing.Contains(x.At))
            .Select(x => new LocationPoint(session.Id, session.OrganizationId, session.UserId, x.At, now,
                x.Input.Latitude, x.Input.Longitude, x.Input.AccuracyMeters, x.Input.SpeedMetersPerSecond,
                x.Input.HeadingDegrees, x.Input.IsMock, x.Input.BatteryPercent, x.Input.IsCharging, x.Input.IsBatterySaverOn))
            .ToList();

        if (fresh.Count > 0)
        {
            await _points.AddRangeAsync(fresh, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return new RecordLocationPointsResultDto(session.Id, fresh.Count, request.Points.Count - fresh.Count, session.IsOpen);
    }
}

public sealed class GiveFieldTrackingConsentCommandHandler : IRequestHandler<GiveFieldTrackingConsentCommand>
{
    private readonly IFieldTrackingConsentRepository _consents; private readonly IOrganizationAccessGuard _access;
    private readonly ICurrentUserService _user; private readonly IUnitOfWork _unitOfWork;
    public GiveFieldTrackingConsentCommandHandler(IFieldTrackingConsentRepository consents, IOrganizationAccessGuard access,
        ICurrentUserService user, IUnitOfWork unitOfWork) =>
        (_consents, _access, _user, _unitOfWork) = (consents, access, user, unitOfWork);
    public async Task Handle(GiveFieldTrackingConsentCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureOrganizationAsync(request.OrganizationId, cancellationToken);
        if (request.PolicyVersion != FieldTrackingPolicy.CurrentVersion)
            throw new BusinessException("TRACKING_POLICY_OUTDATED", "The location-sharing terms have changed. Update the app and read them again.");
        if (await _consents.GetAsync(request.OrganizationId, _user.UserId, request.PolicyVersion, cancellationToken) is not null)
            return;
        await _consents.AddAsync(new FieldTrackingConsent(request.OrganizationId, _user.UserId, request.PolicyVersion,
            DateTime.UtcNow), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class WithdrawFieldTrackingConsentCommandHandler : IRequestHandler<WithdrawFieldTrackingConsentCommand>
{
    private readonly IFieldTrackingConsentRepository _consents; private readonly IOrganizationAccessGuard _access;
    private readonly ICurrentUserService _user; private readonly IUnitOfWork _unitOfWork;
    public WithdrawFieldTrackingConsentCommandHandler(IFieldTrackingConsentRepository consents, IOrganizationAccessGuard access,
        ICurrentUserService user, IUnitOfWork unitOfWork) =>
        (_consents, _access, _user, _unitOfWork) = (consents, access, user, unitOfWork);
    public async Task Handle(WithdrawFieldTrackingConsentCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureOrganizationAsync(request.OrganizationId, cancellationToken);
        var active = await _consents.GetActiveAsync(request.OrganizationId, _user.UserId, cancellationToken);
        if (active.Count == 0) return;
        foreach (var consent in active)
        {
            consent.Withdraw();
            _consents.Update(consent);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UpdateFieldTrackingSettingsCommandHandler : IRequestHandler<UpdateFieldTrackingSettingsCommand>
{
    private readonly IAttendanceSettingsRepository _settings; private readonly IAttendanceTrackedRoleRepository _trackedRoles;
    private readonly IOrganizationRoleRepository _roles; private readonly IOrganizationPermissionChecker _permissions;
    private readonly ICurrentUserService _user; private readonly IUnitOfWork _unitOfWork;
    public UpdateFieldTrackingSettingsCommandHandler(IAttendanceSettingsRepository settings,
        IAttendanceTrackedRoleRepository trackedRoles, IOrganizationRoleRepository roles,
        IOrganizationPermissionChecker permissions, ICurrentUserService user, IUnitOfWork unitOfWork) =>
        (_settings, _trackedRoles, _roles, _permissions, _user, _unitOfWork) =
        (settings, trackedRoles, roles, permissions, user, unitOfWork);
    public async Task Handle(UpdateFieldTrackingSettingsCommand request, CancellationToken cancellationToken)
    {
        await _permissions.EnsurePermissionAsync(request.OrganizationId, _user.UserId,
            OrganizationPermissionNames.ManageAttendance, cancellationToken);

        var wanted = request.TrackedRoleIds.Distinct().ToHashSet();
        var organizationRoles = (await _roles.GetOrganizationRolesAsync(request.OrganizationId, cancellationToken))
            .Select(x => x.Id).ToHashSet();
        if (!wanted.IsSubsetOf(organizationRoles))
            throw new BusinessException("TRACKING_ROLE_NOT_FOUND", "One of the chosen roles does not belong to this organization.");

        var settings = await _settings.GetByOrganizationAsync(request.OrganizationId, cancellationToken);
        if (settings is null)
        {
            settings = new AttendanceSettings(request.OrganizationId, AttendanceSettings.DefaultTimeZone,
                AttendanceSettings.DefaultAutoCloseAfterHours, AttendanceSettings.DefaultPhotoRetentionDays, _user.UserId);
            settings.UpdateTracking(request.TrackingEnabled, request.LocationRetentionDays, _user.UserId);
            await _settings.AddAsync(settings, cancellationToken);
        }
        else
        {
            settings.UpdateTracking(request.TrackingEnabled, request.LocationRetentionDays, _user.UserId);
            _settings.Update(settings);
        }

        // Applies from each member's next punch-in; a session already open keeps what it started with.
        var current = await _trackedRoles.GetByOrganizationAsync(request.OrganizationId, cancellationToken);
        foreach (var tracked in current.Where(x => !wanted.Contains(x.OrganizationRoleId)))
        {
            tracked.SoftDelete();
            _trackedRoles.Update(tracked);
        }
        var already = current.Select(x => x.OrganizationRoleId).ToHashSet();
        foreach (var roleId in wanted.Where(x => !already.Contains(x)))
            await _trackedRoles.AddAsync(new AttendanceTrackedRole(request.OrganizationId, roleId, _user.UserId), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

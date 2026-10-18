using FluentValidation;
using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Attendance;

// The selfie is mandatory on both punches. Time is never taken from the request: it is the server's.
// Location is optional and only recorded — it never decides whether a punch is accepted.
public sealed record PunchInCommand(int OrganizationId, string? Note, long PhotoLength, Stream? Photo,
    double? Latitude = null, double? Longitude = null, double? AccuracyMeters = null) : IRequest<int>;

public sealed record PunchOutCommand(int OrganizationId, string? Note, long PhotoLength, Stream? Photo,
    double? Latitude = null, double? Longitude = null, double? AccuracyMeters = null) : IRequest;

public sealed record AdjustAttendanceSessionCommand(int SessionId, DateTimeOffset PunchInAtUtc,
    DateTimeOffset PunchOutAtUtc, string Reason) : IRequest;

public sealed record UpdateAttendanceSettingsCommand(int OrganizationId, string TimeZone, int AutoCloseAfterHours,
    int PhotoRetentionDays) : IRequest;

public sealed class PunchInCommandValidator : AbstractValidator<PunchInCommand>
{
    public PunchInCommandValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d).When(x => x.Latitude is not null);
        RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d).When(x => x.Longitude is not null);
        RuleFor(x => x.AccuracyMeters).GreaterThanOrEqualTo(0d).When(x => x.AccuracyMeters is not null);
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("Send both latitude and longitude, or neither.");
    }
}

public sealed class PunchOutCommandValidator : AbstractValidator<PunchOutCommand>
{
    public PunchOutCommandValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d).When(x => x.Latitude is not null);
        RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d).When(x => x.Longitude is not null);
        RuleFor(x => x.AccuracyMeters).GreaterThanOrEqualTo(0d).When(x => x.AccuracyMeters is not null);
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("Send both latitude and longitude, or neither.");
    }
}

internal static class PunchLocations
{
    public static PunchLocation? From(double? latitude, double? longitude, double? accuracyMeters) =>
        latitude is { } lat && longitude is { } lng ? new PunchLocation(lat, lng, accuracyMeters) : null;
}

public sealed class AdjustAttendanceSessionCommandValidator : AbstractValidator<AdjustAttendanceSessionCommand>
{
    public AdjustAttendanceSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x).Must(x => x.PunchOutAtUtc > x.PunchInAtUtc).WithMessage("Punch-out must be after punch-in.");
        RuleFor(x => x).Must(x => x.PunchOutAtUtc - x.PunchInAtUtc <= TimeSpan.FromHours(24))
            .WithMessage("A session cannot be longer than 24 hours.");
        RuleFor(x => x.PunchOutAtUtc).Must(x => x <= DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("Punch-out cannot be in the future.");
    }
}

public sealed class UpdateAttendanceSettingsCommandValidator : AbstractValidator<UpdateAttendanceSettingsCommand>
{
    public UpdateAttendanceSettingsCommandValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(100).Must(AttendanceClock.IsKnown)
            .WithMessage("Time zone is not recognized.");
        RuleFor(x => x.AutoCloseAfterHours).InclusiveBetween(1, 24);
        RuleFor(x => x.PhotoRetentionDays).InclusiveBetween(7, 3650);
    }
}

public sealed class PunchInCommandHandler : IRequestHandler<PunchInCommand, int>
{
    private readonly IAttendanceSessionRepository _sessions; private readonly IAttendanceSettingsRepository _settings;
    private readonly IOrganizationAccessGuard _access; private readonly IObjectStorage _storage;
    private readonly ICurrentUserService _user; private readonly IUnitOfWork _unitOfWork;
    private readonly IOrganizationMemberRepository _members; private readonly IAttendanceTrackedRoleRepository _trackedRoles;
    private readonly IFieldTrackingConsentRepository _consents;
    public PunchInCommandHandler(IAttendanceSessionRepository sessions, IAttendanceSettingsRepository settings,
        IOrganizationAccessGuard access, IObjectStorage storage, ICurrentUserService user, IUnitOfWork unitOfWork,
        IOrganizationMemberRepository members, IAttendanceTrackedRoleRepository trackedRoles,
        IFieldTrackingConsentRepository consents) =>
        (_sessions, _settings, _access, _storage, _user, _unitOfWork, _members, _trackedRoles, _consents) =
        (sessions, settings, access, storage, user, unitOfWork, members, trackedRoles, consents);
    public async Task<int> Handle(PunchInCommand request, CancellationToken cancellationToken)
    {
        // Owner or active member: a deactivated member cannot punch.
        await _access.EnsureOrganizationAsync(request.OrganizationId, cancellationToken);
        // A tracked member must have agreed to location sharing first — checked before the selfie
        // is stored, so a refusal leaves nothing behind.
        var isTracked = await FieldTrackingRules.MustTrackAsync(_settings, _members, _trackedRoles,
            request.OrganizationId, _user.UserId, cancellationToken);
        if (isTracked && await _consents.GetAsync(request.OrganizationId, _user.UserId,
                FieldTrackingPolicy.CurrentVersion, cancellationToken) is null)
            throw new BusinessException("TRACKING_CONSENT_REQUIRED",
                "Your role shares location while punched in. Open the latest Taskaro app and agree to location sharing to punch in.");
        var photo = await AttendancePhotoPolicy.ReadAsync(request.Photo, request.PhotoLength, cancellationToken);
        var settings = await _settings.GetByOrganizationAsync(request.OrganizationId, cancellationToken);
        var now = DateTime.UtcNow;

        var open = await _sessions.GetOpenAsync(request.OrganizationId, _user.UserId, cancellationToken);
        if (open is not null)
        {
            var autoCloseAfter = TimeSpan.FromHours(settings?.AutoCloseAfterHours ?? AttendanceSettings.DefaultAutoCloseAfterHours);
            if (now - open.PunchInAtUtc < autoCloseAfter)
                throw new ConflictException("ATTENDANCE_ALREADY_PUNCHED_IN", "You are already punched in. Punch out first.");
            // Yesterday's forgotten punch-out must never lock anyone out of today. Saved on its own,
            // before the new row, so the one-open-session index already sees it closed.
            open.AutoClose(open.PunchInAtUtc + autoCloseAfter);
            _sessions.Update(open);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var storageKey = AttendancePhotoPolicy.StorageKey(request.OrganizationId, _user.UserId, "in");
        await _storage.UploadAsync(storageKey, new MemoryStream(photo.Content, writable: false), photo.ContentType, cancellationToken);
        try
        {
            var workDate = AttendanceClock.WorkDate(now, AttendanceClock.Resolve(settings?.TimeZone));
            var session = AttendanceSession.PunchIn(request.OrganizationId, _user.UserId, workDate, now, request.Note,
                new AttendancePhoto(storageKey, photo.ContentType, photo.Content.LongLength, photo.Sha256),
                PunchLocations.From(request.Latitude, request.Longitude, request.AccuracyMeters), isTracked);
            await _sessions.AddAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return session.Id;
        }
        catch
        {
            await AttendancePhotoPolicy.TryDeleteAsync(_storage, storageKey);
            throw;
        }
    }
}

public sealed class PunchOutCommandHandler : IRequestHandler<PunchOutCommand>
{
    private readonly IAttendanceSessionRepository _sessions; private readonly IOrganizationAccessGuard _access;
    private readonly IObjectStorage _storage; private readonly ICurrentUserService _user; private readonly IUnitOfWork _unitOfWork;
    public PunchOutCommandHandler(IAttendanceSessionRepository sessions, IOrganizationAccessGuard access,
        IObjectStorage storage, ICurrentUserService user, IUnitOfWork unitOfWork) =>
        (_sessions, _access, _storage, _user, _unitOfWork) = (sessions, access, storage, user, unitOfWork);
    public async Task Handle(PunchOutCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureOrganizationAsync(request.OrganizationId, cancellationToken);
        var photo = await AttendancePhotoPolicy.ReadAsync(request.Photo, request.PhotoLength, cancellationToken);
        var session = await _sessions.GetOpenAsync(request.OrganizationId, _user.UserId, cancellationToken) ??
            throw new ConflictException("ATTENDANCE_NOT_PUNCHED_IN", "You are not punched in.");

        var storageKey = AttendancePhotoPolicy.StorageKey(request.OrganizationId, _user.UserId, "out");
        await _storage.UploadAsync(storageKey, new MemoryStream(photo.Content, writable: false), photo.ContentType, cancellationToken);
        try
        {
            session.PunchOut(DateTime.UtcNow, request.Note,
                new AttendancePhoto(storageKey, photo.ContentType, photo.Content.LongLength, photo.Sha256),
                PunchLocations.From(request.Latitude, request.Longitude, request.AccuracyMeters));
            _sessions.Update(session);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await AttendancePhotoPolicy.TryDeleteAsync(_storage, storageKey);
            throw;
        }
    }
}

public sealed class AdjustAttendanceSessionCommandHandler : IRequestHandler<AdjustAttendanceSessionCommand>
{
    private readonly IAttendanceSessionRepository _sessions; private readonly IOrganizationPermissionChecker _permissions;
    private readonly ICurrentUserService _user; private readonly IUnitOfWork _unitOfWork;
    public AdjustAttendanceSessionCommandHandler(IAttendanceSessionRepository sessions,
        IOrganizationPermissionChecker permissions, ICurrentUserService user, IUnitOfWork unitOfWork) =>
        (_sessions, _permissions, _user, _unitOfWork) = (sessions, permissions, user, unitOfWork);
    public async Task Handle(AdjustAttendanceSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(request.SessionId, cancellationToken) ??
            throw new NotFoundException("ATTENDANCE_SESSION_NOT_FOUND", "Attendance record not found.");
        await _permissions.EnsurePermissionAsync(session.OrganizationId, _user.UserId,
            OrganizationPermissionNames.ManageAttendance, cancellationToken);
        session.Adjust(request.PunchInAtUtc.UtcDateTime, request.PunchOutAtUtc.UtcDateTime, request.Reason,
            _user.UserId, DateTime.UtcNow);
        _sessions.Update(session);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UpdateAttendanceSettingsCommandHandler : IRequestHandler<UpdateAttendanceSettingsCommand>
{
    private readonly IAttendanceSettingsRepository _settings; private readonly IOrganizationPermissionChecker _permissions;
    private readonly ICurrentUserService _user; private readonly IUnitOfWork _unitOfWork;
    public UpdateAttendanceSettingsCommandHandler(IAttendanceSettingsRepository settings,
        IOrganizationPermissionChecker permissions, ICurrentUserService user, IUnitOfWork unitOfWork) =>
        (_settings, _permissions, _user, _unitOfWork) = (settings, permissions, user, unitOfWork);
    public async Task Handle(UpdateAttendanceSettingsCommand request, CancellationToken cancellationToken)
    {
        await _permissions.EnsurePermissionAsync(request.OrganizationId, _user.UserId,
            OrganizationPermissionNames.ManageAttendance, cancellationToken);
        var settings = await _settings.GetByOrganizationAsync(request.OrganizationId, cancellationToken);
        if (settings is null)
        {
            await _settings.AddAsync(new AttendanceSettings(request.OrganizationId, request.TimeZone,
                request.AutoCloseAfterHours, request.PhotoRetentionDays, _user.UserId), cancellationToken);
        }
        else
        {
            settings.Update(request.TimeZone, request.AutoCloseAfterHours, request.PhotoRetentionDays, _user.UserId);
            _settings.Update(settings);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

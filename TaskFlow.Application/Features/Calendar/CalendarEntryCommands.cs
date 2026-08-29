using FluentValidation;
using MediatR;
using System.Linq.Expressions;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Enums.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Calendar;

public sealed record CreateCalendarEntryCommand(
    int OrganizationId, CalendarEntryKind Kind, string Title, string? Description,
    DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, bool IsAllDay, string TimeZone,
    int? MemberUserId, CalendarRecurrenceFrequency RecurrenceFrequency = CalendarRecurrenceFrequency.None,
    int RecurrenceInterval = 1, DateOnly? RecurrenceUntil = null) : IRequest<int>;

public sealed record UpdateCalendarEntryCommand(
    int Id, CalendarEntryKind Kind, string Title, string? Description,
    DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, bool IsAllDay, string TimeZone,
    int? MemberUserId, CalendarRecurrenceFrequency RecurrenceFrequency = CalendarRecurrenceFrequency.None,
    int RecurrenceInterval = 1, DateOnly? RecurrenceUntil = null) : IRequest;

public sealed record DeleteCalendarEntryCommand(int Id) : IRequest;

internal static class CalendarEntryRules
{
    public static void Apply<T>(AbstractValidator<T> validator,
        Expression<Func<T, CalendarEntryKind>> kindExpression, Expression<Func<T, string>> titleExpression,
        Expression<Func<T, DateTimeOffset>> startsExpression, Expression<Func<T, DateTimeOffset>> endsExpression,
        Expression<Func<T, bool>> allDayExpression, Expression<Func<T, string>> timeZoneExpression,
        Expression<Func<T, int?>> memberExpression,
        Expression<Func<T, CalendarRecurrenceFrequency>> frequencyExpression,
        Expression<Func<T, int>> intervalExpression, Expression<Func<T, DateOnly?>> untilExpression)
    {
        var kind = kindExpression.Compile(); var starts = startsExpression.Compile();
        var ends = endsExpression.Compile(); var allDay = allDayExpression.Compile();
        var member = memberExpression.Compile(); var frequency = frequencyExpression.Compile();
        var until = untilExpression.Compile();
        validator.RuleFor(kindExpression).IsInEnum();
        validator.RuleFor(frequencyExpression).IsInEnum();
        validator.RuleFor(titleExpression).NotEmpty().MaximumLength(160);
        validator.RuleFor(x => x).Must(x => ends(x) > starts(x)).WithMessage("End must be after start.");
        validator.RuleFor(timeZoneExpression).NotEmpty().MaximumLength(100).Must(BeTimeZone).WithMessage("Time zone is not recognized.");
        validator.RuleFor(x => x).Must(x => kind(x) != CalendarEntryKind.MemberLeave || member(x).HasValue)
            .WithMessage("Member leave requires a member.");
        validator.RuleFor(x => x).Must(x => kind(x) == CalendarEntryKind.MemberLeave || !member(x).HasValue)
            .WithMessage("Only member leave can target a member.");
        validator.RuleFor(x => x).Must(x => kind(x) == CalendarEntryKind.OrganizationEvent || allDay(x))
            .WithMessage("Leave and holidays must be all-day.");
        validator.RuleFor(x => x).Must(x => !allDay(x) ||
            starts(x).UtcDateTime.TimeOfDay == TimeSpan.Zero && ends(x).UtcDateTime.TimeOfDay == TimeSpan.Zero)
            .WithMessage("All-day boundaries must use UTC midnight.");
        validator.RuleFor(intervalExpression).InclusiveBetween(1, 30);
        validator.RuleFor(x => x).Must(x => frequency(x) != CalendarRecurrenceFrequency.None || until(x) is null)
            .WithMessage("A non-recurring entry cannot have a recurrence end.");
        validator.RuleFor(x => x).Must(x => frequency(x) == CalendarRecurrenceFrequency.None ||
            until(x) is null || until(x) >= DateOnly.FromDateTime(starts(x).UtcDateTime))
            .WithMessage("Recurrence end cannot be before the first occurrence.");
    }

    private static bool BeTimeZone(string value)
    {
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(value); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}

public sealed class CreateCalendarEntryCommandValidator : AbstractValidator<CreateCalendarEntryCommand>
{
    public CreateCalendarEntryCommandValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        CalendarEntryRules.Apply(this, x => x.Kind, x => x.Title, x => x.StartsAtUtc, x => x.EndsAtUtc,
            x => x.IsAllDay, x => x.TimeZone, x => x.MemberUserId, x => x.RecurrenceFrequency,
            x => x.RecurrenceInterval, x => x.RecurrenceUntil);
    }
}

public sealed class UpdateCalendarEntryCommandValidator : AbstractValidator<UpdateCalendarEntryCommand>
{
    public UpdateCalendarEntryCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        CalendarEntryRules.Apply(this, x => x.Kind, x => x.Title, x => x.StartsAtUtc, x => x.EndsAtUtc,
            x => x.IsAllDay, x => x.TimeZone, x => x.MemberUserId, x => x.RecurrenceFrequency,
            x => x.RecurrenceInterval, x => x.RecurrenceUntil);
    }
}

public sealed class CreateCalendarEntryCommandHandler : IRequestHandler<CreateCalendarEntryCommand, int>
{
    private readonly ICalendarEntryRepository _entries; private readonly IOrganizationMemberRepository _members;
    private readonly IOrganizationPermissionChecker _permissions; private readonly ICurrentUserService _user;
    private readonly IUnitOfWork _unitOfWork;
    public CreateCalendarEntryCommandHandler(ICalendarEntryRepository entries, IOrganizationMemberRepository members,
        IOrganizationPermissionChecker permissions, ICurrentUserService user, IUnitOfWork unitOfWork) =>
        (_entries, _members, _permissions, _user, _unitOfWork) = (entries, members, permissions, user, unitOfWork);
    public async Task<int> Handle(CreateCalendarEntryCommand request, CancellationToken cancellationToken)
    {
        await _permissions.EnsurePermissionAsync(request.OrganizationId, _user.UserId,
            OrganizationPermissionNames.ManageCalendar, cancellationToken);
        if (request.MemberUserId is int userId && !await _members.IsActiveMemberAsync(request.OrganizationId, userId, cancellationToken))
            throw new NotFoundException("CALENDAR_MEMBER_NOT_FOUND", "The selected active member was not found in this organization.");
        var entry = new CalendarEntry(request.OrganizationId, request.Kind, request.Title, request.Description,
            request.StartsAtUtc.UtcDateTime, request.EndsAtUtc.UtcDateTime, request.IsAllDay, request.TimeZone, request.MemberUserId,
            request.RecurrenceFrequency, request.RecurrenceInterval, request.RecurrenceUntil, _user.UserId);
        await _entries.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }
}

public sealed class UpdateCalendarEntryCommandHandler : IRequestHandler<UpdateCalendarEntryCommand>
{
    private readonly ICalendarEntryRepository _entries; private readonly IOrganizationMemberRepository _members;
    private readonly IOrganizationPermissionChecker _permissions; private readonly ICurrentUserService _user;
    private readonly IUnitOfWork _unitOfWork;
    public UpdateCalendarEntryCommandHandler(ICalendarEntryRepository entries, IOrganizationMemberRepository members,
        IOrganizationPermissionChecker permissions, ICurrentUserService user, IUnitOfWork unitOfWork) =>
        (_entries, _members, _permissions, _user, _unitOfWork) = (entries, members, permissions, user, unitOfWork);
    public async Task Handle(UpdateCalendarEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _entries.GetByIdAsync(request.Id, cancellationToken) ??
            throw new NotFoundException("CALENDAR_ENTRY_NOT_FOUND", "Calendar entry not found.");
        await _permissions.EnsurePermissionAsync(entry.OrganizationId, _user.UserId,
            OrganizationPermissionNames.ManageCalendar, cancellationToken);
        if (request.MemberUserId is int userId && !await _members.IsActiveMemberAsync(entry.OrganizationId, userId, cancellationToken))
            throw new NotFoundException("CALENDAR_MEMBER_NOT_FOUND", "The selected active member was not found in this organization.");
        entry.Update(request.Kind, request.Title, request.Description, request.StartsAtUtc.UtcDateTime, request.EndsAtUtc.UtcDateTime,
            request.IsAllDay, request.TimeZone, request.MemberUserId, request.RecurrenceFrequency,
            request.RecurrenceInterval, request.RecurrenceUntil);
        _entries.Update(entry); await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteCalendarEntryCommandHandler : IRequestHandler<DeleteCalendarEntryCommand>
{
    private readonly ICalendarEntryRepository _entries; private readonly IOrganizationPermissionChecker _permissions;
    private readonly ICurrentUserService _user; private readonly IUnitOfWork _unitOfWork;
    public DeleteCalendarEntryCommandHandler(ICalendarEntryRepository entries, IOrganizationPermissionChecker permissions,
        ICurrentUserService user, IUnitOfWork unitOfWork) =>
        (_entries, _permissions, _user, _unitOfWork) = (entries, permissions, user, unitOfWork);
    public async Task Handle(DeleteCalendarEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _entries.GetByIdAsync(request.Id, cancellationToken) ??
            throw new NotFoundException("CALENDAR_ENTRY_NOT_FOUND", "Calendar entry not found.");
        await _permissions.EnsurePermissionAsync(entry.OrganizationId, _user.UserId,
            OrganizationPermissionNames.ManageCalendar, cancellationToken);
        _entries.Remove(entry); await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

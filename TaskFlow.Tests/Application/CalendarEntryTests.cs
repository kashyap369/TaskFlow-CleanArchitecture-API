using NSubstitute;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.Calendar;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Enums.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Tests.Application;

public sealed class CalendarEntryTests
{
    [Fact]
    public void Domain_EnforcesLeaveBoundaryAndAllDayRule()
    {
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Throws<ArgumentException>(() => new CalendarEntry(7, CalendarEntryKind.MemberLeave,
            "Leave", null, start, start.AddDays(1), true, "UTC", null,
            CalendarRecurrenceFrequency.None, 1, null, 4));
        Assert.Throws<ArgumentException>(() => new CalendarEntry(7, CalendarEntryKind.Holiday,
            "Holiday", null, start, start.AddHours(2), false, "UTC", null,
            CalendarRecurrenceFrequency.None, 1, null, 4));
    }

    [Fact]
    public void Validator_RejectsUnknownTimezoneAndInvalidRecurrence()
    {
        var start = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);
        var result = new CreateCalendarEntryCommandValidator().Validate(new CreateCalendarEntryCommand(
            7, CalendarEntryKind.OrganizationEvent, "Stand-up", null, start, start.AddHours(1),
            false, "Mars/Olympus", null, CalendarRecurrenceFrequency.Weekly, 0,
            new DateOnly(2026, 8, 1)));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("Time zone"));
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("Recurrence end"));
    }

    [Fact]
    public async Task Create_RequiresCalendarPermissionAndActiveOrganizationMember()
    {
        var entries = Substitute.For<ICalendarEntryRepository>();
        var members = Substitute.For<IOrganizationMemberRepository>();
        members.IsActiveMemberAsync(7, 22, Arg.Any<CancellationToken>()).Returns(true);
        var permissions = Substitute.For<IOrganizationPermissionChecker>();
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(4);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new CreateCalendarEntryCommandHandler(entries, members, permissions, user, unitOfWork);
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        await handler.Handle(new CreateCalendarEntryCommand(7, CalendarEntryKind.MemberLeave, "Annual leave",
            null, start, start.AddDays(2), true, "UTC", 22), CancellationToken.None);

        await permissions.Received(1).EnsurePermissionAsync(7, 4,
            OrganizationPermissionNames.ManageCalendar, Arg.Any<CancellationToken>());
        await entries.Received(1).AddAsync(Arg.Is<CalendarEntry>(x => x.OrganizationId == 7 &&
            x.MemberUserId == 22), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_SoftDeletesOnlyTheCalendarAggregate()
    {
        var start = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
        var entry = new CalendarEntry(7, CalendarEntryKind.Holiday, "Foundation day", null,
            start, start.AddDays(1), true, "UTC", null, CalendarRecurrenceFrequency.None, 1, null, 4);
        var entries = Substitute.For<ICalendarEntryRepository>();
        entries.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(entry);
        entries.When(x => x.Remove(entry)).Do(_ => entry.SoftDelete());
        var permissions = Substitute.For<IOrganizationPermissionChecker>();
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(4);
        var unitOfWork = Substitute.For<IUnitOfWork>();

        await new DeleteCalendarEntryCommandHandler(entries, permissions, user, unitOfWork)
            .Handle(new DeleteCalendarEntryCommand(9), CancellationToken.None);

        entries.Received(1).Remove(entry);
        Assert.True(entry.IsDeleted);
    }
}

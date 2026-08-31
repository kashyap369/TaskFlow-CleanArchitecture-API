using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Enums.Meetings;

namespace TaskFlow.Tests.Domain;

public sealed class MeetingTests
{
    private static Meeting Create(DateTime? start = null, DateTime? end = null) =>
        new(7, 11, "Planning review", null, start, end, "UTC", "meeting-test",
            true, false, true, true, true, false, 90);

    [Fact]
    public void Lifecycle_AllowsOnlyDraftOrScheduled_ToStartCancel_AndOnlyLiveToEnd()
    {
        var meeting = Create();
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        meeting.Start(DateTime.UtcNow);
        Assert.Equal(MeetingStatus.Live, meeting.Status);
        Assert.Throws<InvalidOperationException>(() => meeting.Cancel());
        meeting.End(DateTime.UtcNow.AddHours(1));
        Assert.Equal(MeetingStatus.Ended, meeting.Status);
        Assert.Throws<InvalidOperationException>(() => meeting.Start(DateTime.UtcNow));
    }

    [Fact]
    public void ScheduleAndHostInvariants_AreEnforced()
    {
        var now = DateTime.UtcNow.AddDays(1);
        Assert.Throws<ArgumentException>(() => Create(now, now));
        var meeting = Create(now, now.AddHours(1));
        var host = Assert.Single(meeting.Participants);
        Assert.Equal(MeetingAccessLevel.Host, host.AccessLevel);
        Assert.Throws<InvalidOperationException>(() =>
            meeting.UpdateParticipant(host.Id, MeetingAccessLevel.Participant, null, MeetingParticipantState.Admitted));
        Assert.Throws<InvalidOperationException>(() =>
            meeting.AddRegisteredParticipant(22, MeetingAccessLevel.Host));
    }

    [Fact]
    public void AccessLinks_StoreOnlyHash_AndCannotGrantHost()
    {
        var meeting = Create();
        var link = meeting.AddAccessLink(new string('A', 64), MeetingAccessLinkMode.PrivateInvitation,
            "guest@example.test", MeetingAccessLevel.Viewer, null, DateTime.UtcNow.AddHours(1), 1);
        Assert.Equal(new string('A', 64), link.TokenHash);
        Assert.DoesNotContain("guest-token", link.TokenHash);
        Assert.Throws<InvalidOperationException>(() => meeting.AddAccessLink(new string('B', 64),
            MeetingAccessLinkMode.Reusable, null, MeetingAccessLevel.Host, null,
            DateTime.UtcNow.AddHours(1), null));
    }

    [Fact]
    public void BadgeMetadata_RejectsHtmlAndUnsafeStyleValues()
    {
        var meeting = Create();
        Assert.Throws<ArgumentException>(() => meeting.AddBadge("<b>Manager</b>", "blue", null));
        Assert.Throws<ArgumentException>(() => meeting.AddBadge("Manager", "url-javascript", "icon<script>"));
        var badge = meeting.AddBadge("Product lead", "indigo", "BriefcaseBusiness");
        Assert.Equal("Product lead", badge.Label);
    }

    [Fact]
    public void GuestIdentity_IsStable_AndLinkUseLimitCannotBeExceeded()
    {
        var meeting = new Meeting(7, 11, "Guest review", null, null, null, "UTC", "guest-review",
            true, true, true, true, true, false, 90);
        var link = meeting.AddAccessLink(new string('C', 64), MeetingAccessLinkMode.Reusable, null,
            MeetingAccessLevel.Participant, null, DateTime.UtcNow.AddHours(1), 1);
        var first = meeting.AddGuestParticipant("GUEST@EXAMPLE.TEST", "Guest Person",
            MeetingAccessLevel.Participant, null);
        var duplicate = meeting.AddGuestParticipant("guest@example.test", "Another Name",
            MeetingAccessLevel.Participant, null);
        Assert.Same(first, duplicate);
        link.RegisterUse(DateTime.UtcNow);
        Assert.False(link.IsAvailable(DateTime.UtcNow));
        Assert.False(link.IsActive(DateTime.UtcNow.AddHours(2)));
        Assert.Throws<InvalidOperationException>(() => link.RegisterUse(DateTime.UtcNow));
    }
}

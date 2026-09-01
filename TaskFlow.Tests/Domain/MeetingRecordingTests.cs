using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Enums.Meetings;

namespace TaskFlow.Tests.Domain;

public sealed class MeetingRecordingTests
{
    [Fact]
    public void RecordingCannotStartUntilEveryRequiredParticipantAccepts()
    {
        var recording = new MeetingRecording(4, 10, "meetings/4/recordings/a.mp4", [10, 11], DateTime.UtcNow.AddMinutes(1));
        recording.RecordConsent(10, true, DateTime.UtcNow);

        Assert.False(recording.AllAccepted);
        Assert.Throws<InvalidOperationException>(() => recording.BeginStarting("EG_1", DateTime.UtcNow));

        recording.RecordConsent(11, true, DateTime.UtcNow);
        recording.BeginStarting("EG_1", DateTime.UtcNow);
        Assert.Equal(MeetingRecordingStatus.Starting, recording.Status);
    }

    [Fact]
    public void ConsentDecisionIsImmutableAndDeclineRemainsAuditable()
    {
        var recording = new MeetingRecording(4, 10, "meetings/4/recordings/a.mp4", [10, 11], DateTime.UtcNow.AddMinutes(1));
        recording.RecordConsent(11, false, DateTime.UtcNow);
        recording.RecordConsent(11, true, DateTime.UtcNow.AddSeconds(1));

        Assert.True(recording.HasDecline);
        Assert.Equal(MeetingRecordingConsentStatus.Declined,
            recording.Consents.Single(x => x.ParticipantId == 11).Status);
    }

    [Fact]
    public void PendingConsentTimesOutWithoutClaimingRecordingSuccess()
    {
        var expires = DateTime.UtcNow.AddSeconds(1);
        var recording = new MeetingRecording(4, 10, "meetings/4/recordings/a.mp4", [10, 11], expires);
        recording.RecordConsent(10, true, DateTime.UtcNow);
        recording.ExpireConsent(expires.AddSeconds(1));

        Assert.Equal(MeetingRecordingStatus.Failed, recording.Status);
        Assert.Equal(MeetingRecordingConsentStatus.TimedOut,
            recording.Consents.Single(x => x.ParticipantId == 11).Status);
    }
}

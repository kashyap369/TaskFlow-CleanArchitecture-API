using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums.Meetings;

namespace TaskFlow.Domain.Entities.Meetings;

public sealed class MeetingRecording : AuditableEntity
{
    private readonly List<MeetingRecordingConsent> _consents = [];
    public int MeetingId { get; private set; }
    public int RequestedByParticipantId { get; private set; }
    public MeetingRecordingStatus Status { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string? ProviderEgressId { get; private set; }
    public DateTime ConsentExpiresAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? StoppedAtUtc { get; private set; }
    public DateTime? ReadyAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public long? SizeBytes { get; private set; }
    public long? DurationMilliseconds { get; private set; }
    public IReadOnlyCollection<MeetingRecordingConsent> Consents => _consents.AsReadOnly();

    private MeetingRecording() { }
    public MeetingRecording(int meetingId, int requesterParticipantId, string storageKey,
        IEnumerable<int> requiredParticipantIds, DateTime consentExpiresAtUtc)
    {
        if (meetingId <= 0 || requesterParticipantId <= 0) throw new ArgumentOutOfRangeException();
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        MeetingId = meetingId; RequestedByParticipantId = requesterParticipantId;
        StorageKey = storageKey; Status = MeetingRecordingStatus.PendingConsent;
        ConsentExpiresAtUtc = AsUtc(consentExpiresAtUtc);
        foreach (var participantId in requiredParticipantIds.Distinct())
            _consents.Add(new MeetingRecordingConsent(meetingId, participantId));
        if (_consents.All(x => x.ParticipantId != requesterParticipantId))
            _consents.Add(new MeetingRecordingConsent(meetingId, requesterParticipantId));
    }

    public void RecordConsent(int participantId, bool accepted, DateTime utcNow)
    {
        if (Status is not (MeetingRecordingStatus.PendingConsent or MeetingRecordingStatus.Starting or MeetingRecordingStatus.Recording))
            throw new InvalidOperationException("Recording consent is no longer open.");
        var consent = _consents.SingleOrDefault(x => x.ParticipantId == participantId);
        if (consent is null) { consent = new MeetingRecordingConsent(MeetingId, participantId); _consents.Add(consent); }
        consent.Decide(accepted, utcNow); MarkAsUpdated();
    }

    public void ExpireConsent(DateTime utcNow)
    {
        if (Status != MeetingRecordingStatus.PendingConsent || utcNow < ConsentExpiresAtUtc) return;
        foreach (var consent in _consents.Where(x => x.Status == MeetingRecordingConsentStatus.Pending)) consent.Timeout(utcNow);
        Fail("Recording consent timed out.");
    }

    public bool AllAccepted => _consents.Count > 0 && _consents.All(x => x.Status == MeetingRecordingConsentStatus.Accepted);
    public bool HasDecline => _consents.Any(x => x.Status == MeetingRecordingConsentStatus.Declined);
    /// <summary>
    /// Whether this participant was actually asked. A late joiner may still accept — that is how the
    /// join gate lets them in — but only someone in the requested set may decline, otherwise any
    /// assigned participant who never joined the call could veto a recording they are not part of.
    /// </summary>
    public bool WasConsentRequestedFrom(int participantId) => _consents.Any(x => x.ParticipantId == participantId);

    public bool HasAcceptedConsent(int participantId) => _consents.Any(x => x.ParticipantId == participantId && x.Status == MeetingRecordingConsentStatus.Accepted);

    public void BeginStarting(string providerEgressId, DateTime utcNow)
    {
        if (!AllAccepted) throw new InvalidOperationException("Every current participant must consent before recording starts.");
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEgressId);
        ProviderEgressId = providerEgressId.Trim(); Status = MeetingRecordingStatus.Starting;
        StartedAtUtc = AsUtc(utcNow); MarkAsUpdated();
    }
    public void MarkRecording(DateTime utcNow) { if (Status is MeetingRecordingStatus.Starting or MeetingRecordingStatus.Recording) { Status = MeetingRecordingStatus.Recording; StartedAtUtc ??= AsUtc(utcNow); MarkAsUpdated(); } }
    public void MarkProcessing(DateTime utcNow) { if (Status is MeetingRecordingStatus.Starting or MeetingRecordingStatus.Recording or MeetingRecordingStatus.Processing) { Status = MeetingRecordingStatus.Processing; StoppedAtUtc ??= AsUtc(utcNow); MarkAsUpdated(); } }
    public void MarkReady(DateTime utcNow, long? sizeBytes, long? durationMilliseconds)
    { Status = MeetingRecordingStatus.Ready; ReadyAtUtc = AsUtc(utcNow); StoppedAtUtc ??= ReadyAtUtc; SizeBytes = sizeBytes; DurationMilliseconds = durationMilliseconds; FailureReason = null; MarkAsUpdated(); }
    public void Fail(string reason) { Status = MeetingRecordingStatus.Failed; FailureReason = string.IsNullOrWhiteSpace(reason) ? "Recording failed." : reason.Trim()[..Math.Min(reason.Trim().Length, 500)]; MarkAsUpdated(); }
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

public sealed class MeetingRecordingConsent : AuditableEntity
{
    public int MeetingRecordingId { get; private set; }
    public int MeetingId { get; private set; }
    public int ParticipantId { get; private set; }
    public MeetingRecordingConsentStatus Status { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    private MeetingRecordingConsent() { }
    internal MeetingRecordingConsent(int meetingId, int participantId)
    { if (meetingId <= 0 || participantId <= 0) throw new ArgumentOutOfRangeException(); MeetingId = meetingId; ParticipantId = participantId; Status = MeetingRecordingConsentStatus.Pending; }
    internal void Decide(bool accepted, DateTime utcNow) { if (Status != MeetingRecordingConsentStatus.Pending) return; Status = accepted ? MeetingRecordingConsentStatus.Accepted : MeetingRecordingConsentStatus.Declined; DecidedAtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc); MarkAsUpdated(); }
    internal void Timeout(DateTime utcNow) { if (Status != MeetingRecordingConsentStatus.Pending) return; Status = MeetingRecordingConsentStatus.TimedOut; DecidedAtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc); MarkAsUpdated(); }
}

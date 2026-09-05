namespace TaskFlow.Application.Contracts.Meetings;

public interface IMeetingMediaProvider
{
    bool IsEnabled { get; }
    string WebSocketUrl { get; }

    MeetingJoinToken CreateJoinToken(MeetingJoinTokenRequest request);

    Task RemoveParticipantsAsync(string roomName, string participantIdentityPrefix,
        CancellationToken cancellationToken = default);

    Task MuteTrackAsync(string roomName, string participantIdentity, string trackSid, bool muted,
        CancellationToken cancellationToken = default);

    Task CloseRoomAsync(string roomName, CancellationToken cancellationToken = default);

    /// <summary>
    /// The identities currently connected to <paramref name="roomName"/>, straight from the provider.
    /// Recording consent must be built from this rather than from webhook-derived attendance: if a
    /// webhook is delayed or never arrives, attendance is silently short and people in the room are
    /// recorded without ever being asked. Implementations throw when the provider cannot answer, so
    /// callers fail closed instead of recording an unknown roster.
    /// </summary>
    Task<IReadOnlyList<string>> ListRoomParticipantIdentitiesAsync(string roomName,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    Task<MeetingEgressStartResult> StartRoomRecordingAsync(string roomName, string storageKey,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    Task StopRoomRecordingAsync(string providerEgressId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    Task<MeetingEgressStatusResult?> GetRoomRecordingStatusAsync(string providerEgressId,
        CancellationToken cancellationToken = default) => Task.FromResult<MeetingEgressStatusResult?>(null);

    MeetingProviderWebhook VerifyWebhook(string rawBody, string authorizationHeader);
}

public sealed record MeetingJoinTokenRequest(
    string RoomName,
    string ParticipantIdentity,
    string ParticipantName,
    TimeSpan Lifetime,
    bool CanPublish,
    bool CanSubscribe,
    bool CanPublishData,
    bool IsRoomAdmin,
    string? Metadata = null);

public sealed record MeetingJoinToken(
    string Value,
    DateTimeOffset ExpiresAtUtc);

public sealed record MeetingProviderWebhook(
    string EventId,
    string EventType,
    string? RoomName,
    string? ParticipantIdentity,
    string? ParticipantSid,
    DateTimeOffset? OccurredAtUtc,
    string? EgressId = null,
    string? EgressStatus = null,
    string? EgressError = null,
    long? EgressFileSize = null,
    long? EgressDurationMilliseconds = null);

public sealed record MeetingEgressStartResult(string ProviderEgressId);
public enum MeetingEgressState { Starting, Recording, Processing, Ready, Failed }
public sealed record MeetingEgressStatusResult(MeetingEgressState State, string? Error = null,
    long? FileSize = null, long? DurationMilliseconds = null);

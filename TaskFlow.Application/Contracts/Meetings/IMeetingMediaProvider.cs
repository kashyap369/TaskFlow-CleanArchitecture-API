namespace TaskFlow.Application.Contracts.Meetings;

public interface IMeetingMediaProvider
{
    string WebSocketUrl { get; }

    MeetingJoinToken CreateJoinToken(MeetingJoinTokenRequest request);

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
    bool IsRoomAdmin);

public sealed record MeetingJoinToken(
    string Value,
    DateTimeOffset ExpiresAtUtc);

public sealed record MeetingProviderWebhook(
    string EventId,
    string EventType,
    string? RoomName,
    string? ParticipantIdentity,
    DateTimeOffset? OccurredAtUtc);

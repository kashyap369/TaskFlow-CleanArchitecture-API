using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Meetings;

namespace TaskFlow.Infra.Meetings;

public sealed class LiveKitMeetingMediaProvider : IMeetingMediaProvider
{
    private readonly LiveKitSettings _settings;
    private readonly WebhookReceiver _webhookReceiver;

    public LiveKitMeetingMediaProvider(IOptions<LiveKitSettings> options)
    {
        _settings = options.Value;
        _webhookReceiver = new WebhookReceiver(_settings.ApiKey, _settings.ApiSecret);
    }

    public string WebSocketUrl => _settings.Url;

    public MeetingJoinToken CreateJoinToken(MeetingJoinTokenRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RoomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ParticipantIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ParticipantName);

        if (request.Lifetime <= TimeSpan.Zero || request.Lifetime > TimeSpan.FromMinutes(15))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "LiveKit join-token lifetime must be between one tick and 15 minutes.");
        }

        var expiresAtUtc = DateTimeOffset.UtcNow.Add(request.Lifetime);
        var token = new AccessToken(_settings.ApiKey, _settings.ApiSecret)
            .WithIdentity(request.ParticipantIdentity)
            .WithName(request.ParticipantName)
            .WithTtl(request.Lifetime)
            .WithGrants(new VideoGrants
            {
                Room = request.RoomName,
                RoomJoin = true,
                CanPublish = request.CanPublish,
                CanSubscribe = request.CanSubscribe,
                CanPublishData = request.CanPublishData,
                RoomAdmin = request.IsRoomAdmin,
                RoomCreate = false,
                RoomList = false,
                RoomRecord = false,
                IngressAdmin = false
            });

        return new MeetingJoinToken(token.ToJwt(), expiresAtUtc);
    }

    public MeetingProviderWebhook VerifyWebhook(string rawBody, string authorizationHeader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationHeader);

        var webhook = _webhookReceiver.Receive(rawBody, authorizationHeader);
        DateTimeOffset? occurredAtUtc = webhook.CreatedAt > 0
            ? DateTimeOffset.FromUnixTimeSeconds(webhook.CreatedAt)
            : null;

        return new MeetingProviderWebhook(
            webhook.Id,
            webhook.Event,
            webhook.Room?.Name,
            webhook.Participant?.Identity,
            occurredAtUtc);
    }
}

using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Meetings;

namespace TaskFlow.Infra.Meetings;

public sealed class LiveKitMeetingMediaProvider : IMeetingMediaProvider
{
    private readonly LiveKitSettings _settings;
    private readonly WebhookReceiver? _webhookReceiver;
    private readonly RoomServiceClient? _roomService;

    public LiveKitMeetingMediaProvider(IOptions<LiveKitSettings> options)
    {
        _settings = options.Value;
        if (_settings.Enabled)
        {
            _webhookReceiver = new WebhookReceiver(_settings.ApiKey, _settings.ApiSecret);
            _roomService = new RoomServiceClient(ToHttpUrl(_settings.Url), _settings.ApiKey, _settings.ApiSecret);
        }
    }

    public bool IsEnabled => _settings.Enabled;
    public string WebSocketUrl => _settings.Url;

    public MeetingJoinToken CreateJoinToken(MeetingJoinTokenRequest request)
    {
        if (!IsEnabled) throw new InvalidOperationException("LiveKit media is not enabled.");
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

        if (!string.IsNullOrWhiteSpace(request.Metadata))
            token.WithMetadata(request.Metadata);

        return new MeetingJoinToken(token.ToJwt(), expiresAtUtc);
    }

    public async Task RemoveParticipantsAsync(string roomName, string participantIdentityPrefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantIdentityPrefix);
        if (_roomService is null) throw new InvalidOperationException("LiveKit media is not enabled.");
        var response = await _roomService.ListParticipants(new ListParticipantsRequest { Room = roomName });
        foreach (var participant in response.Participants.Where(x =>
                     x.Identity.StartsWith(participantIdentityPrefix, StringComparison.Ordinal)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _roomService.RemoveParticipant(new RoomParticipantIdentity
            {
                Room = roomName,
                Identity = participant.Identity
            });
        }
    }

    public Task MuteTrackAsync(string roomName, string participantIdentity, string trackSid, bool muted,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(trackSid);
        if (_roomService is null) throw new InvalidOperationException("LiveKit media is not enabled.");
        return _roomService.MutePublishedTrack(new MuteRoomTrackRequest
        {
            Room = roomName,
            Identity = participantIdentity,
            TrackSid = trackSid,
            Muted = muted
        });
    }

    public async Task CloseRoomAsync(string roomName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        if (_roomService is null) return;
        var rooms = await _roomService.ListRooms(new ListRoomsRequest());
        if (rooms.Rooms.Any(x => string.Equals(x.Name, roomName, StringComparison.Ordinal)))
            await _roomService.DeleteRoom(new DeleteRoomRequest { Room = roomName });
    }

    public MeetingProviderWebhook VerifyWebhook(string rawBody, string authorizationHeader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationHeader);

        if (_webhookReceiver is null) throw new InvalidOperationException("LiveKit media is not enabled.");
        var webhook = _webhookReceiver.Receive(rawBody, authorizationHeader);
        DateTimeOffset? occurredAtUtc = webhook.CreatedAt > 0
            ? DateTimeOffset.FromUnixTimeSeconds(webhook.CreatedAt)
            : null;

        return new MeetingProviderWebhook(
            webhook.Id,
            webhook.Event,
            webhook.Room?.Name,
            webhook.Participant?.Identity,
            webhook.Participant?.Sid,
            occurredAtUtc);
    }

    private static string ToHttpUrl(string webSocketUrl)
    {
        var uri = new Uri(webSocketUrl);
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase) ? "https" : "http"
        };
        return builder.Uri.ToString().TrimEnd('/');
    }
}

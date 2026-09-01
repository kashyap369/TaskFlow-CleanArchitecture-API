using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Infra.Storage;

namespace TaskFlow.Infra.Meetings;

public sealed class LiveKitMeetingMediaProvider : IMeetingMediaProvider
{
    private readonly LiveKitSettings _settings;
    private readonly WebhookReceiver? _webhookReceiver;
    private readonly RoomServiceClient? _roomService;
    private readonly EgressServiceClient? _egressService;
    private readonly ObjectStorageSettings _storage;

    public LiveKitMeetingMediaProvider(IOptions<LiveKitSettings> options,
        IOptions<ObjectStorageSettings> storage)
    {
        _settings = options.Value;
        _storage = storage.Value;
        if (_settings.Enabled)
        {
            _webhookReceiver = new WebhookReceiver(_settings.ApiKey, _settings.ApiSecret);
            _roomService = new RoomServiceClient(ToHttpUrl(_settings.Url), _settings.ApiKey, _settings.ApiSecret);
            _egressService = new EgressServiceClient(ToHttpUrl(_settings.Url), _settings.ApiKey, _settings.ApiSecret);
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

    public async Task<MeetingEgressStartResult> StartRoomRecordingAsync(string roomName,
        string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        if (_egressService is null) throw new InvalidOperationException("LiveKit media is not enabled.");
        var output = new EncodedFileOutput { FileType = EncodedFileType.Mp4 };
        if (_storage.UsesLocalFileSystem)
        {
            output.Filepath = $"{_settings.EgressLocalOutputPath.TrimEnd('/')}/{storageKey}";
        }
        else
        {
            output.Filepath = storageKey;
            output.S3 = new S3Upload
            {
                AccessKey = _storage.AccessKey, Secret = _storage.SecretKey,
                Bucket = _storage.Bucket, Region = _storage.Region,
                Endpoint = _storage.Endpoint, ForcePathStyle = _storage.ForcePathStyle
            };
        }
        var request = new RoomCompositeEgressRequest { RoomName = roomName, Layout = "grid" };
        request.FileOutputs.Add(output);
        var result = await _egressService.StartRoomCompositeEgress(request);
        return new MeetingEgressStartResult(result.EgressId);
    }

    public async Task StopRoomRecordingAsync(string providerEgressId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEgressId);
        if (_egressService is null) throw new InvalidOperationException("LiveKit media is not enabled.");
        await _egressService.StopEgress(new StopEgressRequest { EgressId = providerEgressId });
    }

    public async Task<MeetingEgressStatusResult?> GetRoomRecordingStatusAsync(string providerEgressId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_egressService is null) return null;
        var response = await _egressService.ListEgress(new ListEgressRequest { EgressId = providerEgressId });
        var info = response.Items.FirstOrDefault(); if (info is null) return null;
        var value = info.Status.ToString();
        var state = value.Contains("Complete", StringComparison.OrdinalIgnoreCase) ? MeetingEgressState.Ready
            : value.Contains("Failed", StringComparison.OrdinalIgnoreCase) || value.Contains("Aborted", StringComparison.OrdinalIgnoreCase) || value.Contains("Limit", StringComparison.OrdinalIgnoreCase) ? MeetingEgressState.Failed
            : value.Contains("Ending", StringComparison.OrdinalIgnoreCase) ? MeetingEgressState.Processing
            : value.Contains("Active", StringComparison.OrdinalIgnoreCase) ? MeetingEgressState.Recording
            : MeetingEgressState.Starting;
        return new(state, info.Error, info.FileResults.FirstOrDefault()?.Size,
            info.EndedAt > info.StartedAt ? (info.EndedAt - info.StartedAt) / 1_000_000 : null);
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
            webhook.Room?.Name ?? webhook.EgressInfo?.RoomName,
            webhook.Participant?.Identity,
            webhook.Participant?.Sid,
            occurredAtUtc,
            webhook.EgressInfo?.EgressId,
            webhook.EgressInfo?.Status.ToString(),
            webhook.EgressInfo?.Error,
            webhook.EgressInfo?.FileResults.FirstOrDefault()?.Size,
            webhook.EgressInfo is null ? null : webhook.EgressInfo.EndedAt > webhook.EgressInfo.StartedAt
                ? (webhook.EgressInfo.EndedAt - webhook.EgressInfo.StartedAt) / 1_000_000
                : null);
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

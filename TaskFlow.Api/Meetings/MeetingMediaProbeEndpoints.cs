using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Infra.Meetings;

namespace TaskFlow.Api.Meetings;

public static class MeetingMediaProbeEndpoints
{
    private const string ProbeRoomName = "taskflow-meeting-phase0";

    public static IEndpointRouteBuilder MapMeetingMediaProbe(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/dev/meetings/livekit")
            .ExcludeFromDescription();

        group.MapPost("/token", CreateToken);
        group.MapPost("/webhook", ReceiveWebhook)
            .Accepts<string>("application/webhook+json");

        return endpoints;
    }

    private static IResult CreateToken(
        MeetingMediaProbeTokenRequest request,
        HttpContext context,
        IMeetingMediaProvider provider,
        Microsoft.Extensions.Options.IOptions<LiveKitSettings> options)
    {
        if (!IsLoopback(context.Connection.RemoteIpAddress))
        {
            return Results.NotFound();
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? "Phase 0 participant"
            : request.DisplayName.Trim();

        if (displayName.Length > 80)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.DisplayName)] = ["Display name cannot exceed 80 characters."]
            });
        }

        var participantIdentity = $"probe-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant()}";
        var token = provider.CreateJoinToken(new MeetingJoinTokenRequest(
            ProbeRoomName,
            participantIdentity,
            displayName,
            TimeSpan.FromMinutes(5),
            CanPublish: true,
            CanSubscribe: true,
            CanPublishData: false,
            IsRoomAdmin: false));

        return Results.Ok(new MeetingMediaProbeTokenResponse(
            options.Value.Url,
            ProbeRoomName,
            participantIdentity,
            token.Value,
            token.ExpiresAtUtc));
    }

    private static async Task<IResult> ReceiveWebhook(
        HttpRequest request,
        IMeetingMediaProvider provider,
        MeetingWebhookReplayGuard replayGuard,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        var authorization = request.Headers.Authorization.ToString();
        MeetingProviderWebhook webhook;
        try
        {
            webhook = provider.VerifyWebhook(rawBody, authorization);
        }
        catch
        {
            return Results.Unauthorized();
        }
        var isFirstDelivery = replayGuard.TryAccept(webhook.EventId);

        return Results.Ok(new
        {
            webhook.EventId,
            webhook.EventType,
            IsDuplicate = !isFirstDelivery
        });
    }

    private static bool IsLoopback(IPAddress? address) =>
        address is not null && IPAddress.IsLoopback(address);
}

public sealed record MeetingMediaProbeTokenRequest(string? DisplayName);

public sealed record MeetingMediaProbeTokenResponse(
    string WebSocketUrl,
    string RoomName,
    string ParticipantIdentity,
    string Token,
    DateTimeOffset ExpiresAtUtc);

public sealed class MeetingWebhookReplayGuard
{
    private readonly ConcurrentDictionary<string, byte> _eventIds = new(StringComparer.Ordinal);

    public bool TryAccept(string eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        return _eventIds.TryAdd(eventId, 0);
    }
}

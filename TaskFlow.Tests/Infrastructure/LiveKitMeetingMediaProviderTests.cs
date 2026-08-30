using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Meetings;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Infra.Meetings;

namespace TaskFlow.Tests.Infrastructure;

public sealed class LiveKitMeetingMediaProviderTests
{
    private const string ApiKey = "devkey";
    private const string ApiSecret = "secretsecretsecretsecretsecretsecret";

    [Fact]
    public void CreateJoinToken_IssuesShortLivedRoomScopedLeastPrivilegeToken()
    {
        var provider = CreateProvider();

        var result = provider.CreateJoinToken(new MeetingJoinTokenRequest(
            "taskflow-meeting-phase0",
            "probe-participant-1",
            "Phase zero",
            TimeSpan.FromMinutes(5),
            CanPublish: true,
            CanSubscribe: true,
            CanPublishData: false,
            IsRoomAdmin: false));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value);
        using var videoGrant = JsonDocument.Parse(token.Claims.Single(claim => claim.Type == "video").Value);

        Assert.Equal(ApiKey, token.Issuer);
        Assert.Equal("probe-participant-1", token.Subject);
        Assert.Equal("taskflow-meeting-phase0", videoGrant.RootElement.GetProperty("room").GetString());
        Assert.True(videoGrant.RootElement.GetProperty("roomJoin").GetBoolean());
        Assert.True(videoGrant.RootElement.GetProperty("canPublish").GetBoolean());
        Assert.True(videoGrant.RootElement.GetProperty("canSubscribe").GetBoolean());
        Assert.False(videoGrant.RootElement.GetProperty("canPublishData").GetBoolean());
        Assert.False(videoGrant.RootElement.TryGetProperty("roomAdmin", out var roomAdmin) && roomAdmin.GetBoolean());
        Assert.InRange(result.ExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(4), DateTimeOffset.UtcNow.AddMinutes(6));
    }

    [Fact]
    public void VerifyWebhook_RejectsTampering_AndReplayGuardDeduplicatesVerifiedEventId()
    {
        const string body = "{\"event\":\"room_started\",\"id\":\"EV_phase0_1\",\"createdAt\":1700000000,\"room\":{\"name\":\"taskflow-meeting-phase0\"}}";
        var provider = CreateProvider();
        var authorization = new AccessToken(ApiKey, ApiSecret)
            .WithSha256(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body))))
            .WithTtl(TimeSpan.FromMinutes(5))
            .ToJwt();

        var webhook = provider.VerifyWebhook(body, authorization);
        var replayGuard = new MeetingWebhookReplayGuard();

        Assert.Equal("EV_phase0_1", webhook.EventId);
        Assert.Equal("room_started", webhook.EventType);
        Assert.Equal("taskflow-meeting-phase0", webhook.RoomName);
        Assert.True(replayGuard.TryAccept(webhook.EventId));
        Assert.False(replayGuard.TryAccept(webhook.EventId));
        Assert.ThrowsAny<Exception>(() => provider.VerifyWebhook(body + " ", authorization));
    }

    private static LiveKitMeetingMediaProvider CreateProvider() =>
        new(Options.Create(new LiveKitSettings
        {
            Enabled = true,
            Url = "ws://localhost:7880",
            ApiKey = ApiKey,
            ApiSecret = ApiSecret,
            WebhookToleranceSeconds = 300
        }));
}

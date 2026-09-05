using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Infra.Meetings;
using TaskFlow.Infra.Storage;

namespace TaskFlow.Tests.Infrastructure;

public sealed class MeetingReadinessProbeTests
{
    private const string ApiKey = "devkey";
    private const string ApiSecret = "secretsecretsecretsecretsecretsecret";

    [Fact]
    public void Describe_ReportsReadyAndProvesTokenSigning_WhenFullyConfigured()
    {
        var report = CreateProbe().Describe();

        Assert.Equal(MeetingReadinessStatus.Ready, report.Status);
        Assert.Empty(report.Blockers);
        Assert.True(report.JoinTokenIssued);
        Assert.Null(report.JoinTokenFailure);
        Assert.Equal("wss", report.WebSocketScheme);
        Assert.Equal("media.example.com", report.WebSocketHost);
    }

    [Fact]
    public void Describe_NeverExposesTheApiSecret()
    {
        var report = CreateProbe().Describe();

        var serialized = System.Text.Json.JsonSerializer.Serialize(report);
        Assert.DoesNotContain(ApiSecret, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, serialized, StringComparison.Ordinal);
        Assert.True(report.ApiSecretConfigured);
        Assert.Equal(ApiSecret.Length, report.ApiSecretLength);
        Assert.Equal(8, report.ApiKeyFingerprint!.Length);
    }

    [Fact]
    public void Describe_NamesUnpropagatedConfiguration_WhenTheProcessLoadedNoLiveKitValues()
    {
        // Reproduces the 2026-09-02 deferral: the platform saved LiveKit__* but the
        // API service started without them, which members saw only as a join failure.
        var report = CreateProbe(liveKit: new LiveKitSettings { Enabled = false }).Describe();

        Assert.Equal(MeetingReadinessStatus.Disabled, report.Status);
        Assert.False(report.JoinTokenIssued);
        Assert.Contains(report.Blockers, blocker => blocker.Contains("not propagated", StringComparison.Ordinal));
    }

    [Fact]
    public void Describe_FlagsAnInsecureWebSocketUrl()
    {
        var report = CreateProbe(liveKit: new LiveKitSettings
        {
            Enabled = true,
            Url = "ws://media.example.com",
            ApiKey = ApiKey,
            ApiSecret = ApiSecret
        }).Describe();

        Assert.Equal(MeetingReadinessStatus.Misconfigured, report.Status);
        Assert.Contains(report.Blockers, blocker => blocker.Contains("browsers refuse it", StringComparison.Ordinal));
    }

    [Fact]
    public void Describe_FlagsRecordingWithoutStorage()
    {
        var report = CreateProbe(storage: new ObjectStorageSettings { Provider = "S3" }).Describe();

        Assert.Equal(MeetingReadinessStatus.Misconfigured, report.Status);
        Assert.False(report.RecordingStorageConfigured);
        Assert.Contains(report.Blockers, blocker => blocker.Contains("object storage", StringComparison.Ordinal));
    }

    private static MeetingReadinessProbe CreateProbe(
        LiveKitSettings? liveKit = null,
        MeetingSettings? meetings = null,
        ObjectStorageSettings? storage = null)
    {
        liveKit ??= new LiveKitSettings
        {
            Enabled = true,
            Url = "wss://media.example.com",
            ApiKey = ApiKey,
            ApiSecret = ApiSecret
        };

        meetings ??= new MeetingSettings { Enabled = true, GuestsEnabled = true, RecordingEnabled = true };

        storage ??= new ObjectStorageSettings
        {
            Provider = "S3",
            Endpoint = "https://storage.example.com",
            Bucket = "taskflow-meetings",
            AccessKey = "access",
            SecretKey = "secret"
        };

        var provider = new LiveKitMeetingMediaProvider(
            Options.Create(liveKit),
            Options.Create(storage));

        return new MeetingReadinessProbe(
            Options.Create(liveKit),
            Options.Create(meetings),
            Options.Create(storage),
            provider,
            new MeetingPolicy(Options.Create(meetings)));
    }
}

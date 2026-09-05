using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Infra.Storage;

namespace TaskFlow.Infra.Meetings;

/// <summary>
/// Describes the meeting media configuration this process actually loaded.
/// Secrets never leave: the API key is reported as a short irreversible
/// fingerprint so an operator can compare it against what they set, and the
/// API secret only ever as "configured" plus its length.
/// </summary>
public sealed class MeetingReadinessProbe : IMeetingReadinessProbe
{
    private const string ProbeRoomName = "taskflow-readiness-probe";

    private readonly LiveKitSettings _liveKit;
    private readonly MeetingSettings _meetings;
    private readonly ObjectStorageSettings _storage;
    private readonly IMeetingMediaProvider _provider;
    private readonly IMeetingPolicy _policy;

    public MeetingReadinessProbe(
        IOptions<LiveKitSettings> liveKit,
        IOptions<MeetingSettings> meetings,
        IOptions<ObjectStorageSettings> storage,
        IMeetingMediaProvider provider,
        IMeetingPolicy policy)
    {
        _liveKit = liveKit.Value;
        _meetings = meetings.Value;
        _storage = storage.Value;
        _provider = provider;
        _policy = policy;
    }

    public MeetingReadinessReport Describe()
    {
        var blockers = new List<string>();

        var apiKeyConfigured = !string.IsNullOrWhiteSpace(_liveKit.ApiKey);
        var apiSecretConfigured = !string.IsNullOrWhiteSpace(_liveKit.ApiSecret);
        var uri = Uri.TryCreate(_liveKit.Url, UriKind.Absolute, out var parsed) ? parsed : null;

        var recordingStorageConfigured =
            string.Equals(_storage.Provider, "Local", StringComparison.OrdinalIgnoreCase)
                ? !string.IsNullOrWhiteSpace(_storage.LocalPath)
                : !string.IsNullOrWhiteSpace(_storage.Endpoint)
                    && !string.IsNullOrWhiteSpace(_storage.Bucket)
                    && !string.IsNullOrWhiteSpace(_storage.AccessKey)
                    && !string.IsNullOrWhiteSpace(_storage.SecretKey);

        if (!_meetings.Enabled)
        {
            blockers.Add("Meetings:Enabled is false, so no meeting route is served.");
        }

        if (!_liveKit.Enabled)
        {
            blockers.Add(
                "LiveKit:Enabled is false in the running process. If the deployment platform shows it " +
                "as set, the value was saved but not propagated into this service.");
        }
        else
        {
            if (uri is null)
            {
                blockers.Add("LiveKit:Url is missing or not an absolute URL.");
            }
            else if (uri.Scheme is not ("ws" or "wss"))
            {
                blockers.Add($"LiveKit:Url uses '{uri.Scheme}'; the client requires ws:// or wss://.");
            }
            else if (uri.Scheme is "ws" && !uri.IsLoopback)
            {
                blockers.Add("LiveKit:Url is a non-loopback ws:// URL; browsers refuse it from an https:// page.");
            }

            if (!apiKeyConfigured)
            {
                blockers.Add("LiveKit:ApiKey is empty.");
            }

            if (!apiSecretConfigured)
            {
                blockers.Add("LiveKit:ApiSecret is empty.");
            }
        }

        if (_meetings.RecordingEnabled && !recordingStorageConfigured)
        {
            blockers.Add("Meetings:RecordingEnabled is true but object storage is not fully configured.");
        }

        var (joinTokenIssued, joinTokenFailure) = TryIssueJoinToken();
        if (_liveKit.Enabled && !joinTokenIssued)
        {
            blockers.Add("This process could not sign a join token, so no participant can enter a room.");
        }

        var status = (_meetings.Enabled, _liveKit.Enabled) switch
        {
            (false, _) or (_, false) => MeetingReadinessStatus.Disabled,
            _ when blockers.Count > 0 => MeetingReadinessStatus.Misconfigured,
            _ => MeetingReadinessStatus.Ready
        };

        return new MeetingReadinessReport(
            status,
            _meetings.Enabled,
            _meetings.GuestsEnabled,
            _meetings.RecordingEnabled,
            _liveKit.Enabled,
            uri?.Scheme,
            uri?.Host,
            apiKeyConfigured,
            apiKeyConfigured ? Fingerprint(_liveKit.ApiKey) : null,
            apiSecretConfigured,
            _liveKit.ApiSecret?.Length ?? 0,
            recordingStorageConfigured,
            joinTokenIssued,
            joinTokenFailure,
            blockers,
            _policy.Capacity);
    }

    private (bool Issued, string? Failure) TryIssueJoinToken()
    {
        if (!_liveKit.Enabled)
        {
            return (false, "LiveKit is disabled, so token signing was not attempted.");
        }

        try
        {
            var token = _provider.CreateJoinToken(new MeetingJoinTokenRequest(
                ProbeRoomName,
                $"readiness-{Guid.NewGuid():n}",
                "Readiness probe",
                TimeSpan.FromMinutes(1),
                CanPublish: false,
                CanSubscribe: false,
                CanPublishData: false,
                IsRoomAdmin: false));

            return (!string.IsNullOrWhiteSpace(token.Value), null);
        }
        catch (Exception exception)
        {
            // The message is operator-facing configuration detail, never a credential.
            return (false, exception.Message);
        }
    }

    private static string Fingerprint(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }
}

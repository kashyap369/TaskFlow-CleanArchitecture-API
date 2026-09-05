using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TaskFlow.Api.Middlewares;
using TaskFlow.Application.Common.Observability;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Meetings;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Enums.Meetings;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Meetings;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Infra.Meetings;

namespace TaskFlow.Tests.Application;

/// <summary>
/// Phase 7 / P7.4. Two things have to hold for meeting observability to be worth having, and both
/// are easy to break silently:
///
/// 1. <b>The signals exist and the alert rules read them correctly.</b> A threshold nobody exercises
///    is a threshold that fires at the wrong number the first time it matters.
/// 2. <b>No signal carries meeting content.</b> Telemetry is copied into logs, dashboards and
///    tickets read by people who were never in the meeting. A tag added carelessly — a room name, an
///    email, a title — leaks it everywhere at once, and nothing about the system would look wrong.
///
/// The redaction test below is the one that must never be deleted: it drives real handlers with
/// deliberately distinctive content and fails if any of it reaches a tag.
/// </summary>
[Collection(MeetingTelemetryCollection.Name)]
public sealed class MeetingObservabilityTests
{
    // ---- The rolling window ----------------------------------------------------------------------

    [Fact]
    public void ASignalIsCountedIntoTheMinuteItHappenedIn_AndAgesOutOfTheShorterWindows()
    {
        var now = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc);
        using var snapshot = new MeetingHealthSnapshot(() => now);

        // A unique refusal code, so this row cannot be confused with a real ceiling another test hit.
        Refuse("TEST_LIMIT_AGEING");
        now = now.AddMinutes(20);
        Refuse("TEST_LIMIT_AGEING");

        var report = snapshot.Describe(now);
        var row = Row(report, MeetingTelemetry.CapacityRefusals.Name, "TEST_LIMIT_AGEING");

        // The count from twenty minutes ago is still inside the hour and outside the shorter windows.
        Assert.Equal(1, row.LastFiveMinutes);
        Assert.Equal(1, row.LastFifteenMinutes);
        Assert.Equal(2, row.LastHour);
    }

    [Fact]
    public void ACountOlderThanTheWholeWindow_IsNotCountedAgainWhenItsBucketComesBackAround()
    {
        var now = new DateTime(2026, 9, 5, 11, 0, 0, DateTimeKind.Utc);
        using var snapshot = new MeetingHealthSnapshot(() => now);

        Refuse("TEST_LIMIT_WRAP");
        // Exactly one full ring later: the same bucket index, a different minute. A ring that did
        // not stamp its buckets would report this hour-old count as if it had just happened.
        now = now.AddMinutes(60);

        var report = snapshot.Describe(now);
        Assert.Null(Rows(report, MeetingTelemetry.CapacityRefusals.Name, "TEST_LIMIT_WRAP"));
    }

    [Fact]
    public void AFreshSnapshot_SaysItHasNotYetObservedAFullWindow()
    {
        var now = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
        using var snapshot = new MeetingHealthSnapshot(() => now);

        // An operator reading "no failures in the last hour" from a process started ten minutes ago
        // is being told something the data cannot support.
        Assert.False(snapshot.Describe(now.AddMinutes(10)).FullyObserved);
        Assert.True(snapshot.Describe(now.AddMinutes(60)).FullyObserved);
    }

    // ---- The alert rules -------------------------------------------------------------------------

    [Fact]
    public void EveryRuleIsReported_EvenWhenNothingIsWrong()
    {
        var now = new DateTime(2026, 9, 5, 13, 0, 0, DateTimeKind.Utc);
        using var snapshot = new MeetingHealthSnapshot(() => now);

        var report = snapshot.Describe(now);

        // A blank panel and a healthy panel must not look the same. Each rule states its own
        // threshold and window so the operator never has to guess what "quiet" was measured against.
        Assert.Equal(8, report.Alerts.Count);
        Assert.All(report.Alerts, alert =>
        {
            Assert.False(alert.Firing);
            Assert.True(alert.Threshold > 0);
            Assert.True(alert.WindowMinutes > 0);
            Assert.False(string.IsNullOrWhiteSpace(alert.Summary));
            Assert.Equal($"#{alert.Id}", alert.Runbook);
        });
    }

    [Fact]
    public void MediaCallFailures_FireOnlyOnceTheThresholdIsReached()
    {
        var now = new DateTime(2026, 9, 5, 14, 0, 0, DateTimeKind.Utc);
        using var snapshot = new MeetingHealthSnapshot(() => now);

        FailMediaCall(); FailMediaCall();
        Assert.False(Alert(snapshot.Describe(now), "media_calls_failing").Firing);

        FailMediaCall();
        var firing = Alert(snapshot.Describe(now), "media_calls_failing");
        Assert.True(firing.Firing);
        Assert.Equal(3, firing.Observed);
        Assert.Equal(MeetingAlertSeverity.Critical, firing.Severity);
    }

    [Fact]
    public void ASingleFailedRecordingStart_IsEnoughToFire()
    {
        var now = new DateTime(2026, 9, 5, 15, 0, 0, DateTimeKind.Utc);
        using var snapshot = new MeetingHealthSnapshot(() => now);

        // A host whose recording never started sees nothing wrong, and neither does the room. One
        // occurrence is the whole signal, so the threshold is one.
        MeetingTelemetry.Recordings.Add(1,
            new KeyValuePair<string, object?>(MeetingTelemetry.Tags.Event, MeetingRecordingEvents.StartFailed));

        Assert.True(Alert(snapshot.Describe(now), "recording_failures").Firing);
    }

    [Fact]
    public void AnIgnoredWebhook_DoesNotCountAsARejectedOne()
    {
        var now = new DateTime(2026, 9, 5, 16, 0, 0, DateTimeKind.Utc);
        using var snapshot = new MeetingHealthSnapshot(() => now);

        // Several environments can share one LiveKit server, so deliveries for rooms this
        // deployment does not own are normal traffic — counting them as rejections would page
        // someone every day and teach them to ignore the alert.
        for (var i = 0; i < 20; i++)
        {
            MeetingTelemetry.Webhooks.Add(1,
                new KeyValuePair<string, object?>(MeetingTelemetry.Tags.Outcome, MeetingWebhookOutcomes.Ignored));
        }

        Assert.False(Alert(snapshot.Describe(now), "webhooks_rejected").Firing);
    }

    // ---- The instrumented paths ------------------------------------------------------------------

    [Fact]
    public async Task RefusingAWriteForCapacity_CountsTheRefusalCode()
    {
        var now = new DateTime(2026, 9, 5, 17, 0, 0, DateTimeKind.Utc);
        using var snapshot = new MeetingHealthSnapshot(() => now);
        var meeting = DraftMeeting();
        SetId(meeting.AddRegisteredParticipant(21, MeetingAccessLevel.Participant), 2);
        var members = Substitute.For<IOrganizationMemberRepository>();
        members.IsActiveMemberAsync(7, 99, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new AddMeetingParticipantCommandHandler(MeetingsReturning(meeting), members,
            HostUser(), Substitute.For<IOrganizationPermissionChecker>(),
            new MeetingTestPolicy { MaxParticipantsPerMeeting = 2 }, Substitute.For<IUnitOfWork>());

        await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(
            new AddMeetingParticipantCommand(5, 99), CancellationToken.None));

        // The refusal code is the tag, so the alert can say which ceiling is being hit without any
        // meeting or organization appearing in the metric.
        var row = Row(snapshot.Describe(now), MeetingTelemetry.CapacityRefusals.Name,
            "MEETING_PARTICIPANT_LIMIT_REACHED");
        Assert.Equal(1, row.LastFiveMinutes);
    }

    [Fact]
    public async Task ARefusedJoin_IsCountedWithTheReasonItWasRefused()
    {
        var now = new DateTime(2026, 9, 5, 18, 0, 0, DateTimeKind.Utc);
        using var snapshot = new MeetingHealthSnapshot(() => now);
        var captured = new List<KeyValuePair<string, object?>>();
        using var listener = CaptureAllTags(captured);
        var handler = new GetMeetingJoinTokenCommandHandler(MeetingsReturning(DraftMeeting()),
            StrangerUser(), Substitute.For<IMeetingMediaProvider>(),
            Substitute.For<IMeetingRecordingRepository>());

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new GetMeetingJoinTokenCommand(5), CancellationToken.None));

        var row = Row(snapshot.Describe(now), MeetingTelemetry.JoinTokens.Name,
            MeetingTelemetry.Outcomes.Refused);
        Assert.Equal(1, row.LastFiveMinutes);

        // The reason rides along on the measurement without becoming the series key, so an operator
        // can separate "the media stack is down" from "the host revoked someone" — and so a new
        // refusal code cannot multiply the stored series.
        Assert.Contains(captured, tag => tag.Key == MeetingTelemetry.Tags.Reason &&
            (string?)tag.Value == "MEETING_ROOM_ACCESS_DENIED");
    }

    [Fact]
    public async Task AFailedGuestCode_IsCountedAgainstTheVerifyStage()
    {
        var now = new DateTime(2026, 9, 5, 19, 0, 0, DateTimeKind.Utc);
        using var snapshot = new MeetingHealthSnapshot(() => now);
        var guestAccess = Substitute.For<IMeetingGuestAccessRepository>();
        guestAccess.GetLinkByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MeetingAccessLink?)null);
        var handler = new VerifyMeetingGuestCodeCommandHandler(guestAccess,
            Substitute.For<IMeetingRepository>(), Substitute.For<IMeetingGuestCodeProtector>(),
            Substitute.For<IUserRepository>(), new MeetingTestPolicy(), Substitute.For<IUnitOfWork>());

        await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(
            new VerifyMeetingGuestCodeCommand(new string('t', 40), "intruder@example.test", "000000",
                "Guest", false, null, null, 60), CancellationToken.None));

        // This is the series the code-guessing alert reads. It must stay separate from the inspect
        // and request-code stages, which fail for ordinary reasons all day.
        var row = Row(snapshot.Describe(now), MeetingTelemetry.GuestVerifications.Name,
            $"{MeetingGuestStages.Verify}:{MeetingTelemetry.Outcomes.Failed}");
        Assert.Equal(1, row.LastFiveMinutes);
    }

    // ---- The privacy contract --------------------------------------------------------------------

    [Fact]
    public async Task NoMeetingSignalCarriesMeetingContent()
    {
        const string email = "kholodets@guest.example";
        const string room = "room-do-not-log-me";
        const string title = "Board pay review";
        var captured = new List<KeyValuePair<string, object?>>();
        using var listener = CaptureAllTags(captured);

        // Drive the paths that hold the sensitive values: a refused guest verification (email and
        // link token), and a refused join for a meeting whose room name and title are distinctive.
        var guestAccess = Substitute.For<IMeetingGuestAccessRepository>();
        guestAccess.GetLinkByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MeetingAccessLink?)null);
        await Assert.ThrowsAsync<BusinessException>(() =>
            new VerifyMeetingGuestCodeCommandHandler(guestAccess, Substitute.For<IMeetingRepository>(),
                    Substitute.For<IMeetingGuestCodeProtector>(), Substitute.For<IUserRepository>(),
                    new MeetingTestPolicy(), Substitute.For<IUnitOfWork>())
                .Handle(new VerifyMeetingGuestCodeCommand("link-token-do-not-log-me", email, "000000",
                    "Guest", false, null, null, 60), CancellationToken.None));

        var meeting = new Meeting(7, 11, title, null, null, null, "UTC", room,
            true, true, true, true, true, false, 90);
        SetId(meeting, 5); SetId(meeting.Participants.Single(), 1);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new GetMeetingJoinTokenCommandHandler(MeetingsReturning(meeting), StrangerUser(),
                    Substitute.For<IMeetingMediaProvider>(), Substitute.For<IMeetingRecordingRepository>())
                .Handle(new GetMeetingJoinTokenCommand(5), CancellationToken.None));

        Assert.NotEmpty(captured);
        var forbidden = new[] { email, "kholodets", room, title, "link-token-do-not-log-me", "@guest.example" };
        foreach (var tag in captured)
        {
            var value = tag.Value?.ToString() ?? string.Empty;
            foreach (var secret in forbidden)
            {
                Assert.DoesNotContain(secret, value, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task TheEdgeMiddlewareTagsTheRouteTemplate_NeverTheConcretePath()
    {
        var captured = new List<KeyValuePair<string, object?>>();
        using var listener = CaptureAllTags(captured);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/meeting/4711/messages";
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
            RoutePatternFactory.Parse("api/meeting/{meetingId:int}/messages"), 0,
            EndpointMetadataCollection.Empty, "messages"));
        var middleware = new MeetingObservabilityMiddleware(_ => Task.CompletedTask,
            NullLogger<MeetingObservabilityMiddleware>.Instance,
            new StaticOptionsMonitor<MeetingSettings>(new MeetingSettings()));

        await middleware.InvokeAsync(context);

        var routes = captured.Where(tag => tag.Key == MeetingTelemetry.Tags.Route)
            .Select(tag => tag.Value?.ToString()).ToList();
        Assert.NotEmpty(routes);
        // The template, so meeting 4711 does not get its own metric series and its id never reaches
        // a metrics backend or a log aggregator.
        Assert.All(routes, route =>
        {
            Assert.Equal("api/meeting/{meetingId:int}/messages", route);
            Assert.DoesNotContain("4711", route);
        });
    }

    [Fact]
    public async Task AnUnmatchedMeetingPath_IsBucketed_SoACallerCannotMintMetricSeries()
    {
        var captured = new List<KeyValuePair<string, object?>>();
        using var listener = CaptureAllTags(captured);
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/meeting/" + new string('z', 200);
        var middleware = new MeetingObservabilityMiddleware(_ => Task.CompletedTask,
            NullLogger<MeetingObservabilityMiddleware>.Instance,
            new StaticOptionsMonitor<MeetingSettings>(new MeetingSettings()));

        await middleware.InvokeAsync(context);

        Assert.Contains(captured, tag =>
            tag.Key == MeetingTelemetry.Tags.Route && (string?)tag.Value == "unmatched");
    }

    [Fact]
    public void RefusalsAndThrottlingAreTheirOwnStatusClass()
    {
        // 401/403 is the abuse trail and must not be diluted by ordinary validation failures, which
        // is the whole reason the rules can alert on refusals without alerting on bad input.
        Assert.Equal(MeetingTelemetry.StatusClasses.Denied, MeetingTelemetry.ClassifyStatus(401));
        Assert.Equal(MeetingTelemetry.StatusClasses.Denied, MeetingTelemetry.ClassifyStatus(403));
        Assert.Equal(MeetingTelemetry.StatusClasses.ClientError, MeetingTelemetry.ClassifyStatus(400));
        Assert.Equal(MeetingTelemetry.StatusClasses.Throttled, MeetingTelemetry.ClassifyStatus(429));
        Assert.Equal(MeetingTelemetry.StatusClasses.ServerError, MeetingTelemetry.ClassifyStatus(503));
        Assert.Equal(MeetingTelemetry.StatusClasses.Ok, MeetingTelemetry.ClassifyStatus(204));
    }

    // ---- Shared arrangement ----------------------------------------------------------------------

    private static void Refuse(string code) =>
        MeetingTelemetry.CapacityRefusals.Add(1,
            new KeyValuePair<string, object?>(MeetingTelemetry.Tags.Limit, code));

    private static void FailMediaCall() =>
        MeetingTelemetry.MediaCalls.Add(1,
            new KeyValuePair<string, object?>(MeetingTelemetry.Tags.Operation, "unit_probe"),
            new KeyValuePair<string, object?>(MeetingTelemetry.Tags.Outcome, MeetingTelemetry.Outcomes.Failed));

    private static MeetingHealthAlert Alert(MeetingHealthReport report, string id) =>
        report.Alerts.Single(alert => alert.Id == id);

    private static MeetingHealthSeries Row(MeetingHealthReport report, string signal, string key) =>
        Rows(report, signal, key) ?? throw new Xunit.Sdk.XunitException(
            $"No series for {signal}|{key}. Present: {string.Join(", ", report.Series.Select(r => $"{r.Signal}|{r.Key}"))}");

    private static MeetingHealthSeries? Rows(MeetingHealthReport report, string signal, string key) =>
        report.Series.FirstOrDefault(row => row.Signal == signal && row.Key == key);

    private static MeterListener CaptureAllTags(List<KeyValuePair<string, object?>> sink)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, self) =>
            {
                if (instrument.Meter.Name == MeetingTelemetry.SourceName) self.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => Collect(sink, tags));
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) => Collect(sink, tags));
        listener.Start();
        return listener;
    }

    private static void Collect(List<KeyValuePair<string, object?>> sink,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            lock (sink) sink.Add(tag);
        }
    }

    private static Meeting DraftMeeting()
    {
        var meeting = new Meeting(7, 11, "Review", null, null, null, "UTC", "meeting-room",
            true, true, true, true, true, false, 90);
        SetId(meeting, 5); SetId(meeting.Participants.Single(), 1);
        return meeting;
    }

    private static ICurrentUserService HostUser()
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(11); user.Email.Returns("host@example.test");
        return user;
    }

    private static ICurrentUserService StrangerUser()
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(404); user.Email.Returns("stranger@example.test");
        return user;
    }

    private static IMeetingRepository MeetingsReturning(Meeting meeting)
    {
        var meetings = Substitute.For<IMeetingRepository>();
        meetings.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(meeting);
        return meetings;
    }

    private static void SetId(object entity, int id) => entity.GetType().GetProperty("Id")!.SetValue(entity, id);

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}

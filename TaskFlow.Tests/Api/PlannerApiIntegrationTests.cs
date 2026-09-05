using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Entities.WorkManagement.Projects;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Enums.WorkManagement;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Infra.Persistence.Context;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Application.Contracts.Email;
using TaskFlow.Application.Contracts.Meetings;
using TaskEntity = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;

namespace TaskFlow.Tests.Api;

public sealed class PlannerApiIntegrationTests : IClassFixture<PlannerApiFixture>
{
    private readonly PlannerApiFixture _fixture;

    public PlannerApiIntegrationTests(PlannerApiFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Phase 7 / P7.4. Drives real meeting traffic over HTTP and then reads the operator's health
    /// report, which is the only thing that proves the whole chain rather than its parts: the
    /// middleware is registered in the pipeline, the snapshot is a live singleton listening before
    /// the first request, the query is wired, and the route is AdminOnly.
    ///
    /// It also asserts the privacy contract where it actually matters — on the response an operator
    /// will screenshot into a ticket. A unit test can prove a handler tags nothing sensitive; only
    /// this can prove the assembled report does not.
    /// </summary>
    [Fact]
    public async Task MeetingHealth_IsAdminOnly_ReportsEveryRule_AndNamesNoMeeting()
    {
        using var admin = _fixture.CreateClient(1, SystemRoleNames.Admin);
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);
        using var otherOwner = _fixture.CreateClient(_fixture.OtherOrganizationOwnerUserId);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await owner.GetAsync("api/admin/meetings/health")).StatusCode);

        const string title = "Telemetry probe do-not-log";
        var created = await owner.PostAsJsonAsync("api/meeting", new
        {
            organizationId = _fixture.CapacityOrganizationId,
            title,
            description = (string?)null,
            timeZone = "UTC",
            guestsAllowed = false,
            retentionDays = 90,
            participantUserIds = Array.Empty<int>()
        });
        Assert.True(created.IsSuccessStatusCode,
            $"Meeting create failed: {created.StatusCode} {await created.Content.ReadAsStringAsync()}");
        var meetingId = await created.Content.ReadFromJsonAsync<int>();

        // One success and one refusal, so both status classes have to appear in the window.
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"api/meeting/{meetingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await otherOwner.GetAsync($"api/meeting/{meetingId}")).StatusCode);

        var body = await admin.GetStringAsync("api/admin/meetings/health");
        using var report = JsonDocument.Parse(body);
        var root = report.RootElement;

        // Every rule is reported whether or not it is firing: a quiet system and a broken endpoint
        // must not produce the same empty panel.
        var alerts = root.GetProperty("alerts").EnumerateArray().ToList();
        Assert.Equal(8, alerts.Count);
        Assert.All(alerts, alert =>
        {
            Assert.True(alert.GetProperty("threshold").GetInt64() > 0);
            Assert.False(string.IsNullOrWhiteSpace(alert.GetProperty("runbook").GetString()));
        });

        var series = root.GetProperty("series").EnumerateArray()
            .Select(row => (Signal: row.GetProperty("signal").GetString(), Key: row.GetProperty("key").GetString()))
            .ToList();
        Assert.Contains(series, row => row.Signal == "taskflow.meetings.requests" && row.Key == "ok");
        Assert.Contains(series, row => row.Signal == "taskflow.meetings.requests" && row.Key == "denied");

        // The report is counts and rule outcomes. Nothing that identifies the meeting whose traffic
        // produced them may reach it — not the title, and not the id that would give this meeting
        // its own metric series.
        Assert.DoesNotContain(title, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("do-not-log", body, StringComparison.OrdinalIgnoreCase);
        Assert.All(series, row => Assert.DoesNotContain(meetingId.ToString(), row.Key!));

        // Latency is reported for the whole window rather than per route, for the same reason.
        Assert.True(root.GetProperty("latency").GetProperty("requests").GetInt64() > 0);
    }

    [Fact]
    public async Task Meetings_EnforceLifecycleParticipantReadsAndCrossOrganizationIsolation()
    {
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);
        using var participant = _fixture.CreateClient(_fixture.CapacityMemberUserId);
        using var otherOwner = _fixture.CreateClient(_fixture.OtherOrganizationOwnerUserId);
        var created = await owner.PostAsJsonAsync("api/meeting", new
        {
            organizationId = _fixture.CapacityOrganizationId,
            title = "Quarterly planning",
            description = "Phase 1 integration",
            scheduledStartUtc = "2026-10-01T09:00:00Z",
            scheduledEndUtc = "2026-10-01T10:00:00Z",
            timeZone = "UTC",
            guestsAllowed = true,
            retentionDays = 90,
            participantUserIds = new[] { _fixture.CapacityMemberUserId }
        });
        Assert.True(created.IsSuccessStatusCode,
            $"Meeting create failed: {created.StatusCode} {await created.Content.ReadAsStringAsync()}");
        var meetingId = await created.Content.ReadFromJsonAsync<int>();

        Assert.Equal(HttpStatusCode.OK, (await participant.GetAsync($"api/meeting/{meetingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherOwner.GetAsync($"api/meeting/{meetingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherOwner.GetAsync(
            $"api/meeting/organization/{_fixture.CapacityOrganizationId}?fromUtc=2026-01-01T00:00:00Z&toUtc=2026-12-31T00:00:00Z")).StatusCode);

        var linkResponse = await owner.PostAsJsonAsync($"api/meeting/{meetingId}/access-links", new
        {
            meetingId,
            mode = 2,
            lockedEmail = (string?)null,
            defaultAccessLevel = 3,
            badgeDefinitionId = (int?)null,
            expiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            maximumUses = 5
        });
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);
        using var linkDocument = JsonDocument.Parse(await linkResponse.Content.ReadAsStringAsync());
        var rawToken = linkDocument.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(rawToken));
        var safeLinks = await owner.GetStringAsync($"api/meeting/{meetingId}/access-links");
        Assert.DoesNotContain(rawToken!, safeLinks);
        Assert.DoesNotContain("tokenHash", safeLinks, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await otherOwner.GetAsync($"api/meeting/{meetingId}/access-links")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.PostAsync($"api/meeting/{meetingId}/start", null)).StatusCode);
        var endResponse = await owner.PostAsync($"api/meeting/{meetingId}/end", null);
        Assert.True(endResponse.StatusCode == HttpStatusCode.NoContent,
            $"Meeting end failed: {endResponse.StatusCode} {await endResponse.Content.ReadAsStringAsync()}");
        var detail = await owner.GetFromJsonAsync<MeetingDetailResponse>($"api/meeting/{meetingId}");
        Assert.NotNull(detail); Assert.Equal(4, detail.Status); Assert.NotNull(detail.ActualStartUtc);
        Assert.NotNull(detail.ActualEndUtc); Assert.Equal(2, detail.Participants.Count);
    }

    [Fact]
    public async Task MeetingGuests_VerifyEmail_GetScopedSession_AndRespectRevokeAndUseLimits()
    {
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);
        using var guest = _fixture.CreateAnonymousClient();
        var created = await owner.PostAsJsonAsync("api/meeting", new
        {
            organizationId = _fixture.CapacityOrganizationId, title = "Guest security review",
            timeZone = "UTC", guestsAllowed = true, retentionDays = 90
        });
        var meetingId = await created.Content.ReadFromJsonAsync<int>();
        var linkResponse = await owner.PostAsJsonAsync($"api/meeting/{meetingId}/access-links", new
        {
            meetingId, mode = 2, lockedEmail = (string?)null, defaultAccessLevel = 3,
            badgeDefinitionId = (int?)null, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1), maximumUses = 1
        });
        using var linkJson = JsonDocument.Parse(await linkResponse.Content.ReadAsStringAsync());
        var token = linkJson.RootElement.GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent, (await guest.PostAsJsonAsync("api/meeting/guest/access/request-code", new { token, email = "guest@example.test" })).StatusCode);
        var code = _fixture.Email.LastCode;
        Assert.Matches("^[0-9]{6}$", code);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("api/meeting/guest/access/verify-code", new
        { token, email = "guest@example.test", code = "000000", displayName = "Guest Person", bindRegisteredAccount = false })).StatusCode);
        var verifiedResponse = await guest.PostAsJsonAsync("api/meeting/guest/access/verify-code", new
        { token, email = "guest@example.test", code, displayName = "Guest Person", bindRegisteredAccount = false });
        Assert.Equal(HttpStatusCode.OK, verifiedResponse.StatusCode);
        using var verified = JsonDocument.Parse(await verifiedResponse.Content.ReadAsStringAsync());
        var sessionToken = verified.RootElement.GetProperty("sessionToken").GetString()!;
        guest.DefaultRequestHeaders.Add("X-Meeting-Guest-Session", sessionToken);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("api/meeting/guest/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync($"api/meeting/{meetingId}")).StatusCode);

        await guest.PostAsJsonAsync("api/meeting/guest/access/request-code", new { token, email = "guest@example.test" });
        Assert.NotEqual(HttpStatusCode.NoContent, (await guest.PostAsJsonAsync("api/meeting/guest/access/request-code", new { token, email = "other@example.test" })).StatusCode);
        var detail = await owner.GetFromJsonAsync<MeetingDetailResponse>($"api/meeting/{meetingId}");
        var guestParticipant = Assert.Single(detail!.Participants, participant => participant.Email == "GUEST@EXAMPLE.TEST");
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"api/meeting/{meetingId}/participants/{guestParticipant.Id}", new
        { meetingId, participantId = guestParticipant.Id, accessLevel = 3, badgeDefinitionId = (int?)null, state = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("api/meeting/guest/session")).StatusCode);

        using var registered = _fixture.CreateClient(_fixture.CapacityMemberUserId);
        var registeredEmail = $"planner-{_fixture.CapacityMemberUserId}@example.test";
        var privateLinkResponse = await owner.PostAsJsonAsync($"api/meeting/{meetingId}/access-links", new
        {
            meetingId, mode = 1, lockedEmail = registeredEmail, defaultAccessLevel = 4,
            badgeDefinitionId = (int?)null, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1), maximumUses = 1
        });
        using var privateLinkJson = JsonDocument.Parse(await privateLinkResponse.Content.ReadAsStringAsync());
        var privateToken = privateLinkJson.RootElement.GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.BadRequest, (await registered.PostAsJsonAsync("api/meeting/guest/access/request-code", new
        { token = privateToken, email = "wrong@example.test" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await registered.PostAsJsonAsync("api/meeting/guest/access/request-code", new
        { token = privateToken, email = registeredEmail })).StatusCode);
        var registeredCode = _fixture.Email.LastCode;
        Assert.Equal(HttpStatusCode.OK, (await registered.PostAsJsonAsync("api/meeting/guest/access/verify-code", new
        { token = privateToken, email = registeredEmail, code = registeredCode, displayName = "Registered Guest", bindRegisteredAccount = true })).StatusCode);
        detail = await owner.GetFromJsonAsync<MeetingDetailResponse>($"api/meeting/{meetingId}");
        Assert.Contains(detail!.Participants, participant => participant.UserId == _fixture.CapacityMemberUserId && participant.IsGuest && participant.Email == registeredEmail.ToUpperInvariant());

        var unboundLinkResponse = await owner.PostAsJsonAsync($"api/meeting/{meetingId}/access-links", new
        {
            meetingId, mode = 2, lockedEmail = (string?)null, defaultAccessLevel = 4,
            badgeDefinitionId = (int?)null, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1), maximumUses = 1
        });
        using var unboundLinkJson = JsonDocument.Parse(await unboundLinkResponse.Content.ReadAsStringAsync());
        var unboundToken = unboundLinkJson.RootElement.GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent, (await registered.PostAsJsonAsync("api/meeting/guest/access/request-code", new
        { token = unboundToken, email = "unbound@example.test" })).StatusCode);
        var unboundCode = _fixture.Email.LastCode;
        Assert.Equal(HttpStatusCode.OK, (await registered.PostAsJsonAsync("api/meeting/guest/access/verify-code", new
        { token = unboundToken, email = "unbound@example.test", code = unboundCode, displayName = "Unbound Guest", bindRegisteredAccount = false })).StatusCode);
    }

    [Fact]
    public async Task MeetingRoomTokens_RequireAssignedMemberOrAdmittedGuest()
    {
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);
        using var member = _fixture.CreateClient(_fixture.CapacityMemberUserId);
        using var outsider = _fixture.CreateClient(_fixture.OtherOrganizationOwnerUserId);
        using var guest = _fixture.CreateAnonymousClient();

        var created = await owner.PostAsJsonAsync("api/meeting", new
        {
            organizationId = _fixture.CapacityOrganizationId,
            title = "Room authorization review",
            timeZone = "UTC",
            guestsAllowed = true,
            retentionDays = 90,
            participantUserIds = new[] { _fixture.CapacityMemberUserId }
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var meetingId = await created.Content.ReadFromJsonAsync<int>();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"api/meeting/{meetingId}/start", null)).StatusCode);

        var memberToken = await member.PostAsync($"api/meeting/{meetingId}/join-token", null);
        Assert.Equal(HttpStatusCode.OK, memberToken.StatusCode);
        using (var token = JsonDocument.Parse(await memberToken.Content.ReadAsStringAsync()))
        {
            Assert.Equal(meetingId, token.RootElement.GetProperty("meetingId").GetInt32());
            Assert.True(token.RootElement.GetProperty("participantId").GetInt32() > 0);
            Assert.True(token.RootElement.GetProperty("canPublish").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(token.RootElement.GetProperty("token").GetString()));
        }
        Assert.Equal(HttpStatusCode.Forbidden,
            (await outsider.PostAsync($"api/meeting/{meetingId}/join-token", null)).StatusCode);

        var link = await owner.PostAsJsonAsync($"api/meeting/{meetingId}/access-links", new
        {
            meetingId,
            mode = 2,
            lockedEmail = (string?)null,
            defaultAccessLevel = 3,
            badgeDefinitionId = (int?)null,
            expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            maximumUses = 1
        });
        using var linkJson = JsonDocument.Parse(await link.Content.ReadAsStringAsync());
        var rawToken = linkJson.RootElement.GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent, (await guest.PostAsJsonAsync("api/meeting/guest/access/request-code",
            new { token = rawToken, email = "room-guest@example.test" })).StatusCode);
        var verify = await guest.PostAsJsonAsync("api/meeting/guest/access/verify-code", new
        {
            token = rawToken, email = "room-guest@example.test", code = _fixture.Email.LastCode,
            displayName = "Room guest", bindRegisteredAccount = false
        });
        using var verified = JsonDocument.Parse(await verify.Content.ReadAsStringAsync());
        var sessionToken = verified.RootElement.GetProperty("sessionToken").GetString()!;
        guest.DefaultRequestHeaders.Add("X-Meeting-Guest-Session", sessionToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PostAsync("api/meeting/guest/join-token", null)).StatusCode);
        var detail = await owner.GetFromJsonAsync<MeetingDetailResponse>($"api/meeting/{meetingId}");
        var guestParticipant = Assert.Single(detail!.Participants, participant => participant.Email == "ROOM-GUEST@EXAMPLE.TEST");
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"api/meeting/{meetingId}/participants/{guestParticipant.Id}", new
        {
            meetingId, participantId = guestParticipant.Id, accessLevel = 3, badgeDefinitionId = (int?)null, state = 2
        })).StatusCode);

        var guestToken = await guest.PostAsync("api/meeting/guest/join-token", null);
        Assert.Equal(HttpStatusCode.OK, guestToken.StatusCode);
        using var guestTokenJson = JsonDocument.Parse(await guestToken.Content.ReadAsStringAsync());
        Assert.Equal(meetingId, guestTokenJson.RootElement.GetProperty("meetingId").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(guestTokenJson.RootElement.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task MeetingRoom_ModerationAndSignedAttendanceAreServerAuthorizedAndDurable()
    {
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);
        using var member = _fixture.CreateClient(_fixture.CapacityMemberUserId);
        var created = await owner.PostAsJsonAsync("api/meeting", new
        {
            organizationId = _fixture.CapacityOrganizationId, title = "Moderation integration",
            timeZone = "UTC", guestsAllowed = true, retentionDays = 90,
            participantUserIds = new[] { _fixture.CapacityMemberUserId }
        });
        var meetingId = await created.Content.ReadFromJsonAsync<int>();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"api/meeting/{meetingId}/start", null)).StatusCode);
        var detail = await owner.GetFromJsonAsync<MeetingDetailResponse>($"api/meeting/{meetingId}");
        var host = Assert.Single(detail!.Participants, x => x.UserId == _fixture.CapacityOwnerUserId);
        var target = Assert.Single(detail.Participants, x => x.UserId == _fixture.CapacityMemberUserId);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.PostAsync($"api/meeting/{meetingId}/room/participants/{host.Id}/remove", null)).StatusCode);

        using var tokenResponse = JsonDocument.Parse(await (await owner.PostAsync(
            $"api/meeting/{meetingId}/join-token", null)).Content.ReadAsStringAsync());
        var identity = tokenResponse.RootElement.GetProperty("participantIdentity").GetString()!;
        var before = await _fixture.ReadMeetingEvidenceAsync(meetingId);
        owner.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(TestMeetingMediaProvider.Authorization);
        var joinedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var webhook = new
        {
            eventId = $"joined-{meetingId}", eventType = "participant_joined", roomName = before.RoomName,
            participantIdentity = identity, participantSid = "PA_integration", occurredAtUtc = joinedAt
        };
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("api/meeting/webhooks/livekit", webhook)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("api/meeting/webhooks/livekit", webhook)).StatusCode);
        var attendance = await _fixture.ReadMeetingEvidenceAsync(meetingId);
        Assert.Equal(1, attendance.AttendanceCount); Assert.Equal(1, attendance.ReceiptCount);

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.PostAsync($"api/meeting/{meetingId}/room/participants/{target.Id}/remove", null)).StatusCode);
        Assert.Contains($"m{meetingId}-p{target.Id}-", _fixture.Media.RemovedPrefixes);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await member.PostAsync($"api/meeting/{meetingId}/join-token", null)).StatusCode);
    }

    [Fact]
    public async Task MeetingCollaboration_ReconcilesRetriesConflictsFilesAndEndedArchive()
    {
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);
        using var member = _fixture.CreateClient(_fixture.CapacityMemberUserId);
        using var outsider = _fixture.CreateClient(_fixture.OtherOrganizationOwnerUserId);
        var created = await owner.PostAsJsonAsync("api/meeting", new
        {
            organizationId = _fixture.CapacityOrganizationId, title = "Durable collaboration",
            timeZone = "UTC", guestsAllowed = true, retentionDays = 90,
            participantUserIds = new[] { _fixture.CapacityMemberUserId }
        });
        var meetingId = await created.Content.ReadFromJsonAsync<int>();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"api/meeting/{meetingId}/start", null)).StatusCode);

        var clientMessageId = Guid.NewGuid();
        var first = await member.PostAsJsonAsync($"api/meeting/{meetingId}/messages", new { clientMessageId, body = "Persist before broadcast" });
        var retry = await member.PostAsJsonAsync($"api/meeting/{meetingId}/messages", new { clientMessageId, body = "Persist before broadcast" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var retryJson = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.Equal(firstJson.RootElement.GetProperty("id").GetInt32(), retryJson.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"api/meeting/{meetingId}/messages")).StatusCode);

        var note = await member.PutAsJsonAsync($"api/meeting/{meetingId}/note", new { content = "Decision one", expectedVersion = 0 });
        Assert.Equal(HttpStatusCode.OK, note.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"api/meeting/{meetingId}/note",
            new { content = "Silent overwrite", expectedVersion = 0 })).StatusCode);

        using var upload = new MultipartFormDataContent();
        var file = new ByteArrayContent("safe meeting attachment"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain"); upload.Add(file, "file", "decisions.txt");
        var uploaded = await member.PostAsync($"api/meeting/{meetingId}/assets", upload);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        using var uploadedJson = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync());
        var assetId = uploadedJson.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"api/meeting/{meetingId}/assets/{assetId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"api/meeting/{meetingId}/end", null)).StatusCode);
        var archive = await member.GetAsync($"api/meeting/{meetingId}/archive");
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        var archiveBody = await archive.Content.ReadAsStringAsync();
        Assert.Contains("Persist before broadcast", archiveBody); Assert.Contains("Decision one", archiveBody); Assert.Contains("decisions.txt", archiveBody);
        Assert.Equal(HttpStatusCode.BadRequest, (await member.PostAsJsonAsync($"api/meeting/{meetingId}/messages",
            new { clientMessageId = Guid.NewGuid(), body = "too late" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"api/meeting/{meetingId}/assets/{assetId}")).StatusCode);
    }

    /// <summary>
    /// Phase 7 / P7.3. The declared ceilings have to hold over the real HTTP and database path, not
    /// only in a handler test: the count that a ceiling compares against is a query, and a query is
    /// exactly what a unit test replaces with a stub.
    /// </summary>
    [Fact]
    public async Task Meetings_EnforceDeclaredMessageAndFileCeilings_OverTheRealPath()
    {
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);
        var created = await owner.PostAsJsonAsync("api/meeting", new
        {
            organizationId = _fixture.CapacityOrganizationId, title = "Declared capacity",
            timeZone = "UTC", retentionDays = 90
        });
        var meetingId = await created.Content.ReadFromJsonAsync<int>();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"api/meeting/{meetingId}/start", null)).StatusCode);

        for (var index = 0; index < 10; index++)
        {
            var accepted = await owner.PostAsJsonAsync($"api/meeting/{meetingId}/messages",
                new { clientMessageId = Guid.NewGuid(), body = $"Message {index}" });
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }
        var refused = await owner.PostAsJsonAsync($"api/meeting/{meetingId}/messages",
            new { clientMessageId = Guid.NewGuid(), body = "One past the ceiling" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var refusedBody = await refused.Content.ReadAsStringAsync();
        Assert.Contains("MEETING_MESSAGE_LIMIT_REACHED", refusedBody);
        // The person who hit it needs the number, not just a refusal.
        Assert.Contains("10", refusedBody);

        for (var index = 0; index < 2; index++)
            Assert.Equal(HttpStatusCode.OK, (await UploadAsync(owner, meetingId, $"notes-{index}.txt")).StatusCode);
        var refusedUpload = await UploadAsync(owner, meetingId, "one-too-many.txt");
        Assert.Equal(HttpStatusCode.BadRequest, refusedUpload.StatusCode);
        Assert.Contains("MEETING_FILE_COUNT_LIMIT_REACHED", await refusedUpload.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Phase 7 / P7.3 concurrency. Duplicate suppression on chat reads before it writes, so it only
    /// catches a retry that arrives after the first send committed. A client retrying on a slow
    /// connection has both in flight at once: both read nothing, both write, and the unique index
    /// refuses one. The refused one must still report the message that landed — a 500 there tells a
    /// user their message was lost while it is sitting in the room.
    /// </summary>
    [Fact]
    public async Task MeetingChat_UnderSimultaneousRetriesOfOneMessage_StoresItOnceAndReportsItToEveryCaller()
    {
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);
        var created = await owner.PostAsJsonAsync("api/meeting", new
        {
            organizationId = _fixture.CapacityOrganizationId, title = "Concurrent retries",
            timeZone = "UTC", retentionDays = 90
        });
        var meetingId = await created.Content.ReadFromJsonAsync<int>();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"api/meeting/{meetingId}/start", null)).StatusCode);

        var clientMessageId = Guid.NewGuid();
        var payload = new { clientMessageId, body = "Sent once, retried six times" };
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            owner.PostAsJsonAsync($"api/meeting/{meetingId}/messages", payload)));

        var ids = new List<int>();
        foreach (var response in responses)
        {
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"Concurrent retry failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            ids.Add(json.RootElement.GetProperty("id").GetInt32());
            response.Dispose();
        }
        // Every caller was told about the same message, and only one exists.
        Assert.Single(ids.Distinct());
        var stored = await _fixture.WithDbContextAsync(context =>
            context.MeetingMessages.CountAsync(x => x.MeetingId == meetingId && x.ClientMessageId == clientMessageId));
        Assert.Equal(1, stored);
    }

    /// <summary>
    /// Phase 7 / P7.3, threat model A-07. Guest sessions and OTP challenges are access records, so
    /// meeting retention never reached them and the tables grew for the life of the deployment.
    /// </summary>
    [Fact]
    public async Task MeetingRetention_PurgesSpentGuestAccessRecords_AndKeepsLiveOnes()
    {
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);
        var created = await owner.PostAsJsonAsync("api/meeting", new
        {
            organizationId = _fixture.CapacityOrganizationId, title = "Guest record retention",
            timeZone = "UTC", guestsAllowed = true, retentionDays = 90
        });
        var meetingId = await created.Content.ReadFromJsonAsync<int>();
        var linkResponse = await owner.PostAsJsonAsync($"api/meeting/{meetingId}/access-links", new
        {
            meetingId, mode = 2, lockedEmail = (string?)null, defaultAccessLevel = 3,
            badgeDefinitionId = (int?)null, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1), maximumUses = (int?)null
        });
        using var linkJson = JsonDocument.Parse(await linkResponse.Content.ReadAsStringAsync());
        var linkId = linkJson.RootElement.GetProperty("id").GetInt32();

        var participantId = await _fixture.WithDbContextAsync(async context =>
        {
            var meeting = await context.Meetings.Include(x => x.Participants).SingleAsync(x => x.Id == meetingId);
            var guest = meeting.AddGuestParticipant("stale@example.test", "Stale Guest",
                TaskFlow.Domain.Enums.Meetings.MeetingAccessLevel.Participant, null);
            await context.SaveChangesAsync();
            var old = DateTime.UtcNow.AddDays(-30);
            context.MeetingGuestSessions.Add(new MeetingGuestSession(meetingId, guest.Id, new string('1', 64), old, linkId));
            context.MeetingGuestSessions.Add(new MeetingGuestSession(meetingId, guest.Id, new string('2', 64), DateTime.UtcNow.AddHours(1), linkId));
            context.MeetingGuestChallenges.Add(new MeetingGuestChallenge(meetingId, linkId, "stale@example.test",
                new string('3', 64), old, old, 5));
            context.MeetingGuestChallenges.Add(new MeetingGuestChallenge(meetingId, linkId, "fresh@example.test",
                new string('4', 64), DateTime.UtcNow.AddMinutes(10), DateTime.UtcNow, 5));
            context.MeetingGuestDecisions.Add(new MeetingGuestDecision(meetingId, guest.Id,
                _fixture.CapacityOwnerUserId, TaskFlow.Domain.Enums.Meetings.MeetingGuestDecisionKind.Admitted));
            await context.SaveChangesAsync();
            return guest.Id;
        });

        await _fixture.RunRetentionCleanupAsync();

        var remaining = await _fixture.WithDbContextAsync(async context => new
        {
            Sessions = await context.MeetingGuestSessions.CountAsync(x => x.MeetingId == meetingId),
            Challenges = await context.MeetingGuestChallenges.CountAsync(x => x.MeetingId == meetingId),
            Decisions = await context.MeetingGuestDecisions.CountAsync(x => x.ParticipantId == participantId)
        });
        Assert.Equal(1, remaining.Sessions);
        Assert.Equal(1, remaining.Challenges);
        // The moderation audit trail is deliberately never purged with the access records.
        Assert.Equal(1, remaining.Decisions);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, int meetingId, string fileName)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("safe meeting attachment"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(file, "file", fileName);
        return client.PostAsync($"api/meeting/{meetingId}/assets", content);
    }

    [Fact]
    public async Task CalendarEntries_ExpandRecurrence_StayOrganizationScoped_AndDeleteIndependently()
    {
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);
        using var outsider = _fixture.CreateClient(999);
        var created = await owner.PostAsJsonAsync("api/calendar", new
        {
            organizationId = _fixture.CapacityOrganizationId,
            kind = 2,
            title = "Annual leave",
            description = "Integration coverage",
            startsAtUtc = "2026-09-01T00:00:00Z",
            endsAtUtc = "2026-09-03T00:00:00Z",
            isAllDay = true,
            timeZone = "UTC",
            memberUserId = _fixture.CapacityMemberUserId,
            recurrenceFrequency = 2,
            recurrenceInterval = 1,
            recurrenceUntil = "2026-09-15"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = await created.Content.ReadFromJsonAsync<int>();

        var path = $"api/calendar/organization/{_fixture.CapacityOrganizationId}" +
            "?fromUtc=2026-09-01T00:00:00Z&toUtc=2026-09-22T00:00:00Z";
        var queryResponse = await owner.GetAsync(path);
        Assert.True(queryResponse.IsSuccessStatusCode,
            $"Calendar query failed: {queryResponse.StatusCode} {await queryResponse.Content.ReadAsStringAsync()}");
        var occurrences = await queryResponse.Content.ReadFromJsonAsync<List<CalendarEntryResponse>>();
        Assert.NotNull(occurrences);
        Assert.Equal(3, occurrences.Count);
        Assert.All(occurrences, occurrence => Assert.Equal(id, occurrence.Id));
        Assert.Equal(3, occurrences.Select(x => x.OccurrenceId).Distinct().Count());
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(path)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"api/calendar/{id}")).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<List<CalendarEntryResponse>>(path))!);
    }

    [Fact]
    public async Task Capacity_IsServerComputed_OrganizationScoped_AndUtcWeekSafe()
    {
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);
        using var outsider = _fixture.CreateClient(999);
        const string path = "api/report/capacity/";

        var response = await owner.GetAsync(
            $"{path}{_fixture.CapacityOrganizationId}?weekStart=2026-08-31&weeks=2");
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Capacity query failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var rows = await response.Content.ReadFromJsonAsync<List<CapacityResponse>>();
        Assert.NotNull(rows);
        Assert.Equal(4, rows.Count);

        var balanced = Assert.Single(rows, row =>
            row.MemberName == "Asha Rao" && row.WeekStart == new DateOnly(2026, 8, 31));
        Assert.True(balanced.HasEnoughData);
        Assert.Equal(1_800, balanced.AssignedEstimateMinutes);
        Assert.Equal(600, balanced.RemainingCapacityMinutes);
        Assert.Equal("Balanced", balanced.WorkloadState);

        var unknown = Assert.Single(rows, row =>
            row.MemberName == "Ben Shah" && row.WeekStart == new DateOnly(2026, 8, 31));
        Assert.False(unknown.HasEnoughData);
        Assert.Null(unknown.AssignedEstimateMinutes);
        Assert.Equal(1, unknown.MissingEstimateTaskCount);
        Assert.Equal("NotEnoughData", unknown.WorkloadState);

        var nextWeek = Assert.Single(rows, row =>
            row.MemberName == "Asha Rao" && row.WeekStart == new DateOnly(2026, 9, 7));
        Assert.Equal("Heavy", nextWeek.WorkloadState);
        Assert.Equal(3_000, nextWeek.AssignedEstimateMinutes);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await outsider.GetAsync(
                $"{path}{_fixture.CapacityOrganizationId}?weekStart=2026-08-31&weeks=1")).StatusCode);
    }

    [Fact]
    public async Task CloudBoard_RestoresAcrossClients_DeniesOtherUsers_AndRejectsStaleSaves()
    {
        using var firstDevice = _fixture.CreateClient(ownerUserId: 101);
        using var secondDevice = _fixture.CreateClient(ownerUserId: 101);
        using var otherUser = _fixture.CreateClient(ownerUserId: 202);

        var initial = await firstDevice.GetAsync($"api/planner/projects/{_fixture.ProjectId}/board");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        Assert.Equal(new EntityTagHeaderValue("\"0\""), initial.Headers.ETag);

        var forbidden = await otherUser.GetAsync($"api/planner/projects/{_fixture.ProjectId}/board");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        const string changedScene =
            "{\"type\":\"excalidraw\",\"version\":2,\"source\":\"taskflow\",\"elements\":[{\"id\":\"shape-1\",\"type\":\"rectangle\"}],\"appState\":{},\"files\":{}}";

        var saved = await firstDevice.PutAsJsonAsync(
            $"api/planner/projects/{_fixture.ProjectId}/board/scene",
            new { expectedRevision = 0, sceneJson = changedScene });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(new EntityTagHeaderValue("\"1\""), saved.Headers.ETag);

        var stale = await secondDevice.PutAsJsonAsync(
            $"api/planner/projects/{_fixture.ProjectId}/board/scene",
            new { expectedRevision = 0, sceneJson = PlannerSceneDocument.Empty });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var error = JsonDocument.Parse(await stale.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                "PLANNER_REVISION_CONFLICT",
                error.RootElement.GetProperty("code").GetString());
        }

        var restored = await secondDevice.GetFromJsonAsync<PlannerBoardResponse>(
            $"api/planner/projects/{_fixture.ProjectId}/board");
        Assert.NotNull(restored);
        Assert.Equal(1, restored.Revision);
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(changedScene), JsonNode.Parse(restored.SceneJson)));

        var revisions = await secondDevice.GetFromJsonAsync<List<PlannerRevisionResponse>>(
            $"api/planner/projects/{_fixture.ProjectId}/board/revisions");
        var revision = Assert.Single(revisions!);
        Assert.Equal(1, revision.Revision);
        Assert.Equal(101, revision.CreatedByUserId);

        var racingSaves = await Task.WhenAll(
            firstDevice.PutAsJsonAsync(
                $"api/planner/projects/{_fixture.ConcurrentProjectId}/board/scene",
                new { expectedRevision = 0, sceneJson = changedScene }),
            secondDevice.PutAsJsonAsync(
                $"api/planner/projects/{_fixture.ConcurrentProjectId}/board/scene",
                new { expectedRevision = 0, sceneJson = PlannerSceneDocument.Empty }));
        Assert.Single(racingSaves, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(racingSaves, response => response.StatusCode == HttpStatusCode.Conflict);

        var concurrentBoard = await firstDevice.GetFromJsonAsync<PlannerBoardResponse>(
            $"api/planner/projects/{_fixture.ConcurrentProjectId}/board");
        Assert.NotNull(concurrentBoard);
        Assert.Equal(1, concurrentBoard.Revision);
    }

    [Fact]
    public async Task LinkedWorkItems_AreAtomic_OwnerScoped_Rehydrated_AndExplicitlyUnlinkedOrDeleted()
    {
        using var owner = _fixture.CreateClient(ownerUserId: 101);
        using var otherUser = _fixture.CreateClient(ownerUserId: 202);
        var basePath = $"api/planner/projects/{_fixture.ProjectId}/board";

        var projectNodeResponse = await owner.PostAsJsonAsync($"{basePath}/nodes/project", new { elementId = "project-card" });
        Assert.True(projectNodeResponse.StatusCode == HttpStatusCode.OK,
            $"Project link failed: {projectNodeResponse.StatusCode} {await projectNodeResponse.Content.ReadAsStringAsync()}");
        var projectNodeId = await projectNodeResponse.Content.ReadFromJsonAsync<Guid>();

        var taskNodeResponse = await owner.PostAsJsonAsync($"{basePath}/nodes/tasks", new
        {
            elementId = "task-card",
            title = "Planner-created task",
            description = "Canonical work item",
            startDate = DateTime.UtcNow,
            priority = 2,
            expectedCompletionDate = (DateTime?)null,
        });
        Assert.Equal(HttpStatusCode.OK, taskNodeResponse.StatusCode);
        var taskNodeId = await taskNodeResponse.Content.ReadFromJsonAsync<Guid>();

        var workspace = await owner.GetFromJsonAsync<PlannerWorkspaceResponse>($"{basePath}/workspace");
        Assert.NotNull(workspace);
        var taskNode = Assert.Single(workspace.Nodes, node => node.NodeId == taskNodeId);
        Assert.Equal("Planner-created task", taskNode.Title);
        Assert.Equal(0, taskNode.CompletionPercentage);

        var subTaskResponse = await owner.PostAsJsonAsync($"{basePath}/nodes/subtasks", new
        {
            elementId = "subtask-card",
            taskId = taskNode.EntityId,
            title = "Planner-created subtask",
        });
        Assert.Equal(HttpStatusCode.OK, subTaskResponse.StatusCode);
        var subTaskNodeId = await subTaskResponse.Content.ReadFromJsonAsync<Guid>();

        workspace = await owner.GetFromJsonAsync<PlannerWorkspaceResponse>($"{basePath}/workspace");
        var subTaskNode = Assert.Single(workspace!.Nodes, node => node.NodeId == subTaskNodeId);
        var completedElsewhere = await owner.PutAsJsonAsync($"api/subtask/{subTaskNode.EntityId}/complete", new { });
        Assert.Equal(HttpStatusCode.NoContent, completedElsewhere.StatusCode);

        workspace = await owner.GetFromJsonAsync<PlannerWorkspaceResponse>($"{basePath}/workspace");
        taskNode = Assert.Single(workspace!.Nodes, node => node.NodeId == taskNodeId);
        subTaskNode = Assert.Single(workspace.Nodes, node => node.NodeId == subTaskNodeId);
        Assert.Equal(100, taskNode.CompletionPercentage);
        Assert.Equal(100, subTaskNode.CompletionPercentage);

        var updateProject = await owner.PutAsJsonAsync($"{basePath}/nodes/{projectNodeId}", new
        {
            title = "Planner integration project",
            description = "Updated through Planner",
            expectedCompletionDate = (DateTime?)null,
            priority = (int?)null,
            problemStatement = "Make visual planning canonical",
            budgetAmount = 1250.50m,
            budgetCurrency = "usd",
            approximateDurationWeeks = 8,
        });
        Assert.Equal(HttpStatusCode.NoContent, updateProject.StatusCode);
        workspace = await owner.GetFromJsonAsync<PlannerWorkspaceResponse>($"{basePath}/workspace");
        Assert.Equal("USD", workspace!.Project.BudgetCurrency);
        Assert.Equal(8, workspace.Project.ApproximateDurationWeeks);

        var forbidden = await otherUser.GetAsync($"{basePath}/workspace");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var unlink = await owner.DeleteAsync($"{basePath}/nodes/{subTaskNodeId}?deleteEntity=false");
        Assert.Equal(HttpStatusCode.NoContent, unlink.StatusCode);
        var stillExists = await owner.GetAsync($"api/subtask/task/{taskNode.EntityId}");
        Assert.Equal(HttpStatusCode.OK, stillExists.StatusCode);

        var deleteTask = await owner.DeleteAsync($"{basePath}/nodes/{taskNodeId}?deleteEntity=true");
        Assert.Equal(HttpStatusCode.NoContent, deleteTask.StatusCode);
        var deletedTask = await owner.GetAsync($"api/task/{taskNode.EntityId}");
        Assert.Equal(HttpStatusCode.NotFound, deletedTask.StatusCode);
    }

    [Fact]
    public async Task Resources_AreStoredOutsideScenes_OwnerScoped_Validated_Relinkable_AndDeletable()
    {
        using var owner = _fixture.CreateClient(ownerUserId: 101);
        using var otherUser = _fixture.CreateClient(ownerUserId: 202);
        var basePath = $"api/planner/projects/{_fixture.ProjectId}/board";
        var suffix = Guid.NewGuid().ToString("N");

        var noteResponse = await owner.PostAsJsonAsync($"{basePath}/resources/notes", new
        {
            elementId = $"note-{suffix}", title = "Launch context", content = "Keep the scene small."
        });
        Assert.Equal(HttpStatusCode.OK, noteResponse.StatusCode);
        var noteNodeId = await noteResponse.Content.ReadFromJsonAsync<Guid>();

        var resources = await owner.GetFromJsonAsync<List<PlannerResourceResponse>>($"{basePath}/resources");
        var note = Assert.Single(resources!, x => x.NodeId == noteNodeId);
        Assert.Equal(1, note.Kind);
        Assert.Equal("Keep the scene small.", note.Content);

        var unlink = await owner.DeleteAsync($"{basePath}/nodes/{noteNodeId}?deleteEntity=false");
        Assert.Equal(HttpStatusCode.NoContent, unlink.StatusCode);
        resources = await owner.GetFromJsonAsync<List<PlannerResourceResponse>>($"{basePath}/resources");
        note = Assert.Single(resources!, x => x.Id == note.Id);
        Assert.Null(note.NodeId);

        var relink = await owner.PostAsJsonAsync($"{basePath}/resources/{note.Id}/link",
            new { elementId = $"note-relinked-{suffix}" });
        Assert.Equal(HttpStatusCode.OK, relink.StatusCode);

        using var upload = new MultipartFormDataContent();
        upload.Add(new StringContent($"doc-{suffix}"), "elementId");
        upload.Add(new StringContent("Release brief"), "title");
        var file = new ByteArrayContent("private planner document"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        upload.Add(file, "file", "brief.txt");
        var uploaded = await owner.PostAsync($"{basePath}/resources/documents", upload);
        Assert.True(uploaded.StatusCode == HttpStatusCode.OK,
            $"Upload failed: {uploaded.StatusCode} {await uploaded.Content.ReadAsStringAsync()}");
        resources = await owner.GetFromJsonAsync<List<PlannerResourceResponse>>($"{basePath}/resources");
        var document = Assert.Single(resources!, x => x.Title == "Release brief");
        Assert.Equal("brief.txt", document.Asset!.FileName);
        Assert.Equal(64, document.Asset.Sha256.Length);

        var content = await owner.GetAsync($"{basePath}/resources/{document.Id}/content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("private planner document", await content.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await otherUser.GetAsync($"{basePath}/resources/{document.Id}/content")).StatusCode);

        using var invalidUpload = new MultipartFormDataContent();
        invalidUpload.Add(new StringContent($"bad-{suffix}"), "elementId");
        invalidUpload.Add(new StringContent("Executable"), "title");
        var invalidFile = new ByteArrayContent([1, 2, 3]);
        invalidFile.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        invalidUpload.Add(invalidFile, "file", "malware.exe");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PostAsync($"{basePath}/resources/documents", invalidUpload)).StatusCode);

        var renamed = await owner.PutAsJsonAsync($"{basePath}/resources/{document.Id}", new
        {
            title = "Final release brief", content = (string?)null, url = (string?)null, fileName = "release-notes.txt"
        });
        Assert.Equal(HttpStatusCode.NoContent, renamed.StatusCode);
        resources = await owner.GetFromJsonAsync<List<PlannerResourceResponse>>($"{basePath}/resources");
        Assert.Equal("release-notes.txt", Assert.Single(resources!, x => x.Id == document.Id).Asset!.FileName);

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.DeleteAsync($"{basePath}/resources/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.GetAsync($"{basePath}/resources/{document.Id}/content")).StatusCode);

        var board = await owner.GetFromJsonAsync<PlannerBoardResponse>(basePath);
        Assert.DoesNotContain("private planner document", board!.SceneJson);
    }

    [Fact]
    public async Task TemplateLibrary_IsAdminManaged_Versioned_Validated_AndSnapshotsNodes()
    {
        using var admin = _fixture.CreateClient(1, SystemRoleNames.Admin);
        using var member = _fixture.CreateClient(101);
        using var nonAdmin = _fixture.CreateClient(202);
        var definition = new { name = "Delivery task", objectType = 2, icon = "ListTodo", header = "Delivery",
            backgroundColor = "#f3f0ff", strokeColor = "#7048e8", defaultWidth = 280, defaultHeight = 128,
            visibleFieldsJson = "[\"title\",\"priority\",\"progress\"]", defaultValuesJson = "{\"priority\":2}", sortOrder = 10, isActive = true };

        Assert.Equal(HttpStatusCode.Forbidden, (await nonAdmin.PostAsJsonAsync("api/admin/planner/templates", definition)).StatusCode);
        var created = await admin.PostAsJsonAsync("api/admin/planner/templates", definition);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var templateId = await created.Content.ReadFromJsonAsync<Guid>();
        var published = await admin.PostAsJsonAsync($"api/admin/planner/templates/{templateId}/publish", new { });
        Assert.True(published.StatusCode == HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
        var versionOne = await published.Content.ReadFromJsonAsync<Guid>();

        var memberTemplates = await member.GetFromJsonAsync<List<PlannerTemplateResponse>>("api/planner/templates");
        Assert.Equal(versionOne, Assert.Single(memberTemplates!).Versions.Single().Id);

        var task = await member.PostAsJsonAsync($"api/planner/projects/{_fixture.ProjectId}/board/nodes/tasks", new {
            elementId = "templated-task-card", title = "Templated delivery task", description = "", startDate = DateTime.UtcNow,
            priority = 2, expectedCompletionDate = (DateTime?)null, templateVersionId = versionOne });
        Assert.Equal(HttpStatusCode.OK, task.StatusCode);

        var updateDefinition = new { definition.name, definition.objectType, definition.icon, header = "Delivery v2",
            backgroundColor = "#fff4e6", strokeColor = "#e8590c", definition.defaultWidth, definition.defaultHeight,
            definition.visibleFieldsJson, definition.defaultValuesJson, definition.sortOrder, definition.isActive };
        var updated = await admin.PutAsJsonAsync($"api/admin/planner/templates/{templateId}", updateDefinition);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var versionTwo = await updated.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(versionOne, versionTwo);

        memberTemplates = await member.GetFromJsonAsync<List<PlannerTemplateResponse>>("api/planner/templates");
        Assert.Equal(versionTwo, Assert.Single(memberTemplates!).Versions.Single().Id);
        var staleVersionUse = await member.PostAsJsonAsync($"api/planner/projects/{_fixture.ProjectId}/board/nodes/tasks", new {
            elementId = "stale-template-card", title = "Stale template task", description = "", startDate = DateTime.UtcNow,
            priority = 2, expectedCompletionDate = (DateTime?)null, templateVersionId = versionOne });
        Assert.Equal(HttpStatusCode.NotFound, staleVersionUse.StatusCode);

        var workspace = await member.GetFromJsonAsync<PlannerWorkspaceWithTemplateResponse>($"api/planner/projects/{_fixture.ProjectId}/board/workspace");
        Assert.Equal(versionOne, Assert.Single(workspace!.Nodes, x => x.ElementId == "templated-task-card").TemplateVersion!.Id);

        await admin.PostAsJsonAsync($"api/admin/planner/templates/{templateId}/archive", new { });
        Assert.Empty((await member.GetFromJsonAsync<List<PlannerTemplateResponse>>("api/planner/templates"))!);

        var invalid = await admin.PostAsJsonAsync("api/admin/planner/templates", new { definition.name, objectType = 3,
            definition.icon, definition.header, definition.backgroundColor, definition.strokeColor, definition.defaultWidth,
            definition.defaultHeight, visibleFieldsJson = "[\"budgetAmount\"]", definition.defaultValuesJson, definition.sortOrder, definition.isActive });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task PrimaryRequirements_AreImmutable_OwnerScoped_AndTrackScopeButNotProgress()
    {
        using var owner = _fixture.CreateClient(101);
        using var otherUser = _fixture.CreateClient(202);
        var basePath = $"api/planner/projects/{_fixture.RequirementProjectId}/board";

        var taskResponse = await owner.PostAsJsonAsync($"{basePath}/nodes/tasks", new
        {
            elementId = "baseline-task", title = "Define launch scope", description = "Initial scope",
            startDate = DateTime.UtcNow, priority = 2, expectedCompletionDate = (DateTime?)null,
        });
        Assert.Equal(HttpStatusCode.OK, taskResponse.StatusCode);
        var taskNodeId = await taskResponse.Content.ReadFromJsonAsync<Guid>();
        var workspace = await owner.GetFromJsonAsync<PlannerWorkspaceResponse>($"{basePath}/workspace");
        var taskNode = Assert.Single(workspace!.Nodes, x => x.NodeId == taskNodeId);

        var subTaskResponse = await owner.PostAsJsonAsync($"{basePath}/nodes/subtasks", new
        {
            elementId = "baseline-subtask", taskId = taskNode.EntityId, title = "Confirm audience",
        });
        Assert.Equal(HttpStatusCode.OK, subTaskResponse.StatusCode);
        var subTaskNodeId = await subTaskResponse.Content.ReadFromJsonAsync<Guid>();

        var finalized = await owner.PostAsJsonAsync($"{basePath}/requirements/finalize", new { });
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);
        var baseline = await finalized.Content.ReadFromJsonAsync<RequirementBaselineResponse>();
        Assert.NotNull(baseline);
        Assert.Equal(1, baseline.BaselineNumber);
        Assert.Equal(3, baseline.Snapshots.Count);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsJsonAsync($"{basePath}/requirements/finalize", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await otherUser.GetAsync($"{basePath}/requirements/baselines")).StatusCode);

        workspace = await owner.GetFromJsonAsync<PlannerWorkspaceResponse>($"{basePath}/workspace");
        var subTask = Assert.Single(workspace!.Nodes, x => x.NodeId == subTaskNodeId);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.PutAsJsonAsync($"api/subtask/{subTask.EntityId}/complete", new { })).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<List<RequirementChangeResponse>>(
            $"{basePath}/requirements/changes"))!);

        var updated = await owner.PutAsJsonAsync($"{basePath}/nodes/{taskNodeId}", new
        {
            title = "Define launch scope and owners", description = "Initial scope", priority = 2,
            expectedCompletionDate = (DateTime?)null, problemStatement = (string?)null,
            budgetAmount = (decimal?)null, budgetCurrency = (string?)null,
            approximateDurationWeeks = (int?)null, changeReason = "Ownership was missing",
        });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        var newTask = await owner.PostAsJsonAsync($"{basePath}/nodes/tasks", new
        {
            elementId = "post-baseline-task", title = "Prepare rollback", description = "New scope",
            startDate = DateTime.UtcNow, priority = 3, expectedCompletionDate = (DateTime?)null,
            changeReason = "Release risk review",
        });
        Assert.Equal(HttpStatusCode.OK, newTask.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.DeleteAsync($"{basePath}/nodes/{subTaskNodeId}?deleteEntity=true&changeReason=No%20longer%20needed")).StatusCode);

        var changes = await owner.GetFromJsonAsync<List<RequirementChangeResponse>>(
            $"{basePath}/requirements/changes");
        Assert.Contains(changes!, x => x.ChangeType == 1 && x.Title == "Prepare rollback");
        Assert.Contains(changes!, x => x.ChangeType == 2 && x.Reason == "Ownership was missing");
        Assert.Contains(changes!, x => x.ChangeType == 3 && x.Title == "Confirm audience");

        var changedOnly = await owner.GetFromJsonAsync<List<RequirementChangeResponse>>(
            $"{basePath}/requirements/changes?changeType=2");
        Assert.Single(changedOnly!);
        var comparison = await owner.GetFromJsonAsync<RequirementComparisonResponse>(
            $"{basePath}/requirements/compare?baselineId={baseline.Id}");
        Assert.NotNull(comparison);
        var changedTask = Assert.Single(comparison.Items, x => x.ChangeType == 2);
        Assert.Contains(changedTask.Differences, x => x.Field == "title" &&
            x.BaselineValue == "Define launch scope" && x.CurrentValue == "Define launch scope and owners");
    }

    private sealed record PlannerBoardResponse(int Revision, string SceneJson);
    private sealed record PlannerRevisionResponse(int Revision, int CreatedByUserId);
    private sealed record PlannerWorkspaceResponse(PlannerProjectResponse Project, List<PlannerNodeResponse> Nodes);
    private sealed record PlannerProjectResponse(string? BudgetCurrency, int? ApproximateDurationWeeks);
    private sealed record PlannerNodeResponse(Guid NodeId, int EntityId, string Title, decimal CompletionPercentage);
    private sealed record PlannerTemplateResponse(Guid Id, List<PlannerTemplateVersionResponse> Versions);
    private sealed record PlannerTemplateVersionResponse(Guid Id);
    private sealed record PlannerWorkspaceWithTemplateResponse(List<PlannerNodeWithTemplateResponse> Nodes);
    private sealed record PlannerNodeWithTemplateResponse(string ElementId, PlannerTemplateVersionResponse? TemplateVersion);
    private sealed record PlannerResourceResponse(Guid Id, int Kind, string Title, string? Content,
        Guid? NodeId, PlannerAssetResponse? Asset);
    private sealed record PlannerAssetResponse(string FileName, string Sha256);
    private sealed record RequirementBaselineResponse(Guid Id, int BaselineNumber,
        List<RequirementSnapshotResponse> Snapshots);
    private sealed record RequirementSnapshotResponse(Guid Id, int EntityType, int EntityId, string Title);
    private sealed record RequirementChangeResponse(int ChangeType, string Title, string? Reason);
    private sealed record RequirementComparisonResponse(List<RequirementComparisonItemResponse> Items);
    private sealed record RequirementComparisonItemResponse(int ChangeType,
        List<RequirementDifferenceResponse> Differences);
    private sealed record RequirementDifferenceResponse(string Field, string? BaselineValue, string? CurrentValue);
    private sealed record CapacityResponse(
        string MemberName,
        DateOnly WeekStart,
        int? AssignedEstimateMinutes,
        int? RemainingCapacityMinutes,
        int MissingEstimateTaskCount,
        bool HasEnoughData,
        string WorkloadState);
    private sealed record CalendarEntryResponse(int Id, string OccurrenceId);
    private sealed record MeetingDetailResponse(int Status, DateTime? ActualStartUtc,
        DateTime? ActualEndUtc, List<MeetingParticipantResponse> Participants);
    private sealed record MeetingParticipantResponse(int Id, int? UserId, string? Email, bool IsGuest);
}

public sealed class PlannerApiFixture : IAsyncLifetime
{
    private readonly string _databaseName =
        $"taskflow_planner_test_{Guid.NewGuid():N}";
    private string _adminConnectionString = string.Empty;
    private WebApplicationFactory<Program>? _factory;
    private int _nextClientIp;

    public int ProjectId { get; private set; }
    public int ConcurrentProjectId { get; private set; }
    public int RequirementProjectId { get; private set; }
    public int CapacityOrganizationId { get; private set; }
    public int CapacityOwnerUserId { get; private set; }
    public int CapacityMemberUserId { get; private set; }
    public int OtherOrganizationOwnerUserId { get; private set; }
    public PlannerTestEmailService Email { get; } = new();
    public TestMeetingMediaProvider Media { get; } = new();

    public async Task InitializeAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settingsPath = Path.Combine(repositoryRoot, "TaskFlow.Api", "appsettings.json");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
        var configuredConnection = settings.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString()
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        var testConnection = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Database = _databaseName,
            Pooling = false,
        };
        var adminConnection = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Database = "postgres",
            Pooling = false,
        };
        _adminConnectionString = adminConnection.ConnectionString;

        await using (var connection = new NpgsqlConnection(_adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"CREATE DATABASE \"{_databaseName}\"",
                connection);
            await command.ExecuteNonQueryAsync();
        }

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = testConnection.ConnectionString,
                        ["ClientSettings:BaseUrl"] = "http://localhost",
                        ["ObjectStorage:Provider"] = "Local",
                        ["ObjectStorage:LocalPath"] = "App_Data/integration-test-objects",
                        ["Meetings:Enabled"] = "true",
                        ["Meetings:GuestsEnabled"] = "true",
                        // Small on purpose: a ceiling nobody can reach in a test proves nothing.
                        // These are per meeting, and every test uses its own meeting.
                        ["Meetings:MaxMessagesPerMeeting"] = "10",
                        ["Meetings:MaxAssetsPerMeeting"] = "2",
                        ["Meetings:GuestAccessRecordRetentionDays"] = "1",
                        ["LiveKit:Enabled"] = "true",
                        ["LiveKit:Url"] = "ws://livekit.integration.test",
                        ["LiveKit:ApiKey"] = "integration-key",
                        ["LiveKit:ApiSecret"] = "integration-test-livekit-secret-at-least-32-characters",
                        ["OneTimeCodeSettings:SecretKey"] = "integration-test-code-secret-at-least-32-characters",
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IObjectStorage>();
                    services.AddSingleton<IObjectStorage, PlannerTestObjectStorage>();
                    services.RemoveAll<IEmailService>();
                    services.AddSingleton<IEmailService>(Email);
                    services.RemoveAll<IMeetingMediaProvider>();
                    services.AddSingleton<IMeetingMediaProvider>(Media);
                    services.AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                            options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                        })
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                            TestAuthenticationHandler.SchemeName,
                            _ => { });
                });
            });

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var migrations = context.Database.GetMigrations().ToList();
        var plannerMigrationIndex = migrations.FindIndex(
            migration => migration.EndsWith("_AddPlannerCloudPersistence", StringComparison.Ordinal));
        Assert.True(plannerMigrationIndex > 0, "Planner migration must follow an existing schema migration.");
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[plannerMigrationIndex - 1]);

        var databaseConnection = (NpgsqlConnection)context.Database.GetDbConnection();
        await databaseConnection.OpenAsync();
        ProjectId = await InsertPersonalProjectAsync(databaseConnection, "Planner integration project");
        ConcurrentProjectId = await InsertPersonalProjectAsync(databaseConnection, "Planner concurrency project");
        RequirementProjectId = await InsertPersonalProjectAsync(databaseConnection, "Planner requirement history project");

        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();
        var backfilledBoards = await context.PlannerBoards
            .Where(board => board.ProjectId == ProjectId || board.ProjectId == ConcurrentProjectId || board.ProjectId == RequirementProjectId)
            .ToListAsync();
        Assert.Equal(3, backfilledBoards.Count);
        Assert.All(backfilledBoards, board =>
        {
            Assert.Equal(101, board.OwnerUserId);
            Assert.Equal(0, board.CurrentRevision);
        });

        await SeedCapacityScenarioAsync(context);
    }

    private async Task SeedCapacityScenarioAsync(TaskFlowDbContext context)
    {
        var owner = User.Register(
            new FullName("Asha", "Rao"),
            new Email("asha.capacity@example.test"),
            new PhoneNumber("9876543210"),
            "test-password-hash",
            AccountType.Organization);
        var colleague = User.Register(
            new FullName("Ben", "Shah"),
            new Email("ben.capacity@example.test"),
            new PhoneNumber("9876543211"),
            "test-password-hash");
        var otherOwner = User.Register(
            new FullName("Nila", "Patel"),
            new Email("nila.other@example.test"),
            new PhoneNumber("9876543212"),
            "test-password-hash",
            AccountType.Organization);
        owner.ClearDomainEvents();
        colleague.ClearDomainEvents();
        otherOwner.ClearDomainEvents();
        context.Users.AddRange(owner, colleague, otherOwner);
        await context.SaveChangesAsync();

        var organization = new Organization("Capacity test", "", owner.Id);
        var otherOrganization = new Organization("Other organization", "", otherOwner.Id);
        context.Organizations.AddRange(organization, otherOrganization);
        await context.SaveChangesAsync();
        var role = new OrganizationRole(organization.Id, "Planner", "");
        context.OrganizationRoles.Add(role);
        await context.SaveChangesAsync();

        var ownerMember = new OrganizationMember(organization.Id, owner.Id, role.Id);
        ownerMember.SetWeeklyCapacity(2_400);
        var colleagueMember = new OrganizationMember(organization.Id, colleague.Id, role.Id);
        colleagueMember.SetWeeklyCapacity(2_400);
        context.OrganizationMembers.AddRange(ownerMember, colleagueMember);
        await context.SaveChangesAsync();

        context.Tasks.AddRange(
            CapacityTask("Monday edge", owner.Id, organization.Id,
                new DateTime(2026, 8, 31, 0, 30, 0, DateTimeKind.Utc), 1_200),
            CapacityTask("Sunday edge", owner.Id, organization.Id,
                new DateTime(2026, 9, 6, 23, 30, 0, DateTimeKind.Utc), 600),
            CapacityTask("Next Monday", owner.Id, organization.Id,
                new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc), 3_000),
            CapacityTask("Unknown effort", colleague.Id, organization.Id,
                new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc), null));
        await context.SaveChangesAsync();

        CapacityOrganizationId = organization.Id;
        CapacityOwnerUserId = owner.Id;
        CapacityMemberUserId = colleague.Id;
        OtherOrganizationOwnerUserId = otherOwner.Id;
    }

    private static TaskEntity CapacityTask(
        string title,
        int userId,
        int organizationId,
        DateTime due,
        int? estimateMinutes)
    {
        var task = new TaskEntity(
            title,
            "",
            due.AddDays(-1),
            TaskPriority.Medium,
            organizationId,
            userId,
            due);
        task.Assign(userId, userId);
        task.SetEstimate(estimateMinutes);
        task.ClearDomainEvents();
        return task;
    }

    private static async Task<int> InsertPersonalProjectAsync(NpgsqlConnection connection, string title)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO "Projects"
                ("Title", "Description", "Status", "StartDate", "OrganizationId", "CreatedByUserId",
                 "CreatedAt", "IsDeleted")
            VALUES (@title, '', 1, @startDate, NULL, 101, @createdAt, FALSE)
            RETURNING "Id";
            """, connection);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("startDate", DateTime.UtcNow);
        command.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Test project insert did not return an id."));
    }

    public async Task<MeetingEvidence> ReadMeetingEvidenceAsync(int meetingId)
    {
        await using var scope = (_factory ?? throw new InvalidOperationException("Fixture is not ready."))
            .Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var roomName = await context.Meetings.Where(x => x.Id == meetingId).Select(x => x.RoomName).SingleAsync();
        var attendanceCount = await context.MeetingAttendance.CountAsync(x => x.MeetingId == meetingId);
        var receiptCount = await context.MeetingWebhookReceipts.CountAsync(x => x.MeetingId == meetingId);
        return new(roomName, attendanceCount, receiptCount);
    }

    /// <summary>Runs one real retention pass, the way the hosted service would on its timer.</summary>
    public async Task RunRetentionCleanupAsync()
    {
        await using var scope = (_factory ?? throw new InvalidOperationException("Fixture is not ready."))
            .Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetServices<IHostedService>()
            .OfType<TaskFlow.Infra.Meetings.MeetingRetentionCleanupService>().Single();
        await service.PurgeExpiredAsync(CancellationToken.None);
    }

    public async Task<T> WithDbContextAsync<T>(Func<TaskFlowDbContext, Task<T>> work)
    {
        await using var scope = (_factory ?? throw new InvalidOperationException("Fixture is not ready."))
            .Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>());
    }

    public HttpClient CreateClient(int ownerUserId, string role = SystemRoleNames.User)
    {
        var client = CreateTestClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeader, ownerUserId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeader, role);
        return client;
    }

    public HttpClient CreateAnonymousClient() => CreateTestClient();

    private HttpClient CreateTestClient()
    {
        var client = (_factory ?? throw new InvalidOperationException("Fixture is not ready."))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"198.51.100.{Interlocked.Increment(ref _nextClientIp)}");
        return client;
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        NpgsqlConnection.ClearAllPools();

        if (string.IsNullOrWhiteSpace(_adminConnectionString))
            return;

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TaskFlow.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the TaskFlow repository root.");
    }
}

public sealed record MeetingEvidence(string RoomName, int AttendanceCount, int ReceiptCount);

public sealed class TestMeetingMediaProvider : IMeetingMediaProvider
{
    public const string Authorization = "Test meeting-webhook-signature";
    public bool IsEnabled => true;
    public string WebSocketUrl => "ws://livekit.integration.test";
    public System.Collections.Concurrent.ConcurrentBag<string> RemovedPrefixes { get; } = [];

    public MeetingJoinToken CreateJoinToken(MeetingJoinTokenRequest request) =>
        new($"test-token-{request.ParticipantIdentity}", DateTimeOffset.UtcNow.Add(request.Lifetime));

    public Task RemoveParticipantsAsync(string roomName, string participantIdentityPrefix,
        CancellationToken cancellationToken = default)
    { RemovedPrefixes.Add(participantIdentityPrefix); return Task.CompletedTask; }

    public Task MuteTrackAsync(string roomName, string participantIdentity, string trackSid, bool muted,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CloseRoomAsync(string roomName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public MeetingProviderWebhook VerifyWebhook(string rawBody, string authorizationHeader)
    {
        if (!string.Equals(authorizationHeader, Authorization, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid test webhook signature.");
        var payload = JsonSerializer.Deserialize<TestWebhookPayload>(rawBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid test webhook payload.");
        return new(payload.EventId, payload.EventType, payload.RoomName, payload.ParticipantIdentity,
            payload.ParticipantSid, payload.OccurredAtUtc);
    }

    private sealed record TestWebhookPayload(string EventId, string EventType, string RoomName,
        string? ParticipantIdentity, string? ParticipantSid, DateTimeOffset? OccurredAtUtc);
}

public sealed class PlannerTestObjectStorage : IObjectStorage
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, StoredObject> _objects = new();
    public Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public async Task UploadAsync(string objectKey, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _objects[objectKey] = new StoredObject(buffer.ToArray(), contentType);
    }
    public Task<StoredObject> DownloadAsync(string objectKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_objects[objectKey]);
    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    { _objects.TryRemove(objectKey, out _); return Task.CompletedTask; }
}

public sealed class PlannerTestEmailService : IEmailService
{
    public string LastBody { get; private set; } = string.Empty;
    public string LastCode => System.Text.RegularExpressions.Regex.Match(LastBody, @">([0-9]{6})<").Groups[1].Value;
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    { LastBody = body; return Task.CompletedTask; }
}

internal sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "PlannerIntegrationTest";
    public const string UserIdHeader = "X-Test-User-Id";
    public const string RoleHeader = "X-Test-Role";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var value) ||
            !int.TryParse(value, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = Request.Headers.TryGetValue(RoleHeader, out var roleValue) ? roleValue.ToString() : SystemRoleNames.User;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, $"planner-{userId}@example.test"),
            new Claim(ClaimTypes.Role, role),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

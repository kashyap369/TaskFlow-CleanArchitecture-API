using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Domain.Enums.Meetings;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Meetings;

public sealed class MeetingRetentionCleanupService(IServiceScopeFactory scopes,
    ILogger<MeetingRetentionCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PurgeExpiredAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Meeting retention cleanup failed."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    /// <summary>
    /// Public so a test can drive one pass against a real database rather than waiting on the timer.
    /// </summary>
    public async Task PurgeExpiredAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<MeetingSettings>>().Value;
        await PurgeSpentGuestAccessRecordsAsync(db, settings.GuestAccessRecordRetentionDays, ct);
        var now = DateTime.UtcNow;
        var stalled = 0;
        foreach (var candidate in await FindExpiredAsync(db, now, ct))
        {
            // A meeting somebody is still connected to is not abandoned, however long ago it was
            // supposed to end. Sweeping it would delete a call that is in progress.
            if (candidate.Status == MeetingStatus.Live &&
                await db.MeetingAttendance.AnyAsync(x => x.MeetingId == candidate.Id && x.LeftAtUtc == null, ct))
                continue;
            if (!await PurgeMeetingAsync(db, storage, candidate.Id, now, ct)) stalled++;
        }
        if (stalled > 0)
            logger.LogError(
                "Meeting retention could not delete stored objects for {StalledCount} expired meeting(s); " +
                "their content and personal data are still held and the next pass will retry.", stalled);
    }

    /// <summary>
    /// Every meeting whose retention window has passed, whatever state it stopped in. Until P7.8 this
    /// asked only for <c>Ended</c> meetings that had an <c>ActualEndUtc</c>, which left cancelled
    /// meetings, abandoned drafts and meetings wedged in <c>Live</c> holding their content and their
    /// guests' email addresses indefinitely.
    /// <para>
    /// The clock is <c>ActualEndUtc ?? ScheduledEndUtc ?? UpdatedAt ?? CreatedAt</c>. The first three
    /// terms are what <c>MeetingCollaborationAccess.RetainUntil</c> already uses to make an archive
    /// unreachable; <c>UpdatedAt</c> is the extra term the never-ended states need, so that editing a
    /// long-lived draft restarts its window instead of leaving it eligible from the day it was
    /// created. The database filter is deliberately coarse — the minimum retention is one day, so
    /// nothing eligible can be newer than that — and the exact comparison happens here per row,
    /// because the window length is per meeting.
    /// </para>
    /// </summary>
    private static async Task<IReadOnlyList<ExpiredMeeting>> FindExpiredAsync(
        TaskFlowDbContext db, DateTime now, CancellationToken ct)
    {
        var coarseCutoff = now.AddDays(-1);
        var candidates = await db.Meetings
            .Where(x => (x.ActualEndUtc ?? x.ScheduledEndUtc ?? x.UpdatedAt ?? x.CreatedAt) <= coarseCutoff)
            .Select(x => new ExpiredMeeting(x.Id, x.Status,
                x.ActualEndUtc ?? x.ScheduledEndUtc ?? x.UpdatedAt ?? x.CreatedAt, x.RetentionDays))
            .ToListAsync(ct);
        return candidates.Where(x => x.RetentionBasisUtc.AddDays(x.RetentionDays) <= now).ToList();
    }

    private sealed record ExpiredMeeting(int Id, MeetingStatus Status, DateTime RetentionBasisUtc, int RetentionDays);

    /// <summary>
    /// Storage first, database second: rows are cleared only once every object this meeting owns is
    /// gone, so a storage outage leaves the meeting intact for the next pass rather than losing the
    /// only pointer to a file that still exists. Returns false when that happened.
    /// </summary>
    private async Task<bool> PurgeMeetingAsync(TaskFlowDbContext db, IObjectStorage storage,
        int meetingId, DateTime now, CancellationToken ct)
    {
        var assets = await db.MeetingAssets.Where(x => x.MeetingId == meetingId).ToListAsync(ct);
        var recordings = await db.MeetingRecordings.Where(x => x.MeetingId == meetingId).ToListAsync(ct);
        var storageComplete = true;
        foreach (var asset in assets)
            storageComplete &= await TryDeleteObjectAsync(storage, asset.StorageKey, meetingId, "asset", ct);
        // P7.8: every recording object, not only the Ready ones. A Failed or Processing Egress can
        // still have written a partial composite file, and nothing else ever deleted it — so the
        // video of a meeting outlived the meeting precisely when the recording had gone wrong.
        foreach (var recording in recordings.Where(x => !string.IsNullOrWhiteSpace(x.StorageKey)))
            storageComplete &= await TryDeleteObjectAsync(storage, recording.StorageKey, meetingId, "recording", ct);
        if (!storageComplete) return false;

        foreach (var asset in assets) asset.SoftDelete();
        foreach (var recording in recordings) recording.SoftDelete();
        foreach (var consent in await db.MeetingRecordingConsents.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) consent.SoftDelete();
        foreach (var message in await db.MeetingMessages.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) message.SoftDelete();
        foreach (var note in await db.MeetingNotes.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) note.SoftDelete();
        foreach (var revision in await db.MeetingNoteRevisions.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) revision.SoftDelete();
        foreach (var attendance in await db.MeetingAttendance.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) attendance.SoftDelete();
        var redacted = await RedactPersonalDataAsync(db, meetingId, now, ct);
        await db.SaveChangesAsync(ct);
        if (redacted > 0)
            logger.LogInformation(
                "Meeting retention redacted personal data from {RedactedCount} row(s) for meeting {MeetingId}.",
                redacted, meetingId);
        return true;
    }

    /// <summary>
    /// P7.8. Soft-deleting the content leaves the roster and the invitations, and those are where a
    /// guest's email address, their chosen display name and the address a private invitation was
    /// locked to live. Those rows cannot be removed — attendance, messages, consents and the guest
    /// moderation trail all reference them — so retention clears the personal columns instead and
    /// keeps the shape of who took part. Guest <i>decisions</i> stay whole on purpose: they are an
    /// audit trail about a moderator, and they name a guest only by reference to a row that no
    /// longer does.
    /// </summary>
    private static async Task<int> RedactPersonalDataAsync(TaskFlowDbContext db, int meetingId,
        DateTime now, CancellationToken ct)
    {
        var redacted = 0;
        foreach (var participant in await db.MeetingParticipants.IgnoreQueryFilters()
                     .Where(x => x.MeetingId == meetingId).ToListAsync(ct))
            if (participant.RedactPersonalData()) redacted++;
        foreach (var link in await db.MeetingAccessLinks.IgnoreQueryFilters()
                     .Where(x => x.MeetingId == meetingId).ToListAsync(ct))
            if (link.RedactPersonalData(now)) redacted++;
        return redacted;
    }

    /// <summary>
    /// An object that is not there is the outcome this wants, so a missing key counts as success: a
    /// recording that failed before Egress wrote anything must not stall its meeting's retention
    /// forever. Any other failure is real and blocks the delete of the rows that point at it.
    /// </summary>
    private async Task<bool> TryDeleteObjectAsync(IObjectStorage storage, string key, int meetingId,
        string kind, CancellationToken ct)
    {
        try { await storage.DeleteAsync(key, ct); return true; }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            logger.LogDebug(error, "Expired meeting {Kind} object for meeting {MeetingId} was already gone.", kind, meetingId);
            return true;
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Could not delete expired meeting {Kind} object for meeting {MeetingId}.", kind, meetingId);
            return false;
        }
    }

    /// <summary>
    /// Guest sessions and OTP challenges are access records, not meeting content, so meeting
    /// retention never reached them and nothing else deleted them: the tables grew for the life of
    /// the deployment (threat model A-07). Only spent rows go — a session that has expired or been
    /// revoked, a challenge that has expired or been consumed — and only once they are older than
    /// the declared window, so a support question about a recent join can still be answered.
    /// Guest <i>decisions</i> are untouched on purpose: they are the moderation audit trail.
    /// </summary>
    private async Task PurgeSpentGuestAccessRecordsAsync(TaskFlowDbContext db, int retentionDays, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays));
        var sessions = await db.MeetingGuestSessions
            .Where(x => (x.ExpiresAtUtc <= cutoff) || (x.RevokedAtUtc != null && x.RevokedAtUtc <= cutoff))
            .ToListAsync(ct);
        var challenges = await db.MeetingGuestChallenges
            .Where(x => (x.ExpiresAtUtc <= cutoff) || (x.ConsumedAtUtc != null && x.ConsumedAtUtc <= cutoff))
            .ToListAsync(ct);
        if (sessions.Count == 0 && challenges.Count == 0) return;
        // These rows carry no object storage and no history worth keeping, so they are removed
        // outright rather than soft-deleted: a soft delete would leave the growth this fixes.
        db.MeetingGuestSessions.RemoveRange(sessions);
        db.MeetingGuestChallenges.RemoveRange(challenges);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Purged {SessionCount} spent meeting guest sessions and {ChallengeCount} challenges older than {Cutoff:u}.",
            sessions.Count, challenges.Count, cutoff);
    }
}

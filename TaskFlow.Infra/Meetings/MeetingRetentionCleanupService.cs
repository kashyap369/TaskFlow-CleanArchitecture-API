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
        var ended = await db.Meetings.Where(x => x.Status == MeetingStatus.Ended && x.ActualEndUtc != null)
            .Select(x => new { x.Id, x.ActualEndUtc, x.RetentionDays }).ToListAsync(ct);
        var ids = ended.Where(x => x.ActualEndUtc!.Value.AddDays(x.RetentionDays) <= DateTime.UtcNow).Select(x => x.Id).ToList();
        foreach (var meetingId in ids)
        {
            var assets = await db.MeetingAssets.Where(x => x.MeetingId == meetingId).ToListAsync(ct);
            var recordings = await db.MeetingRecordings.Where(x => x.MeetingId == meetingId).ToListAsync(ct);
            var storageComplete = true;
            foreach (var asset in assets) try { await storage.DeleteAsync(asset.StorageKey, ct); }
                catch (Exception error) { storageComplete = false; logger.LogWarning(error, "Could not delete expired meeting object for meeting {MeetingId}.", meetingId); }
            foreach (var recording in recordings.Where(x => x.Status == MeetingRecordingStatus.Ready))
                try { await storage.DeleteAsync(recording.StorageKey, ct); }
                catch (Exception error) { storageComplete = false; logger.LogWarning(error, "Could not delete expired meeting recording for meeting {MeetingId}.", meetingId); }
            if (!storageComplete) continue;
            foreach (var asset in assets) asset.SoftDelete();
            foreach (var recording in recordings) recording.SoftDelete();
            foreach (var consent in await db.MeetingRecordingConsents.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) consent.SoftDelete();
            foreach (var message in await db.MeetingMessages.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) message.SoftDelete();
            foreach (var note in await db.MeetingNotes.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) note.SoftDelete();
            foreach (var revision in await db.MeetingNoteRevisions.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) revision.SoftDelete();
            foreach (var attendance in await db.MeetingAttendance.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) attendance.SoftDelete();
            await db.SaveChangesAsync(ct);
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

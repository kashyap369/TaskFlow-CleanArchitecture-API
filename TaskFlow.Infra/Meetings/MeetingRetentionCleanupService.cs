using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    internal async Task PurgeExpiredAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var ended = await db.Meetings.Where(x => x.Status == MeetingStatus.Ended && x.ActualEndUtc != null)
            .Select(x => new { x.Id, x.ActualEndUtc, x.RetentionDays }).ToListAsync(ct);
        var ids = ended.Where(x => x.ActualEndUtc!.Value.AddDays(x.RetentionDays) <= DateTime.UtcNow).Select(x => x.Id).ToList();
        foreach (var meetingId in ids)
        {
            var assets = await db.MeetingAssets.Where(x => x.MeetingId == meetingId).ToListAsync(ct);
            var storageComplete = true;
            foreach (var asset in assets) try { await storage.DeleteAsync(asset.StorageKey, ct); }
                catch (Exception error) { storageComplete = false; logger.LogWarning(error, "Could not delete expired meeting object for meeting {MeetingId}.", meetingId); }
            if (!storageComplete) continue;
            foreach (var asset in assets) asset.SoftDelete();
            foreach (var message in await db.MeetingMessages.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) message.SoftDelete();
            foreach (var note in await db.MeetingNotes.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) note.SoftDelete();
            foreach (var revision in await db.MeetingNoteRevisions.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) revision.SoftDelete();
            foreach (var attendance in await db.MeetingAttendance.Where(x => x.MeetingId == meetingId).ToListAsync(ct)) attendance.SoftDelete();
            await db.SaveChangesAsync(ct);
        }
    }
}

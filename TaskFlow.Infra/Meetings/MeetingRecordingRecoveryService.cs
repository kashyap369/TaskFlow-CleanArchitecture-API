using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Domain.Enums.Meetings;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Meetings;

public sealed class MeetingRecordingRecoveryService(IServiceScopeFactory scopes,
    ILogger<MeetingRecordingRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ReconcileAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Meeting recording reconciliation failed."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    internal async Task ReconcileAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<IMeetingMediaProvider>();
        var active = await db.MeetingRecordings.Include(x => x.Consents).Where(x =>
            x.Status == MeetingRecordingStatus.PendingConsent || x.Status == MeetingRecordingStatus.Starting ||
            x.Status == MeetingRecordingStatus.Recording || x.Status == MeetingRecordingStatus.Processing).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var recording in active)
        {
            if (recording.Status == MeetingRecordingStatus.PendingConsent) { recording.ExpireConsent(now); continue; }
            if (string.IsNullOrWhiteSpace(recording.ProviderEgressId) || !provider.IsEnabled) continue;
            var state = await provider.GetRoomRecordingStatusAsync(recording.ProviderEgressId, ct);
            if (state is null) continue;
            switch (state.State)
            {
                case MeetingEgressState.Recording: recording.MarkRecording(now); break;
                case MeetingEgressState.Processing: recording.MarkProcessing(now); break;
                case MeetingEgressState.Ready: recording.MarkReady(now, state.FileSize, state.DurationMilliseconds); break;
                case MeetingEgressState.Failed: recording.Fail(state.Error ?? "The recording provider reported a failure."); break;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Enums.Organization;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Attendance;

/// <summary>
/// Two sweeps: close sessions whose punch-out was forgotten, so they cannot block the next
/// punch-in or inflate a day; and delete punch selfies past the organization's retention period.
/// The attendance record itself is never deleted.
/// </summary>
public sealed class AttendanceMaintenanceService(IServiceScopeFactory scopes,
    ILogger<AttendanceMaintenanceService> logger) : BackgroundService
{
    private const int PhotoBatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CloseForgottenSessionsAsync(stoppingToken);
                await PurgeExpiredPhotosAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Attendance maintenance failed."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    /// <summary>Public so a test can drive one pass against a real database rather than waiting on the timer.</summary>
    public async Task CloseForgottenSessionsAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var now = DateTime.UtcNow;
        var open = await context.AttendanceSessions
            .Where(x => x.Status == AttendanceSessionStatus.Open && x.PunchInAtUtc < now.AddHours(-1))
            .ToListAsync(ct);
        if (open.Count == 0) return;

        var organizationIds = open.Select(x => x.OrganizationId).Distinct().ToList();
        var hoursByOrganization = await context.AttendanceSettings.AsNoTracking()
            .Where(x => organizationIds.Contains(x.OrganizationId))
            .ToDictionaryAsync(x => x.OrganizationId, x => x.AutoCloseAfterHours, ct);

        var closed = 0;
        foreach (var session in open)
        {
            var hours = hoursByOrganization.GetValueOrDefault(session.OrganizationId, AttendanceSettings.DefaultAutoCloseAfterHours);
            var cutoff = session.PunchInAtUtc.AddHours(hours);
            if (cutoff > now) continue;
            session.AutoClose(cutoff);
            closed++;
        }

        if (closed == 0) return;
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Attendance maintenance auto-closed {Count} forgotten sessions.", closed);
    }

    /// <summary>Public so a test can drive one pass against a real database rather than waiting on the timer.</summary>
    public async Task PurgeExpiredPhotosAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var now = DateTime.UtcNow;

        // The shortest retention any organization may set; nothing younger can be due.
        var candidates = await context.AttendanceSessions
            .Where(x => x.PhotosPurgedAtUtc == null && (x.PunchInPhotoKey != null || x.PunchOutPhotoKey != null) &&
                x.PunchInAtUtc < now.AddDays(-7))
            .OrderBy(x => x.PunchInAtUtc)
            .Take(PhotoBatchSize)
            .ToListAsync(ct);
        if (candidates.Count == 0) return;

        var organizationIds = candidates.Select(x => x.OrganizationId).Distinct().ToList();
        var daysByOrganization = await context.AttendanceSettings.AsNoTracking()
            .Where(x => organizationIds.Contains(x.OrganizationId))
            .ToDictionaryAsync(x => x.OrganizationId, x => x.PhotoRetentionDays, ct);

        var purged = 0;
        foreach (var session in candidates)
        {
            var days = daysByOrganization.GetValueOrDefault(session.OrganizationId, AttendanceSettings.DefaultPhotoRetentionDays);
            if (session.PunchInAtUtc > now.AddDays(-days)) continue;

            // Only forget the pointers once the objects are really gone; a failed delete is retried next pass.
            if (!await TryDeleteAsync(storage, session.PunchInPhotoKey, session.Id, ct) ||
                !await TryDeleteAsync(storage, session.PunchOutPhotoKey, session.Id, ct))
                continue;
            session.PurgePhotos(now);
            purged++;
        }

        if (purged == 0) return;
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Attendance maintenance removed selfies from {Count} sessions past retention.", purged);
    }

    private async Task<bool> TryDeleteAsync(IObjectStorage storage, string? key, int sessionId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(key)) return true;
        try
        {
            await storage.DeleteAsync(key, ct);
            return true;
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning(error, "Could not delete an expired attendance selfie for session {SessionId}.", sessionId);
            return false;
        }
    }
}

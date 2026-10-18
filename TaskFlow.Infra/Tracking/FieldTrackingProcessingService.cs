using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Contracts.Tracking;
using TaskFlow.Application.Features.Attendance;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Enums.Organization;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Tracking;

/// <summary>
/// Turns received GPS points into the route a manager sees: every few minutes, each session with
/// new points has its whole route rebuilt (clean → stops → pieces → road-matched) and replaced.
/// Rebuilding the whole session keeps the logic simple and the result consistent; a day is a few
/// thousand points, so this stays cheap at the planned scale (FIELD-TRACKING.md §6).
/// </summary>
public sealed class FieldTrackingProcessingService(IServiceScopeFactory scopes,
    ILogger<FieldTrackingProcessingService> logger) : BackgroundService
{
    private const int SessionsPerPass = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(3));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
                await PurgeExpiredPointsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Field tracking processing failed."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    /// <summary>Public so a test can drive one pass against a real database rather than waiting on the timer.</summary>
    public async Task ProcessPendingAsync(CancellationToken ct)
    {
        List<int> sessionIds;
        using (var scope = scopes.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
            sessionIds = await context.LocationPoints.AsNoTracking()
                .Where(x => !x.IsProcessed)
                .Select(x => x.AttendanceSessionId)
                .Distinct()
                .OrderBy(x => x)
                .Take(SessionsPerPass)
                .ToListAsync(ct);
        }

        foreach (var sessionId in sessionIds)
        {
            try
            {
                await ProcessSessionAsync(sessionId, ct);
            }
            catch (RouteMatcherUnavailableException error)
            {
                // The engine is down: leave the points unprocessed and try the whole lot next pass.
                logger.LogWarning(error, "Road matching unavailable; field tracking processing will retry.");
                return;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // One bad session must not hold up everyone else's routes.
                logger.LogError(error, "Field tracking could not process session {SessionId}.", sessionId);
            }
        }
    }

    public async Task ProcessSessionAsync(int sessionId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var matcher = scope.ServiceProvider.GetRequiredService<IRouteMatcher>();

        var session = await context.AttendanceSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        var points = await context.LocationPoints.AsNoTracking()
            .Where(x => x.AttendanceSessionId == sessionId)
            .OrderBy(x => x.RecordedAtUtc)
            .Select(x => new { x.Id, x.RecordedAtUtc, x.Latitude, x.Longitude, x.AccuracyMeters })
            .ToListAsync(ct);
        if (points.Count == 0) return;
        var lastId = points.Max(x => x.Id);

        var segments = new List<TrackedRouteSegment>();
        var stops = new List<TrackStop>();
        if (session is not null)
        {
            var plan = FieldTrackingRoutes.Build(points
                .Select(x => new TrackInputPoint(DateTime.SpecifyKind(x.RecordedAtUtc, DateTimeKind.Utc), x.Latitude,
                    x.Longitude, x.AccuracyMeters))
                .ToList());
            var now = DateTime.UtcNow;
            var sequence = 0;
            foreach (var planned in plan.Segments)
            {
                var matched = planned.Kind == TrackedSegmentKind.Recorded
                    ? await matcher.MatchAsync(planned.Points, ct)
                    : await matcher.RouteAsync(planned.Points[0], planned.Points[^1], ct);
                var polyline = matched?.EncodedPolyline ?? Polyline.Encode(planned.Points);
                var distance = matched?.DistanceMeters ?? FieldTrackingRoutes.LengthMeters(planned.Points);
                segments.Add(new TrackedRouteSegment(session.Id, session.OrganizationId, session.UserId, sequence++,
                    planned.Kind, planned.StartedAtUtc, planned.EndedAtUtc, polyline, distance,
                    planned.Kind == TrackedSegmentKind.Recorded ? planned.Points.Count : 0, matched is not null, now));
            }
            stops.AddRange(plan.Stops.Select(x => new TrackStop(session.Id, session.OrganizationId, session.UserId,
                x.Latitude, x.Longitude, x.ArrivedAtUtc, x.LeftAtUtc)));
        }

        // Replace the session's route and mark exactly the points it was built from, in one transaction,
        // so a point that arrived meanwhile stays queued for the next pass.
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await context.TrackedRouteSegments.Where(x => x.AttendanceSessionId == sessionId).ExecuteDeleteAsync(ct);
        await context.TrackStops.Where(x => x.AttendanceSessionId == sessionId).ExecuteDeleteAsync(ct);
        context.TrackedRouteSegments.AddRange(segments);
        context.TrackStops.AddRange(stops);
        await context.SaveChangesAsync(ct);
        await context.LocationPoints
            .Where(x => x.AttendanceSessionId == sessionId && !x.IsProcessed && x.Id <= lastId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.IsProcessed, true), ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Hard-deletes raw points past each organization's retention. Only processed points: their
    /// route already exists. The route, stops and distance are kept.
    /// </summary>
    public async Task PurgeExpiredPointsAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var now = DateTime.UtcNow;
        // The shortest retention any organization may set; nothing younger can be due.
        var earliestCutoff = now.AddDays(-7);
        var organizationIds = await context.LocationPoints.AsNoTracking()
            .Where(x => x.RecordedAtUtc < earliestCutoff)
            .Select(x => x.OrganizationId)
            .Distinct()
            .ToListAsync(ct);
        if (organizationIds.Count == 0) return;

        var daysByOrganization = await context.AttendanceSettings.AsNoTracking()
            .Where(x => organizationIds.Contains(x.OrganizationId))
            .ToDictionaryAsync(x => x.OrganizationId, x => x.LocationRetentionDays, ct);

        var deleted = 0;
        foreach (var organizationId in organizationIds)
        {
            var days = daysByOrganization.GetValueOrDefault(organizationId, AttendanceSettings.DefaultLocationRetentionDays);
            var cutoff = now.AddDays(-days);
            deleted += await context.LocationPoints
                .Where(x => x.OrganizationId == organizationId && x.IsProcessed && x.RecordedAtUtc < cutoff)
                .ExecuteDeleteAsync(ct);
        }
        if (deleted > 0) logger.LogInformation("Field tracking removed {Count} location points past retention.", deleted);
    }
}

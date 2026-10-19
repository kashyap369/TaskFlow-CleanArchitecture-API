using TaskFlow.Application.Contracts.Tracking;
using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Application.Features.Attendance;

public sealed record TrackInputPoint(DateTime RecordedAtUtc, double Latitude, double Longitude, double? AccuracyMeters);

/// <summary>A piece of route before it is matched to roads: the points it should follow.</summary>
public sealed record PlannedSegment(TrackedSegmentKind Kind, DateTime StartedAtUtc, DateTime EndedAtUtc,
    IReadOnlyList<GeoPoint> Points);

public sealed record PlannedStop(double Latitude, double Longitude, DateTime ArrivedAtUtc, DateTime LeftAtUtc);

public sealed record PlannedRoute(IReadOnlyList<PlannedSegment> Segments, IReadOnlyList<PlannedStop> Stops);

/// <summary>
/// Turns a session's raw GPS points into what the map draws: cleaned movement pieces (to be
/// matched to roads), gaps bridged as <see cref="TrackedSegmentKind.Inferred"/>, and stops.
/// Pure — no I/O — so every rule here is unit-testable. See Taskaro docs/FIELD-TRACKING.md §2.
/// </summary>
public static class FieldTrackingRoutes
{
    // A fix worse than this is more likely to mislead than to help (urban canyons, Wi-Fi/cell fallback).
    public const double MaxAccuracyMeters = 50;
    // Indoors a phone drifts further than StopRadiusMeters while its owner sits at one desk, which
    // used to break the stop run and leave "movement" for the matcher to snap onto nearby streets.
    // A stop therefore tolerates drift up to the accuracy the fixes themselves admit to.
    // ~250 km/h: anything faster between two fixes is a GPS jump, not travel.
    public const double MaxSpeedMetersPerSecond = 70;
    // Jitter while standing still; closer fixes add nothing to the line.
    public const double MinStepMeters = 5;
    public const double StopRadiusMeters = 100;
    public static readonly TimeSpan MinStopDuration = TimeSpan.FromMinutes(10);
    // No fix for this long is a gap: the phone was off, killed, or had no signal.
    public static readonly TimeSpan GapThreshold = TimeSpan.FromMinutes(5);
    // A gap that ends this close to where it began was spent in one place: no line is drawn.
    public const double MinGapBridgeMeters = 200;
    // Map matchers work best (and have limits) on bounded traces.
    public const int MaxPointsPerSegment = 1000;

    public static PlannedRoute Build(IReadOnlyList<TrackInputPoint> points)
    {
        var clean = Clean(points);
        var stops = FindStops(clean);
        var segments = Segment(clean, stops);
        return new PlannedRoute(segments, stops);
    }

    /// <summary>Ordered, deduplicated, inaccurate fixes and impossible jumps removed.</summary>
    public static List<TrackInputPoint> Clean(IReadOnlyList<TrackInputPoint> points)
    {
        var kept = new List<TrackInputPoint>();
        foreach (var point in points.OrderBy(x => x.RecordedAtUtc))
        {
            // A fix with no stated accuracy cannot be judged, and the ones that arrive that way are
            // the coarse network fixes, not GPS. Keeping them is what drew routes nobody walked.
            if (point.AccuracyMeters is not { } accuracy || !double.IsFinite(accuracy) || accuracy > MaxAccuracyMeters) continue;
            if (kept.Count > 0)
            {
                var previous = kept[^1];
                var seconds = (point.RecordedAtUtc - previous.RecordedAtUtc).TotalSeconds;
                if (seconds <= 0) continue;
                if (DistanceMeters(previous, point) / seconds > MaxSpeedMetersPerSecond) continue;
            }
            kept.Add(point);
        }
        return kept;
    }

    /// <summary>
    /// Runs of fixes that stay within <see cref="StopRadiusMeters"/> of where the run began for at
    /// least <see cref="MinStopDuration"/>. Gaps inside a run are fine — a phone at rest reports rarely.
    /// </summary>
    public static List<PlannedStop> FindStops(IReadOnlyList<TrackInputPoint> points)
    {
        var stops = new List<PlannedStop>();
        var start = 0;
        while (start < points.Count)
        {
            var end = start;
            double latitudeSum = points[start].Latitude, longitudeSum = points[start].Longitude;
            while (end + 1 < points.Count)
            {
                var count = end - start + 1;
                var centre = new GeoPoint(latitudeSum / count, longitudeSum / count);
                var next = points[end + 1];
                // A fix is only as sure of itself as its accuracy: one 40 m fix that lands 120 m
                // from the centre is still consistent with sitting at that centre. Judging it
                // against the bare radius is what split a morning at one desk into "movement".
                var tolerance = StopRadiusMeters + Math.Min(MaxAccuracyMeters, next.AccuracyMeters ?? 0);
                if (DistanceMeters(centre, new GeoPoint(next.Latitude, next.Longitude)) > tolerance)
                    break;
                end++;
                latitudeSum += points[end].Latitude;
                longitudeSum += points[end].Longitude;
            }

            if (points[end].RecordedAtUtc - points[start].RecordedAtUtc >= MinStopDuration)
            {
                var count = end - start + 1;
                stops.Add(new PlannedStop(latitudeSum / count, longitudeSum / count,
                    points[start].RecordedAtUtc, points[end].RecordedAtUtc));
                start = end + 1;
            }
            else
            {
                start++;
            }
        }
        return stops;
    }

    /// <summary>
    /// Movement between stops becomes recorded segments; a stop collapses to its centre so the
    /// matcher never sees its jitter; a long silence becomes an inferred bridge (or nothing, if
    /// the member ended up where they were).
    /// </summary>
    public static List<PlannedSegment> Segment(IReadOnlyList<TrackInputPoint> points, IReadOnlyList<PlannedStop> stops)
    {
        // The stream the line is drawn from: moving fixes, and each stop as its centre at arrival and departure.
        var stream = new List<(DateTime At, GeoPoint Point, bool AtStop)>();
        var stopIndex = 0;
        foreach (var point in points)
        {
            while (stopIndex < stops.Count && stops[stopIndex].LeftAtUtc < point.RecordedAtUtc) stopIndex++;
            if (stopIndex < stops.Count && point.RecordedAtUtc >= stops[stopIndex].ArrivedAtUtc &&
                point.RecordedAtUtc <= stops[stopIndex].LeftAtUtc)
            {
                var stop = stops[stopIndex];
                var centre = new GeoPoint(stop.Latitude, stop.Longitude);
                if (point.RecordedAtUtc == stop.ArrivedAtUtc) stream.Add((stop.ArrivedAtUtc, centre, true));
                if (point.RecordedAtUtc == stop.LeftAtUtc) stream.Add((stop.LeftAtUtc, centre, true));
                continue;
            }
            var here = new GeoPoint(point.Latitude, point.Longitude);
            if (stream.Count > 0 && !stream[^1].AtStop && DistanceMeters(stream[^1].Point, here) < MinStepMeters) continue;
            stream.Add((point.RecordedAtUtc, here, false));
        }

        var segments = new List<PlannedSegment>();
        var piece = new List<(DateTime At, GeoPoint Point, bool AtStop)>();
        for (var i = 0; i < stream.Count; i++)
        {
            var current = stream[i];
            if (piece.Count > 0)
            {
                var previous = piece[^1];
                var silence = current.At - previous.At;
                // Inside one stop, silence is expected; anywhere else it is a gap.
                var sameStop = previous.AtStop && current.AtStop && previous.Point == current.Point;
                if (silence >= GapThreshold && !sameStop)
                {
                    FlushRecorded(piece, segments);
                    if (DistanceMeters(previous.Point, current.Point) >= MinGapBridgeMeters)
                        segments.Add(new PlannedSegment(TrackedSegmentKind.Inferred, previous.At, current.At,
                            [previous.Point, current.Point]));
                    piece.Clear();
                }
                else if (sameStop)
                {
                    // Arrival and departure at one stop: the line pauses, it does not move.
                    FlushRecorded(piece, segments);
                    piece.Clear();
                }
            }
            piece.Add(current);
        }
        FlushRecorded(piece, segments);
        return segments;
    }

    private static void FlushRecorded(List<(DateTime At, GeoPoint Point, bool AtStop)> piece, List<PlannedSegment> segments)
    {
        if (piece.Count < 2) return;
        // Chunks share their boundary point so the drawn line stays continuous.
        for (var start = 0; start < piece.Count - 1; start += MaxPointsPerSegment - 1)
        {
            var chunk = piece.Skip(start).Take(MaxPointsPerSegment).ToList();
            if (chunk.Count < 2) break;
            segments.Add(new PlannedSegment(TrackedSegmentKind.Recorded, chunk[0].At, chunk[^1].At,
                chunk.Select(x => x.Point).ToList()));
        }
    }

    public static double LengthMeters(IReadOnlyList<GeoPoint> points)
    {
        double total = 0;
        for (var i = 1; i < points.Count; i++) total += DistanceMeters(points[i - 1], points[i]);
        return total;
    }

    public static double DistanceMeters(TrackInputPoint a, TrackInputPoint b) =>
        DistanceMeters(new GeoPoint(a.Latitude, a.Longitude), new GeoPoint(b.Latitude, b.Longitude));

    /// <summary>Haversine on a spherical Earth — ample at city scale.</summary>
    public static double DistanceMeters(GeoPoint a, GeoPoint b)
    {
        const double earthRadius = 6_371_000;
        var dLat = ToRadians(b.Latitude - a.Latitude);
        var dLon = ToRadians(b.Longitude - a.Longitude);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRadians(a.Latitude)) * Math.Cos(ToRadians(b.Latitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * earthRadius * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

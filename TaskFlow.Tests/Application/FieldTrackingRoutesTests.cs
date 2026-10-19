using TaskFlow.Application.Contracts.Tracking;
using TaskFlow.Application.Features.Attendance;
using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Tests.Application;

public sealed class FieldTrackingRoutesTests
{
    // Mumbai-ish, so a degree of longitude is noticeably shorter than one of latitude.
    private const double BaseLatitude = 19.07;
    private const double BaseLongitude = 72.88;
    private static readonly DateTime Start = new(2026, 10, 18, 4, 0, 0, DateTimeKind.Utc);

    private static TrackInputPoint At(TimeSpan offset, double eastMeters, double northMeters, double? accuracy = 8) =>
        new(Start + offset, BaseLatitude + northMeters / 111_320.0,
            BaseLongitude + eastMeters / (111_320.0 * Math.Cos(BaseLatitude * Math.PI / 180)), accuracy);

    private static GeoPoint Geo(double eastMeters, double northMeters) =>
        new(BaseLatitude + northMeters / 111_320.0,
            BaseLongitude + eastMeters / (111_320.0 * Math.Cos(BaseLatitude * Math.PI / 180)));

    /// <summary>Ten minutes driving east, fifteen minutes parked, twenty minutes of silence, two minutes driving north.</summary>
    private static List<TrackInputPoint> Day()
    {
        var points = new List<TrackInputPoint>();
        for (var i = 0; i <= 120; i++) points.Add(At(TimeSpan.FromSeconds(5 * i), 50 * i, 0));
        for (var k = 1; k <= 15; k++) points.Add(At(TimeSpan.FromMinutes(10 + k), 6000 + (k % 3) * 4, (k % 2) * 3));
        for (var i = 0; i <= 24; i++) points.Add(At(TimeSpan.FromMinutes(45) + TimeSpan.FromSeconds(5 * i), 6000, 2000 + 50 * i));
        return points;
    }

    [Fact]
    public void Clean_DropsInaccurateFixesAndImpossibleJumps()
    {
        var points = new List<TrackInputPoint>
        {
            At(TimeSpan.Zero, 0, 0),
            At(TimeSpan.FromSeconds(5), 50, 0, accuracy: 120),
            At(TimeSpan.FromSeconds(10), 5000, 0),
            At(TimeSpan.FromSeconds(15), 100, 0),
            At(TimeSpan.FromSeconds(15), 100, 0)
        };

        var clean = FieldTrackingRoutes.Clean(points);

        Assert.Equal(2, clean.Count);
        Assert.Equal(Start, clean[0].RecordedAtUtc);
        Assert.Equal(Start.AddSeconds(15), clean[1].RecordedAtUtc);
    }

    [Fact]
    public void FindStops_FindsTheParkedStretch()
    {
        var stops = FieldTrackingRoutes.FindStops(FieldTrackingRoutes.Clean(Day()));

        var stop = Assert.Single(stops);
        var minutes = (stop.LeftAtUtc - stop.ArrivedAtUtc).TotalMinutes;
        Assert.InRange(minutes, 15, 16);
        Assert.Equal(Start.AddMinutes(25), stop.LeftAtUtc);
    }

    [Fact]
    public void Build_DrawsMovement_BridgesTheGapAsInferred_AndNeverDrawsTheParkedJitter()
    {
        var route = FieldTrackingRoutes.Build(Day());

        Assert.Equal(
            new[] { TrackedSegmentKind.Recorded, TrackedSegmentKind.Inferred, TrackedSegmentKind.Recorded },
            route.Segments.Select(x => x.Kind).ToArray());
        var inferred = route.Segments[1];
        Assert.Equal(Start.AddMinutes(25), inferred.StartedAtUtc);
        Assert.Equal(Start.AddMinutes(45), inferred.EndedAtUtc);
        Assert.Equal(2, inferred.Points.Count);
        Assert.InRange(FieldTrackingRoutes.LengthMeters(inferred.Points), 1990, 2010);
        // The drive east is ~6 km; the parked jitter adds nothing to it.
        Assert.InRange(FieldTrackingRoutes.LengthMeters(route.Segments[0].Points), 5800, 6100);
    }

    [Fact]
    public void Build_LeavesNoLineForAGapThatEndsNearWhereItBegan()
    {
        // 150 m apart after half an hour: too far to be one stop, too close to be worth a guessed road.
        var points = new List<TrackInputPoint>
        {
            At(TimeSpan.Zero, 0, 0),
            At(TimeSpan.FromSeconds(5), 50, 0),
            At(TimeSpan.FromMinutes(30), 200, 0),
            At(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(5), 250, 0)
        };

        var route = FieldTrackingRoutes.Build(points);

        Assert.All(route.Segments, x => Assert.Equal(TrackedSegmentKind.Recorded, x.Kind));
        Assert.Equal(2, route.Segments.Count);
    }

    [Fact]
    public void Build_SplitsLongPiecesSoTheyShareTheirBoundaryPoint()
    {
        var points = Enumerable.Range(0, 2500).Select(i => At(TimeSpan.FromSeconds(2 * i), 20 * i, 0)).ToList();

        var segments = FieldTrackingRoutes.Build(points).Segments;

        Assert.Equal(3, segments.Count);
        Assert.All(segments, x => Assert.True(x.Points.Count <= FieldTrackingRoutes.MaxPointsPerSegment));
        Assert.Equal(segments[0].Points[^1], segments[1].Points[0]);
        Assert.Equal(segments[0].EndedAtUtc, segments[1].StartedAtUtc);
    }

    [Fact]
    public void Polyline_RoundTripsAtSixDecimals()
    {
        var points = new List<GeoPoint> { new(19.076090, 72.877426), new(19.054999, 72.840278), new(-33.856784, 151.215297) };

        var decoded = Polyline.Decode(Polyline.Encode(points));

        Assert.Equal(points.Count, decoded.Count);
        for (var i = 0; i < points.Count; i++)
        {
            Assert.Equal(points[i].Latitude, decoded[i].Latitude, 6);
            Assert.Equal(points[i].Longitude, decoded[i].Longitude, 6);
        }
    }

    [Fact]
    public void Clean_DropsFixesThatStateNoAccuracy()
    {
        // The coarse network-provider fixes arrive this way; GPS always states its accuracy.
        var points = new List<TrackInputPoint>
        {
            At(TimeSpan.Zero, 0, 0),
            At(TimeSpan.FromSeconds(5), 30, 0, accuracy: null),
            At(TimeSpan.FromSeconds(10), 60, 0)
        };

        var clean = FieldTrackingRoutes.Clean(points);

        Assert.Equal(2, clean.Count);
        Assert.All(clean, x => Assert.NotNull(x.AccuracyMeters));
    }

    [Fact]
    public void FindStops_KeepsOneStop_WhenIndoorDriftExceedsTheBareRadius()
    {
        // A morning at one desk: fixes admit to 45 m of error and wander further than the bare
        // 100 m radius. Judged against the radius alone this split into "movement" and was then
        // snapped onto nearby streets — the phantom office route.
        var points = new List<TrackInputPoint>();
        for (var minute = 0; minute <= 30; minute++)
        {
            var east = minute % 2 == 0 ? 0 : 130;
            points.Add(At(TimeSpan.FromMinutes(minute), east, 0, accuracy: 45));
        }

        var stop = Assert.Single(FieldTrackingRoutes.FindStops(FieldTrackingRoutes.Clean(points)));

        Assert.Equal(Start, stop.ArrivedAtUtc);
        Assert.Equal(Start.AddMinutes(30), stop.LeftAtUtc);
    }

    [Fact]
    public void FindStops_StillSeesRealMovementAsMovement()
    {
        // The tolerance must not swallow someone actually leaving: accurate fixes walking away
        // stay movement, so no stop is found.
        var points = new List<TrackInputPoint>();
        for (var minute = 0; minute <= 30; minute++)
        {
            points.Add(At(TimeSpan.FromMinutes(minute), 200 * minute, 0, accuracy: 8));
        }

        Assert.Empty(FieldTrackingRoutes.FindStops(FieldTrackingRoutes.Clean(points)));
    }

    [Fact]
    public void ModeFor_CallsAStrollWalkingAndACommuteDriving()
    {
        // 1.3 m/s over ten minutes: a walk.
        var walk = new PlannedSegment(TrackedSegmentKind.Recorded, Start, Start.AddMinutes(10),
            [Geo(0, 0), Geo(780, 0)]);
        // 11 m/s over the same ten minutes: a vehicle.
        var drive = new PlannedSegment(TrackedSegmentKind.Recorded, Start, Start.AddMinutes(10),
            [Geo(0, 0), Geo(6600, 0)]);

        Assert.Equal(RouteTravelMode.Walking, FieldTrackingRoutes.ModeFor(walk));
        Assert.Equal(RouteTravelMode.Driving, FieldTrackingRoutes.ModeFor(drive));
    }

    [Fact]
    public void MatchExplainsPoints_AcceptsARoadMatchThatHugsTheTrack()
    {
        // A real road match sits within a few metres of the fixes it explains.
        var recorded = new List<GeoPoint>();
        var matched = new List<GeoPoint>();
        for (var i = 0; i <= 50; i++)
        {
            recorded.Add(Geo(40 * i, 0));
            matched.Add(Geo(40 * i, 6));
        }

        Assert.True(FieldTrackingRoutes.MatchExplainsPoints(recorded, matched));
    }

    [Fact]
    public void MatchExplainsPoints_RejectsATrainSnappedOntoTheRoadBesideTheRailway()
    {
        // Valhalla never refuses: it answers with the best road it can find, so a local train
        // comes back snapped onto the highway running parallel, a few hundred metres away.
        var recorded = new List<GeoPoint>();
        var matched = new List<GeoPoint>();
        for (var i = 0; i <= 50; i++)
        {
            recorded.Add(Geo(40 * i, 0));
            matched.Add(Geo(40 * i, 300));
        }

        Assert.False(FieldTrackingRoutes.MatchExplainsPoints(recorded, matched));
        Assert.InRange(FieldTrackingRoutes.MedianDeviationMeters(recorded, matched), 295, 305);
    }

    [Fact]
    public void MatchExplainsPoints_IsNotFooledByOneStrayFix()
    {
        // One fix off a roof should not throw away an otherwise good match, which is why the
        // measure is the median rather than the worst case.
        var recorded = new List<GeoPoint>();
        var matched = new List<GeoPoint>();
        for (var i = 0; i <= 50; i++)
        {
            recorded.Add(Geo(40 * i, i == 25 ? 900 : 0));
            matched.Add(Geo(40 * i, 5));
        }

        Assert.True(FieldTrackingRoutes.MatchExplainsPoints(recorded, matched));
    }
}

using TaskFlow.Application.Features.Attendance;
using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Tests.Application;

/// <summary>
/// The two pieces of the attendance map's reads that are arithmetic rather than SQL: the battery a
/// punch pin shows, and how a shift's thousands of fixes are thinned before they are sent.
/// </summary>
public sealed class FieldTrackingMapReadTests
{
    private static readonly DateTime NineAm = new(2026, 10, 19, 3, 30, 0, DateTimeKind.Utc);

    private static GetFieldTrackingDayQueryHandler.SessionRow Session(DateTime? punchOut = null, bool photos = true) => new()
    {
        Id = 7,
        PunchInAtUtc = NineAm,
        PunchOutAtUtc = punchOut,
        Status = punchOut is null ? AttendanceSessionStatus.Open : AttendanceSessionStatus.Closed,
        IsTracked = true,
        PunchInLatitude = 19.07,
        PunchInLongitude = 72.88,
        PunchInAccuracyMeters = 12,
        PunchOutLatitude = punchOut is null ? null : 19.1,
        PunchOutLongitude = punchOut is null ? null : 72.9,
        PunchInNote = "At the depot",
        HasPunchInPhoto = photos,
        HasPunchOutPhoto = photos && punchOut is not null
    };

    private static GetFieldTrackingDayQueryHandler.PointRow Point(DateTime at, int? battery, bool charging = false,
        int sessionId = 7) => new()
    {
        AttendanceSessionId = sessionId,
        RecordedAtUtc = at,
        Latitude = 19.07,
        Longitude = 72.88,
        BatteryPercent = battery,
        IsCharging = charging
    };

    [Fact]
    public void PunchDetail_TakesTheBatteryOfTheNearestFixToThePunch()
    {
        var points = new[]
        {
            Point(NineAm.AddMinutes(-9), 95),
            Point(NineAm.AddMinutes(2), 81),
            Point(NineAm.AddMinutes(30), 74)
        };

        var detail = GetFieldTrackingDayQueryHandler.PunchDetail(Session(), "in", points);

        Assert.NotNull(detail);
        Assert.Equal(81, detail!.BatteryPercent);
        Assert.Equal("in", detail.Kind);
        Assert.True(detail.HasPhoto);
        Assert.Equal(12, detail.AccuracyMeters);
        Assert.Equal("At the depot", detail.Note);
    }

    [Fact]
    public void PunchDetail_LeavesBatteryBlankWhenEveryFixIsFarFromThePunch()
    {
        // Half an hour later is not "the battery at the punch" — a blank is honest, a guess is not.
        var points = new[] { Point(NineAm.AddMinutes(31), 74) };

        var detail = GetFieldTrackingDayQueryHandler.PunchDetail(Session(), "in", points);

        Assert.NotNull(detail);
        Assert.Null(detail!.BatteryPercent);
    }

    [Fact]
    public void PunchDetail_IgnoresAnotherSessionsFixes()
    {
        var points = new[] { Point(NineAm.AddMinutes(1), 42, sessionId: 8) };

        Assert.Null(GetFieldTrackingDayQueryHandler.PunchDetail(Session(), "in", points)!.BatteryPercent);
    }

    [Fact]
    public void PunchDetail_IsNullForThePunchOutOfAnOpenSession()
    {
        Assert.Null(GetFieldTrackingDayQueryHandler.PunchDetail(Session(), "out", Array.Empty<GetFieldTrackingDayQueryHandler.PointRow>()));
    }

    [Fact]
    public void PunchDetail_ReadsThePunchOutSideWhenTheSessionIsClosed()
    {
        var punchOut = NineAm.AddHours(8);
        var points = new[] { Point(punchOut.AddMinutes(-1), 35) };

        var detail = GetFieldTrackingDayQueryHandler.PunchDetail(Session(punchOut), "out", points);

        Assert.NotNull(detail);
        Assert.Equal(punchOut, detail!.AtUtc);
        Assert.Equal(19.1, detail.Latitude);
        Assert.Equal(35, detail.BatteryPercent);
        Assert.True(detail.HasPhoto);
        Assert.Null(detail.Note);
    }

    [Fact]
    public void Sample_KeepsEverythingWhenThereIsLittle()
    {
        var all = Enumerable.Range(0, 120).ToList();

        Assert.Equal(all, GetFieldTrackingPointsQueryHandler.Sample(all, 400));
    }

    [Fact]
    public void Sample_ThinsAShiftToTheCapAndKeepsBothEnds()
    {
        // Eight hours at one fix every five seconds.
        var all = Enumerable.Range(0, 5_760).ToList();

        var kept = GetFieldTrackingPointsQueryHandler.Sample(all, 400);

        Assert.InRange(kept.Count, 1, 401);
        Assert.Equal(0, kept[0]);
        Assert.Equal(5_759, kept[^1]);
        // Evenly spread: every step but the last is the same stride.
        var strides = kept.Zip(kept.Skip(1), (a, b) => b - a).ToList();
        Assert.Single(strides.SkipLast(1).Distinct());
    }

    [Fact]
    public void Sample_DoesNotRepeatTheLastFixWhenTheStrideAlreadyLandsOnIt()
    {
        var all = Enumerable.Range(0, 9).ToList();

        var kept = GetFieldTrackingPointsQueryHandler.Sample(all, 5);

        Assert.Equal(new[] { 0, 2, 4, 6, 8 }, kept);
    }

    [Fact]
    public void Sample_RejectsANonPositiveCap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GetFieldTrackingPointsQueryHandler.Sample(new[] { 1, 2 }, 0));
    }
}

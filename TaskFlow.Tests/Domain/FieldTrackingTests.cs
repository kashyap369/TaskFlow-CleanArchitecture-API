using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Tests.Domain;

public sealed class FieldTrackingTests
{
    private static readonly DateTime NineAm = new(2026, 10, 18, 3, 30, 0, DateTimeKind.Utc);
    private static AttendancePhoto Selfie(string kind) => new($"attendance/1/7/x-{kind}", "image/jpeg", 2048, new string('a', 64));

    [Fact]
    public void PunchIn_RecordsWhereAndWhetherItIsTracked()
    {
        var session = AttendanceSession.PunchIn(1, 7, new DateOnly(2026, 10, 18), NineAm, null, Selfie("in"),
            new PunchLocation(19.07, 72.88, 12), isTracked: true);
        session.PunchOut(NineAm.AddHours(8), null, Selfie("out"), new PunchLocation(19.1, 72.9, null));

        Assert.True(session.IsTracked);
        Assert.Equal(19.07, session.PunchInLatitude!.Value);
        Assert.Equal(12.0, session.PunchInAccuracyMeters!.Value);
        Assert.Equal(72.9, session.PunchOutLongitude!.Value);
        Assert.Null(session.PunchOutAccuracyMeters);
    }

    [Fact]
    public void PunchIn_WithoutLocation_IsUntracked()
    {
        var session = AttendanceSession.PunchIn(1, 7, new DateOnly(2026, 10, 18), NineAm, null, Selfie("in"));
        Assert.False(session.IsTracked);
        Assert.Null(session.PunchInLatitude);
    }

    [Fact]
    public void PunchLocation_RejectsImpossibleCoordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PunchLocation(91, 0, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PunchLocation(0, 181, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PunchLocation(0, 0, -1));
    }

    [Fact]
    public void LocationPoint_KeepsTheFix_AndDropsMeaninglessExtras()
    {
        var point = new LocationPoint(5, 1, 7, NineAm, NineAm.AddMinutes(2), 19.07, 72.88, double.NaN, -1, 90,
            isMock: true, batteryPercent: 64, isCharging: false, isBatterySaverOn: null);

        Assert.Null(point.AccuracyMeters);
        Assert.Null(point.SpeedMetersPerSecond);
        Assert.Equal(90.0, point.HeadingDegrees!.Value);
        Assert.True(point.IsMock);
        Assert.False(point.IsProcessed);
        point.MarkProcessed();
        Assert.True(point.IsProcessed);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LocationPoint(5, 1, 7, NineAm, NineAm, 19, 72, null, null, null, false, 101, null, null));
    }

    [Fact]
    public void Settings_TrackingIsOffByDefault_AndRetentionIsBounded()
    {
        var settings = new AttendanceSettings(1, "Asia/Kolkata", 16, 90, 7);
        Assert.False(settings.TrackingEnabled);
        Assert.Equal(AttendanceSettings.DefaultLocationRetentionDays, settings.LocationRetentionDays);

        settings.UpdateTracking(true, 14, 7);
        Assert.True(settings.TrackingEnabled);
        Assert.Equal(14, settings.LocationRetentionDays);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.UpdateTracking(true, 3, 7));
    }

    [Fact]
    public void Consent_WithdrawIsASoftDelete()
    {
        var consent = new FieldTrackingConsent(1, 7, 1, NineAm);
        consent.Withdraw();
        Assert.True(consent.IsDeleted);
    }
}

using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Tests.Domain;

public sealed class AttendanceSessionTests
{
    private static readonly DateTime NineAm = new(2026, 10, 18, 3, 30, 0, DateTimeKind.Utc);
    private static AttendancePhoto Selfie(string kind) => new($"attendance/1/7/x-{kind}", "image/jpeg", 2048, new string('a', 64));

    [Fact]
    public void PunchIn_OpensWithItsSelfie()
    {
        var session = AttendanceSession.PunchIn(1, 7, new DateOnly(2026, 10, 18), NineAm, " on site ", Selfie("in"));
        Assert.True(session.IsOpen);
        Assert.Null(session.PunchOutAtUtc);
        Assert.Equal("attendance/1/7/x-in", session.PunchInPhotoKey);
        Assert.Equal("on site", session.PunchInNote);
    }

    [Fact]
    public void PunchOut_ClosesOnce_AndNeverBeforePunchIn()
    {
        var session = AttendanceSession.PunchIn(1, 7, new DateOnly(2026, 10, 18), NineAm, null, Selfie("in"));
        Assert.Throws<ArgumentException>(() => session.PunchOut(NineAm.AddMinutes(-1), null, Selfie("out")));
        session.PunchOut(NineAm.AddHours(8), null, Selfie("out"));
        Assert.Equal(AttendanceSessionStatus.Closed, session.Status);
        Assert.Equal("attendance/1/7/x-out", session.PunchOutPhotoKey);
        Assert.Throws<InvalidOperationException>(() => session.PunchOut(NineAm.AddHours(9), null, Selfie("out")));
    }

    [Fact]
    public void AutoClose_IsMarkedAsSuch_AndLeavesClosedSessionsAlone()
    {
        var session = AttendanceSession.PunchIn(1, 7, new DateOnly(2026, 10, 18), NineAm, null, Selfie("in"));
        session.AutoClose(NineAm.AddHours(16));
        Assert.Equal(AttendanceSessionStatus.AutoClosed, session.Status);
        Assert.Equal(NineAm.AddHours(16), session.PunchOutAtUtc);
        session.AutoClose(NineAm.AddHours(20));
        Assert.Equal(NineAm.AddHours(16), session.PunchOutAtUtc);
    }

    [Fact]
    public void Adjust_KeepsTheFirstOriginals_AndNeedsAReason()
    {
        var session = AttendanceSession.PunchIn(1, 7, new DateOnly(2026, 10, 18), NineAm, null, Selfie("in"));
        session.AutoClose(NineAm.AddHours(16));
        Assert.Throws<ArgumentException>(() => session.Adjust(NineAm, NineAm.AddHours(8), " ", 2, DateTime.UtcNow));

        session.Adjust(NineAm, NineAm.AddHours(8), "Forgot to punch out", 2, DateTime.UtcNow);
        session.Adjust(NineAm, NineAm.AddHours(7), "Left early", 2, DateTime.UtcNow);

        Assert.Equal(AttendanceSessionStatus.Adjusted, session.Status);
        Assert.Equal(NineAm.AddHours(16), session.OriginalPunchOutAtUtc);
        Assert.Equal("Left early", session.AdjustmentReason);
    }

    [Fact]
    public void PurgePhotos_ForgetsSelfies_ButKeepsTheRecord()
    {
        var session = AttendanceSession.PunchIn(1, 7, new DateOnly(2026, 10, 18), NineAm, null, Selfie("in"));
        session.PunchOut(NineAm.AddHours(8), null, Selfie("out"));
        session.PurgePhotos(NineAm.AddDays(91));
        Assert.Null(session.PunchInPhotoKey);
        Assert.Null(session.PunchOutPhotoKey);
        Assert.NotNull(session.PhotosPurgedAtUtc);
        Assert.Equal(NineAm.AddHours(8), session.PunchOutAtUtc);
    }

    [Fact]
    public void Settings_RejectOutOfRangeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttendanceSettings(1, "Asia/Kolkata", 0, 90, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttendanceSettings(1, "Asia/Kolkata", 16, 3, 2));
        var settings = new AttendanceSettings(1, " Asia/Kolkata ", 16, 90, 2);
        Assert.Equal("Asia/Kolkata", settings.TimeZone);
    }
}

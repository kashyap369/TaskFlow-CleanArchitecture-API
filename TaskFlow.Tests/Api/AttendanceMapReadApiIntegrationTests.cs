using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Tests.Api;

/// <summary>
/// The three reads the attendance map was given, over HTTP against a real migrated database.
/// They are Dapper, so nothing but this proves the quoted identifiers, the joins and the
/// work-date filter: a typo in raw SQL is invisible to the compiler and to every unit test.
/// </summary>
public sealed class AttendanceMapReadApiIntegrationTests : IClassFixture<PlannerApiFixture>
{
    private readonly PlannerApiFixture _fixture;

    // The organization's time zone defaults to UTC with no settings row, so the work date is the UTC date.
    private static readonly DateOnly WorkDate = new(2026, 6, 15);
    private static readonly DateTime PunchIn = new(2026, 6, 15, 4, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PunchOut = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
    private const int SeededPoints = 2_880;

    private int _sessionId;

    public AttendanceMapReadApiIntegrationTests(PlannerApiFixture fixture)
    {
        _fixture = fixture;
    }

    private static AttendancePhoto Selfie(string kind) =>
        new($"attendance/map-read/{kind}.jpg", "image/jpeg", 4_096, new string('b', 64));

    /// <summary>
    /// One tracked shift for the organization's non-owner member: punched in and out with a selfie
    /// and a location, and a realistic number of fixes (one every ten seconds for eight hours).
    /// </summary>
    private async Task<int> SeedShiftAsync()
    {
        if (_sessionId != 0) return _sessionId;
        _sessionId = await _fixture.WithDbContextAsync(async context =>
        {
            var existing = await context.AttendanceSessions
                .FirstOrDefaultAsync(x => x.OrganizationId == _fixture.CapacityOrganizationId
                    && x.UserId == _fixture.CapacityMemberUserId && x.WorkDate == WorkDate);
            if (existing is not null) return existing.Id;

            var session = AttendanceSession.PunchIn(_fixture.CapacityOrganizationId, _fixture.CapacityMemberUserId,
                WorkDate, PunchIn, "Starting the round", Selfie("in"), new PunchLocation(19.07, 72.88, 14),
                isTracked: true);
            session.PunchOut(PunchOut, null, Selfie("out"), new PunchLocation(19.11, 72.91, 22));
            session.ClearDomainEvents();
            context.AttendanceSessions.Add(session);
            await context.SaveChangesAsync();

            var points = new List<LocationPoint>(SeededPoints);
            for (var i = 0; i < SeededPoints; i++)
            {
                var at = PunchIn.AddSeconds(i * 10);
                points.Add(new LocationPoint(session.Id, _fixture.CapacityOrganizationId,
                    _fixture.CapacityMemberUserId, at, at.AddMinutes(2), 19.07 + i * 0.000_01,
                    72.88 + i * 0.000_01, 8, 2.4, 95, isMock: false,
                    // 100 % at the punch, falling a point every 36 fixes — so the pin's battery is
                    // the one recorded next to the punch, not the day's average.
                    batteryPercent: Math.Max(0, 100 - i / 36), isCharging: false, isBatterySaverOn: false));
            }
            context.LocationPoints.AddRange(points);
            await context.SaveChangesAsync();
            return session.Id;
        });
        return _sessionId;
    }

    [Fact]
    public async Task AttendedMembers_ListsOnlyWhoPunchedInOnThatDate()
    {
        await SeedShiftAsync();
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);

        var day = await owner.GetFromJsonAsync<JsonObject>(
            $"/api/attendance/organization/{_fixture.CapacityOrganizationId}/attended-members?date={WorkDate:yyyy-MM-dd}");
        var quietDay = await owner.GetFromJsonAsync<JsonObject>(
            $"/api/attendance/organization/{_fixture.CapacityOrganizationId}/attended-members?date={WorkDate.AddDays(-1):yyyy-MM-dd}");

        Assert.NotNull(day);
        var members = day!["members"]!.AsArray();
        var member = Assert.Single(members);
        Assert.Equal(_fixture.CapacityMemberUserId, (int)member!["userId"]!);
        Assert.Equal("Ben Shah", (string?)member["fullName"]);
        Assert.Equal("Planner", (string?)member["roleName"]);
        Assert.Equal(1, (int)member["sessionCount"]!);
        Assert.False((bool)member["isStillIn"]!);
        Assert.True((bool)member["wasTracked"]!);
        // The organization has two members and an owner; only one of them was ever at work that day.
        Assert.Empty(quietDay!["members"]!.AsArray());
    }

    [Fact]
    public async Task AttendedMembers_AreRefusedToSomeoneOutsideTheOrganization()
    {
        await SeedShiftAsync();
        using var outsider = _fixture.CreateClient(_fixture.OtherOrganizationOwnerUserId);

        var response = await outsider.GetAsync(
            $"/api/attendance/organization/{_fixture.CapacityOrganizationId}/attended-members?date={WorkDate:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MemberDay_CarriesThePunchDetailThePinShows()
    {
        var sessionId = await SeedShiftAsync();
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);

        var day = await owner.GetFromJsonAsync<JsonObject>(
            $"/api/attendance/organization/{_fixture.CapacityOrganizationId}/tracking/member/{_fixture.CapacityMemberUserId}?date={WorkDate:yyyy-MM-dd}");

        Assert.NotNull(day);
        Assert.Equal("ben.capacity@example.test", (string?)day!["email"]);
        var session = Assert.Single(day["sessions"]!.AsArray());
        Assert.Equal(sessionId, (int)session!["attendanceSessionId"]!);

        var punchIn = session["punchIn"]!;
        Assert.Equal("in", (string?)punchIn["kind"]);
        Assert.Equal(19.07, (double)punchIn["latitude"]!, 5);
        Assert.Equal(14, (double)punchIn["accuracyMeters"]!, 5);
        Assert.True((bool)punchIn["hasPhoto"]!);
        Assert.Equal(100, (int)punchIn["batteryPercent"]!);
        Assert.Equal("Starting the round", (string?)punchIn["note"]);

        var punchOut = session["punchOut"]!;
        Assert.Equal("out", (string?)punchOut["kind"]);
        Assert.Equal(22, (double)punchOut["accuracyMeters"]!, 5);
        Assert.True((bool)punchOut["hasPhoto"]!);
        // The last fix lands ten seconds before punch-out, by then down to 21 %.
        Assert.Equal(21, (int)punchOut["batteryPercent"]!);
        Assert.Null((string?)punchOut["note"]);
    }

    [Fact]
    public async Task Points_AreSampledNotShippedWhole_AndSayHowManyWereKept()
    {
        var sessionId = await SeedShiftAsync();
        using var owner = _fixture.CreateClient(_fixture.CapacityOwnerUserId);

        var points = await owner.GetFromJsonAsync<JsonObject>(
            $"/api/attendance/organization/{_fixture.CapacityOrganizationId}/tracking/member/{_fixture.CapacityMemberUserId}/points?date={WorkDate:yyyy-MM-dd}");

        Assert.NotNull(points);
        var max = (int)points!["maxPointsPerSession"]!;
        Assert.Equal(SeededPoints, (int)points["totalPointCount"]!);
        Assert.InRange((int)points["sampledPointCount"]!, 1, max + 1);

        var session = Assert.Single(points["sessions"]!.AsArray());
        Assert.Equal(sessionId, (int)session!["attendanceSessionId"]!);
        Assert.Equal(SeededPoints, (int)session["totalPointCount"]!);

        var kept = session["points"]!.AsArray();
        Assert.InRange(kept.Count, 1, max + 1);
        // Every stored field is on the wire, and the sample spans the whole shift.
        var first = kept[0]!;
        Assert.Equal(PunchIn, (DateTime)first["recordedAtUtc"]!);
        Assert.Equal(8, (double)first["accuracyMeters"]!, 5);
        Assert.Equal(2.4, (double)first["speedMetersPerSecond"]!, 5);
        Assert.Equal(95, (double)first["headingDegrees"]!, 5);
        Assert.Equal(100, (int)first["batteryPercent"]!);
        Assert.False((bool)first["isCharging"]!);
        Assert.False((bool)first["isBatterySaverOn"]!);
        Assert.False((bool)first["isMock"]!);
        Assert.Equal(PunchIn.AddSeconds((SeededPoints - 1) * 10), (DateTime)kept[^1]!["recordedAtUtc"]!);
    }

    [Fact]
    public async Task Points_AreRefusedToSomeoneWithoutViewFieldTracking()
    {
        await SeedShiftAsync();
        // A plain member of the organization: inside the access scope, but holding no permission.
        using var colleague = _fixture.CreateClient(_fixture.CapacityMemberUserId);

        var mine = await colleague.GetAsync(
            $"/api/attendance/organization/{_fixture.CapacityOrganizationId}/tracking/member/{_fixture.CapacityMemberUserId}/points?date={WorkDate:yyyy-MM-dd}");
        var someoneElse = await colleague.GetAsync(
            $"/api/attendance/organization/{_fixture.CapacityOrganizationId}/tracking/member/{_fixture.CapacityOwnerUserId}/points?date={WorkDate:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, someoneElse.StatusCode);
    }
}

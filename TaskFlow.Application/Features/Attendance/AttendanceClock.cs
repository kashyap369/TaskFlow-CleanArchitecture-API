namespace TaskFlow.Application.Features.Attendance;

/// <summary>Turns server time into an organization's work date.</summary>
internal static class AttendanceClock
{
    public static TimeZoneInfo Resolve(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    public static bool IsKnown(string timeZone)
    {
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    public static DateOnly WorkDate(DateTime utc, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));

    /// <summary>The UTC instant a work date begins in the zone.</summary>
    public static DateTime StartOfDayUtc(DateOnly date, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
}

namespace Rollcall.Data;

/// <summary>The school's local date. Attendance is per calendar day in Turkey, not per UTC day.</summary>
public static class SchoolTime
{
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Istanbul", out var zone)
            ? zone
            : TimeZoneInfo.CreateCustomTimeZone("Europe/Istanbul", TimeSpan.FromHours(3), "Türkiye", "Türkiye");

    public static DateTime Now(TimeProvider timeProvider) => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Zone).DateTime;

    public static DateOnly Today(TimeProvider timeProvider) => DateOnly.FromDateTime(Now(timeProvider));

    public static bool IsSchoolDay(DateOnly date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
}

using System.Globalization;
using Rollcall.Data.Entities;

namespace Rollcall.Web.Infrastructure;

/// <summary>Turkish formatting helpers shared by the views.</summary>
public static class Display
{
    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Number(int value) => value.ToString("N0", Turkish);

    public static string Score(decimal? value) => value is decimal v ? v.ToString("0.##", Turkish) : "—";

    public static string Percent(double value) => value.ToString("P0", Turkish);

    public static string Date(DateOnly value) => value.ToString("d MMMM yyyy, dddd", Turkish);

    public static string ShortDate(DateOnly value) => value.ToString("d MMM ddd", Turkish);

    public static string Initials(string firstName, string lastName) =>
        $"{firstName[..1].ToUpper(Turkish)}{lastName[..1].ToUpper(Turkish)}";

    public static string Status(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "Var",
        AttendanceStatus.Absent => "Yok",
        AttendanceStatus.Late => "Geç",
        _ => "İzinli",
    };

    public static string StatusBadge(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "badge badge-success",
        AttendanceStatus.Absent => "badge badge-danger",
        AttendanceStatus.Late => "badge badge-warning",
        _ => "badge badge-neutral",
    };

    /// <summary>Colour band for a 0–100 score: below 50 fails in Turkish high schools.</summary>
    public static string ScoreClass(decimal? value) => value switch
    {
        null => "text-muted",
        < 50 => "score-low",
        >= 85 => "score-high",
        _ => string.Empty,
    };
}

using System.Globalization;

namespace Rollcall.Application.Common;

internal static class Text
{
    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Trims and collapses inner whitespace.</summary>
    public static string Clean(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string? CleanOrNull(string? value)
    {
        var cleaned = Clean(value);
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>Capitalises each word the Turkish way: "ayşe nur" → "Ayşe Nur", "İLKER" → "İlker".</summary>
    public static string TitleCase(string? value)
    {
        var words = Clean(value).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length == 0 ? w : w[..1].ToUpper(Turkish) + w[1..].ToLower(Turkish));
        return string.Join(' ', words);
    }
}

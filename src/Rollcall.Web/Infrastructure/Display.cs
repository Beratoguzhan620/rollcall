using System.Globalization;

namespace Rollcall.Web.Infrastructure;

/// <summary>Turkish formatting helpers shared by the views.</summary>
public static class Display
{
    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Number(int value) => value.ToString("N0", Turkish);

    public static string Initials(string firstName, string lastName) =>
        $"{firstName[..1].ToUpper(Turkish)}{lastName[..1].ToUpper(Turkish)}";
}

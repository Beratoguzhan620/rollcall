using Rollcall.Application.Attendance;
using Rollcall.Application.Export;
using Rollcall.Application.Grades;

namespace Rollcall.Web.Infrastructure;

/// <summary>Column sets and file responses for the attendance and grade book exports.</summary>
public static class Exports
{
    public const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string CsvType = "text/csv; charset=utf-8";

    public static readonly IReadOnlyList<ExportColumn<AttendanceExportRow>> AttendanceColumns =
    [
        new("Tarih", r => r.Date, ExportFormat.Date),
        new("Sınıf", r => r.ClassName),
        new("Numara", r => r.StudentNumber),
        new("Ad soyad", r => r.FullName),
        new("Durum", r => Display.Status(r.Status)),
    ];

    /// <summary>One column per graded exam, then each course's average and the overall average.</summary>
    public static IReadOnlyList<ExportColumn<GradeBookRow>> GradeBookColumns(GradeBook book)
    {
        var columns = new List<ExportColumn<GradeBookRow>>
        {
            new("Numara", r => r.StudentNumber),
            new("Ad soyad", r => r.FullName),
        };

        foreach (var exam in book.Exams)
            columns.Add(new($"{exam.CourseName} · {exam.Title}", r => r.Scores.TryGetValue(exam.Id, out var score) ? score : null, ExportFormat.Score));

        var courses = book.Exams.GroupBy(e => new { e.CourseId, e.CourseName }).ToList();
        foreach (var course in courses)
        {
            var ids = course.Select(e => e.Id).ToList();
            columns.Add(new($"{course.Key.CourseName} ortalaması", r => CourseAverage(r, ids), ExportFormat.Score));
        }

        columns.Add(new("Genel ortalama", r =>
        {
            var averages = courses.Select(c => CourseAverage(r, c.Select(e => e.Id).ToList())).OfType<decimal>().ToList();
            return averages.Count == 0 ? null : Math.Round(averages.Average(), 2);
        }, ExportFormat.Score));

        return columns;
    }

    public static (byte[] Content, string ContentType, string FileName) Build<T>(
        IEnumerable<T> rows, IReadOnlyList<ExportColumn<T>> columns, string? format, string baseName, string sheetName)
    {
        return string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
            ? (TableExport.ToCsv(rows, columns), CsvType, $"{baseName}.csv")
            : (TableExport.ToXlsx(rows, columns, sheetName), XlsxType, $"{baseName}.xlsx");
    }

    private static decimal? CourseAverage(GradeBookRow row, List<int> examIds)
    {
        var scores = examIds.Where(row.Scores.ContainsKey).Select(id => row.Scores[id]).ToList();
        return scores.Count == 0 ? null : Math.Round(scores.Average(), 2);
    }
}

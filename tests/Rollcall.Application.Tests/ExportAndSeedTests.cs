using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rollcall.Application.Export;
using Rollcall.Data;
using Rollcall.Data.Entities;
using Rollcall.Data.Seeding;

namespace Rollcall.Application.Tests;

public sealed class ExportAndSeedTests : IDisposable
{
    private readonly TestDatabase db = new();

    public void Dispose() => db.Dispose();

    private sealed record Row(string Name, DateOnly Date, decimal? Score);

    private static readonly IReadOnlyList<ExportColumn<Row>> Columns =
    [
        new("Ad", r => r.Name),
        new("Tarih", r => r.Date, ExportFormat.Date),
        new("Not", r => r.Score, ExportFormat.Score),
    ];

    [Fact]
    public void Csv_is_ready_for_turkish_excel()
    {
        var bytes = TableExport.ToCsv([new Row("=HYPERLINK(\"x\")", new DateOnly(2026, 10, 14), 72.5m), new Row("Işık; Öztürk", new DateOnly(2026, 1, 2), null)], Columns);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lines = text.TrimStart('﻿').Split("\r\n");
        Assert.Equal("Ad;Tarih;Not", lines[0]);
        Assert.Equal("\"'=HYPERLINK(\"\"x\"\")\";2026-10-14;72,5", lines[1]);
        Assert.Equal("\"Işık; Öztürk\";2026-01-02;", lines[2]);
    }

    [Fact]
    public void Xlsx_is_a_workbook_with_dates_as_numbers()
    {
        var bytes = TableExport.ToXlsx([new Row("Ali", new DateOnly(2026, 10, 14), 80m)], Columns, "Notlar 9-A");

        using var zip = new ZipArchive(new MemoryStream(bytes));
        var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
        Assert.Contains("""<c r="B2" s="4"><v>46309</v></c>""", sheet);
        Assert.Contains("""<c r="C2" s="0"><v>80</v></c>""", sheet);
    }

    [Fact]
    public async Task Demo_school_is_complete_and_seeded_once()
    {
        // Start from an empty database, as on a first run.
        db.Context.Classes.RemoveRange(db.Context.Classes);
        await db.Context.SaveChangesAsync();

        await DemoDataSeeder.SeedAsync(db.Context, db.Time);
        await DemoDataSeeder.SeedAsync(db.Context, db.Time);

        var fresh = db.Fresh();
        Assert.Equal(5, await fresh.Classes.CountAsync());
        Assert.Equal(120, await fresh.Students.CountAsync());
        Assert.Equal(120, await fresh.Students.Select(s => s.StudentNumber).Distinct().CountAsync());

        var dates = await fresh.Attendance.Select(a => a.Date).Distinct().ToListAsync();
        Assert.All(dates, d => Assert.True(d <= db.Today && SchoolTime.IsSchoolDay(d)));
        Assert.Equal(DemoDataSeeder.AttendanceDays + 1, dates.Count);
        Assert.Equal(3, await fresh.Attendance.Where(a => a.Date == db.Today).Select(a => a.ClassId).Distinct().CountAsync());

        var exams = await fresh.Exams.Include(e => e.Grades).ToListAsync();
        Assert.All(exams.Where(e => e.Date < db.Today), e => Assert.Equal(DemoDataSeeder.StudentsPerClass, e.Grades.Count));
        Assert.All(exams.Where(e => e.Date > db.Today), e => Assert.Empty(e.Grades));
        Assert.All(exams.SelectMany(e => e.Grades), g => Assert.InRange(g.Score, 0, 100));
        Assert.True(await fresh.Attendance.CountAsync(a => a.Status == AttendanceStatus.Absent) > 0);
    }
}

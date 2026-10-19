using Microsoft.EntityFrameworkCore;
using Rollcall.Application.Common;
using Rollcall.Data.Entities;

namespace Rollcall.Application.Tests;

public sealed class AttendanceTests : IDisposable
{
    private readonly TestDatabase db = new();

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task Today_is_the_local_date_in_istanbul()
    {
        db.Time.SetUtcNow(new DateTimeOffset(2026, 10, 14, 22, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 10, 15), db.Attendance().Today);
    }

    [Fact]
    public async Task A_new_sheet_lists_active_students_without_a_status()
    {
        db.AddStudent("Zeynep", "Aksoy");
        db.AddStudent("Ali", "Yılmaz");
        db.AddStudent("Eski", "Öğrenci", active: false);
        db.AddStudent("Başka", "Sınıf", db.NineB);

        var sheet = await db.Attendance().GetSheetAsync(db.NineA.Id, db.Today);

        Assert.False(sheet!.IsTaken);
        Assert.Equal(new[] { "Zeynep Aksoy", "Ali Yılmaz" }, sheet.Rows.Select(r => r.FullName));
        Assert.All(sheet.Rows, r => Assert.Null(r.Status));
    }

    [Fact]
    public async Task Saving_twice_updates_instead_of_duplicating()
    {
        var ali = db.AddStudent("Ali", "Yılmaz");
        var ece = db.AddStudent("Ece", "Kaya");

        var first = await db.Attendance().SaveAsync(db.NineA.Id, db.Today, new Dictionary<int, AttendanceStatus>
        {
            [ali.Id] = AttendanceStatus.Absent,
            [ece.Id] = AttendanceStatus.Present,
        });
        await db.Attendance().SaveAsync(db.NineA.Id, db.Today, new Dictionary<int, AttendanceStatus>
        {
            [ali.Id] = AttendanceStatus.Late,
            [ece.Id] = AttendanceStatus.Present,
        });

        Assert.Contains("1 devamsız", first.Message);
        var records = await db.Fresh().Attendance.ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.Equal(AttendanceStatus.Late, records.Single(r => r.StudentId == ali.Id).Status);
        var sheet = await db.Attendance().GetSheetAsync(db.NineA.Id, db.Today);
        Assert.True(sheet!.IsTaken);
        Assert.Equal(1, sheet.Count(AttendanceStatus.Late));
    }

    [Fact]
    public async Task Future_days_and_other_classes_are_refused()
    {
        var ali = db.AddStudent("Ali", "Yılmaz");
        var other = db.AddStudent("Başka", "Sınıf", db.NineB);

        var future = await db.Attendance().SaveAsync(db.NineA.Id, db.Today.AddDays(1), new Dictionary<int, AttendanceStatus> { [ali.Id] = AttendanceStatus.Present });
        var wrongClass = await db.Attendance().SaveAsync(db.NineA.Id, db.Today, new Dictionary<int, AttendanceStatus> { [other.Id] = AttendanceStatus.Present });
        var badStatus = await db.Attendance().SaveAsync(db.NineA.Id, db.Today, new Dictionary<int, AttendanceStatus> { [ali.Id] = (AttendanceStatus)9 });
        var missing = await db.Attendance().SaveAsync(404, db.Today, new Dictionary<int, AttendanceStatus>());

        Assert.Equal(ResultStatus.Invalid, future.Status);
        Assert.Equal(ResultStatus.Invalid, wrongClass.Status);
        Assert.Equal(ResultStatus.Invalid, badStatus.Status);
        Assert.Equal(ResultStatus.NotFound, missing.Status);
        Assert.Equal(0, await db.Fresh().Attendance.CountAsync());
    }

    [Fact]
    public async Task A_student_who_left_still_appears_on_days_they_were_marked()
    {
        var ali = db.AddStudent("Ali", "Yılmaz");
        db.AddAttendance(ali, db.Today.AddDays(-1), AttendanceStatus.Present);
        ali.IsActive = false;
        db.Context.SaveChanges();

        var before = await db.Attendance().GetSheetAsync(db.NineA.Id, db.Today.AddDays(-1));
        var after = await db.Attendance().GetSheetAsync(db.NineA.Id, db.Today);

        Assert.Single(before!.Rows);
        Assert.Empty(after!.Rows);
    }

    [Fact]
    public async Task Student_summary_counts_each_status_and_lists_recent_absences()
    {
        var ali = db.AddStudent("Ali", "Yılmaz");
        db.AddAttendance(ali, new DateOnly(2026, 10, 5), AttendanceStatus.Present);
        db.AddAttendance(ali, new DateOnly(2026, 10, 6), AttendanceStatus.Absent);
        db.AddAttendance(ali, new DateOnly(2026, 10, 7), AttendanceStatus.Late);
        db.AddAttendance(ali, new DateOnly(2026, 10, 8), AttendanceStatus.Excused);

        var result = await db.Attendance().GetStudentAsync(ali.Id);

        Assert.Equal((1, 1, 1, 1), (result.Summary.Present, result.Summary.Absent, result.Summary.Late, result.Summary.Excused));
        Assert.Equal(0.5, result.Summary.PresenceRate);
        Assert.Equal(new[] { new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 6) }, result.Recent.Select(r => r.Date));
    }

    [Fact]
    public async Task Today_overview_shows_which_classes_are_done()
    {
        var ali = db.AddStudent("Ali", "Yılmaz");
        db.AddStudent("Ece", "Kaya", db.NineB);
        db.AddAttendance(ali, db.Today, AttendanceStatus.Absent);

        var today = await db.Attendance().GetTodayAsync();

        Assert.True(today.Single(c => c.ClassName == "9-A").IsTaken);
        Assert.Equal(1, today.Single(c => c.ClassName == "9-A").Absent);
        Assert.False(today.Single(c => c.ClassName == "9-B").IsTaken);
    }

    [Fact]
    public async Task Most_absent_ranks_active_students_above_the_threshold()
    {
        var many = db.AddStudent("Çok", "Gelmeyen");
        var some = db.AddStudent("Biraz", "Gelmeyen");
        var gone = db.AddStudent("Ayrılan", "Öğrenci", active: false);
        for (var day = 1; day <= 5; day++)
        {
            db.AddAttendance(many, new DateOnly(2026, 10, day), AttendanceStatus.Absent);
            db.AddAttendance(gone, new DateOnly(2026, 10, day), AttendanceStatus.Absent);
            db.AddAttendance(some, new DateOnly(2026, 10, day), day <= 3 ? AttendanceStatus.Absent : AttendanceStatus.Late);
        }

        var leaders = await db.Attendance().GetMostAbsentAsync(minimumAbsent: 3);

        Assert.Equal(new[] { "Çok Gelmeyen", "Biraz Gelmeyen" }, leaders.Select(l => l.FullName));
        Assert.Equal(2, leaders[1].Late);
    }

    [Fact]
    public async Task Export_rows_cover_the_date_range_in_order()
    {
        var ali = db.AddStudent("Ali", "Yılmaz");
        db.AddAttendance(ali, new DateOnly(2026, 10, 1), AttendanceStatus.Present);
        db.AddAttendance(ali, new DateOnly(2026, 10, 2), AttendanceStatus.Absent);
        db.AddAttendance(ali, new DateOnly(2026, 10, 9), AttendanceStatus.Late);

        var rows = await db.Attendance().GetRecordsAsync(db.NineA.Id, new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 9));

        Assert.Equal(new[] { AttendanceStatus.Absent, AttendanceStatus.Late }, rows.Select(r => r.Status));
        Assert.All(rows, r => Assert.Equal("9-A", r.ClassName));
    }
}

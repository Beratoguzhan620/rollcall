using Microsoft.EntityFrameworkCore;
using Rollcall.Application.Common;
using Rollcall.Data;
using Rollcall.Data.Entities;

namespace Rollcall.Application.Attendance;

public sealed record SheetRow(int StudentId, string StudentNumber, string FullName, AttendanceStatus? Status);

public sealed record AttendanceSheet(int ClassId, string ClassName, DateOnly Date, bool IsTaken, List<SheetRow> Rows)
{
    public int Count(AttendanceStatus status) => Rows.Count(r => r.Status == status);
}

public sealed record AttendanceSummary(int Present, int Absent, int Late, int Excused)
{
    public int Total => Present + Absent + Late + Excused;

    /// <summary>Share of recorded days the student was in class (late counts as present).</summary>
    public double PresenceRate => Total == 0 ? 1 : (Present + Late) / (double)Total;
}

public sealed record AbsenceDay(DateOnly Date, AttendanceStatus Status);

public sealed record StudentAttendance(AttendanceSummary Summary, List<AbsenceDay> Recent);

public sealed record ClassToday(int ClassId, string ClassName, int StudentCount, bool IsTaken, int Absent, int Late);

public sealed record AbsenceLeader(int StudentId, string FullName, string ClassName, int Absent, int Late, int Days);

public sealed class AttendanceService(AppDbContext context, TimeProvider timeProvider)
{
    public DateOnly Today => SchoolTime.Today(timeProvider);

    /// <summary>The class list for a day, with the saved status of each student or null when not taken yet.</summary>
    public async Task<AttendanceSheet?> GetSheetAsync(int classId, DateOnly date)
    {
        var schoolClass = await context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId);
        if (schoolClass is null)
            return null;

        var saved = await context.Attendance
            .AsNoTracking()
            .Where(a => a.ClassId == classId && a.Date == date)
            .ToDictionaryAsync(a => a.StudentId, a => a.Status);

        // Active students, plus anyone who has a record that day (e.g. left the school afterwards).
        var savedIds = saved.Keys.ToList();
        var students = await context.Students
            .AsNoTracking()
            .Where(s => (s.ClassId == classId && s.IsActive) || savedIds.Contains(s.Id))
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, s.StudentNumber, s.FirstName, s.LastName })
            .ToListAsync();

        var rows = students
            .Select(s => new SheetRow(s.Id, s.StudentNumber, $"{s.FirstName} {s.LastName}", saved.TryGetValue(s.Id, out var status) ? status : null))
            .ToList();

        return new AttendanceSheet(classId, schoolClass.Name, date, saved.Count > 0, rows);
    }

    /// <summary>Saves the whole sheet at once; students missing from <paramref name="statuses"/> keep their record.</summary>
    public async Task<Result<int>> SaveAsync(int classId, DateOnly date, IReadOnlyDictionary<int, AttendanceStatus> statuses)
    {
        if (!await context.Classes.AnyAsync(c => c.Id == classId))
            return Result<int>.NotFound("Sınıf bulunamadı.");

        if (date > Today)
            return Result<int>.Invalid("İleri bir tarih için yoklama alınamaz.");

        if (statuses.Values.Any(s => !Enum.IsDefined(s)))
            return Result<int>.Invalid("Geçersiz yoklama durumu.");

        var ids = statuses.Keys.ToList();
        var allowed = await context.Students
            .Where(s => ids.Contains(s.Id) && (s.ClassId == classId || context.Attendance.Any(a => a.StudentId == s.Id && a.Date == date && a.ClassId == classId)))
            .Select(s => s.Id)
            .ToListAsync();
        if (allowed.Count != ids.Count)
            return Result<int>.Invalid("Listede bu sınıfa ait olmayan öğrenciler var.");

        var existing = await context.Attendance
            .Where(a => a.Date == date && ids.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var (studentId, status) in statuses)
        {
            if (existing.TryGetValue(studentId, out var record))
            {
                if (record.Status != status)
                {
                    record.Status = status;
                    record.RecordedAt = now;
                }
                record.ClassId = classId;
            }
            else
            {
                context.Attendance.Add(new AttendanceRecord { StudentId = studentId, ClassId = classId, Date = date, Status = status, RecordedAt = now });
            }
        }

        await context.SaveChangesAsync();
        var absent = statuses.Values.Count(s => s == AttendanceStatus.Absent);
        return Result<int>.Ok(statuses.Count, $"Yoklama kaydedildi: {statuses.Count} öğrenci, {absent} devamsız.");
    }

    public async Task<StudentAttendance> GetStudentAsync(int studentId, int recentCount = 10)
    {
        var counts = await context.Attendance
            .AsNoTracking()
            .Where(a => a.StudentId == studentId)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int Of(AttendanceStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        var recent = await context.Attendance
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && a.Status != AttendanceStatus.Present)
            .OrderByDescending(a => a.Date)
            .Take(recentCount)
            .Select(a => new AbsenceDay(a.Date, a.Status))
            .ToListAsync();

        return new StudentAttendance(
            new AttendanceSummary(Of(AttendanceStatus.Present), Of(AttendanceStatus.Absent), Of(AttendanceStatus.Late), Of(AttendanceStatus.Excused)),
            recent);
    }

    /// <summary>For each class: whether today's attendance is in and how many are away.</summary>
    public async Task<List<ClassToday>> GetTodayAsync()
    {
        var today = Today;
        var records = await context.Attendance
            .AsNoTracking()
            .Where(a => a.Date == today)
            .GroupBy(a => a.ClassId)
            .Select(g => new
            {
                ClassId = g.Key,
                Absent = g.Count(a => a.Status == AttendanceStatus.Absent),
                Late = g.Count(a => a.Status == AttendanceStatus.Late),
            })
            .ToListAsync();

        var classes = await context.Classes
            .AsNoTracking()
            .OrderBy(c => c.GradeLevel).ThenBy(c => c.Section)
            .Select(c => new { c.Id, c.GradeLevel, c.Section, Count = c.Students.Count(s => s.IsActive) })
            .ToListAsync();

        return classes.Select(c =>
        {
            var record = records.FirstOrDefault(r => r.ClassId == c.Id);
            return new ClassToday(c.Id, $"{c.GradeLevel}-{c.Section}", c.Count, record is not null, record?.Absent ?? 0, record?.Late ?? 0);
        }).ToList();
    }

    /// <summary>Active students with the most absent days, at or above <paramref name="minimumAbsent"/>.</summary>
    public async Task<List<AbsenceLeader>> GetMostAbsentAsync(int take = 8, int minimumAbsent = 3)
    {
        var rows = await context.Attendance
            .AsNoTracking()
            .Where(a => a.Student!.IsActive)
            .GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Absent = g.Count(a => a.Status == AttendanceStatus.Absent),
                Late = g.Count(a => a.Status == AttendanceStatus.Late),
                Days = g.Count(),
            })
            .Where(r => r.Absent >= minimumAbsent)
            .OrderByDescending(r => r.Absent).ThenByDescending(r => r.Late).ThenBy(r => r.StudentId)
            .Take(take)
            .ToListAsync();

        var ids = rows.Select(r => r.StudentId).ToList();
        var students = await context.Students
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.FirstName, s.LastName, s.Class!.GradeLevel, s.Class.Section })
            .ToDictionaryAsync(s => s.Id);

        return rows.Select(r =>
        {
            var s = students[r.StudentId];
            return new AbsenceLeader(r.StudentId, $"{s.FirstName} {s.LastName}", $"{s.GradeLevel}-{s.Section}", r.Absent, r.Late, r.Days);
        }).ToList();
    }
}

using Microsoft.EntityFrameworkCore;
using Rollcall.Application.Common;
using Rollcall.Data;

namespace Rollcall.Application.Grades;

public sealed record CourseItem(int Id, string Name);

public sealed record ExamRow(int Id, int ClassId, string ClassName, int CourseId, string CourseName, string Title, DateOnly Date, int GradedCount, int StudentCount, decimal? Average);

public sealed record ExamInput(int ClassId, int CourseId, string Title, DateOnly Date);

public sealed record ScoreRow(int StudentId, string StudentNumber, string FullName, decimal? Score);

public sealed record GradeSheet(ExamRow Exam, List<ScoreRow> Rows);

public sealed record CourseResult(int CourseId, string CourseName, List<(string Title, DateOnly Date, decimal Score)> Scores)
{
    public decimal? Average => Scores.Count == 0 ? null : Math.Round(Scores.Average(s => s.Score), 2);
}

public sealed record StudentGrades(List<CourseResult> Courses)
{
    /// <summary>Mean of the course averages, so a course with many exams does not outweigh the others.</summary>
    public decimal? Overall => Courses.Count(c => c.Average is not null) == 0
        ? null
        : Math.Round(Courses.Where(c => c.Average is not null).Average(c => c.Average!.Value), 2);
}

/// <summary>One row per student with a score per exam (by exam id), for the class grade book export.</summary>
public sealed record GradeBook(List<ExamRow> Exams, List<GradeBookRow> Rows);

public sealed record GradeBookRow(string StudentNumber, string FullName, Dictionary<int, decimal> Scores);

public sealed class GradeService(AppDbContext context, TimeProvider timeProvider)
{
    public Task<List<CourseItem>> GetCoursesAsync() =>
        context.Courses.AsNoTracking().OrderBy(c => c.Id).Select(c => new CourseItem(c.Id, c.Name)).ToListAsync();

    public async Task<List<ExamRow>> GetExamsAsync(int? classId = null, int? courseId = null)
    {
        var exams = context.Exams.AsNoTracking();
        if (classId is int c)
            exams = exams.Where(e => e.ClassId == c);
        if (courseId is int course)
            exams = exams.Where(e => e.CourseId == course);

        var rows = await exams
            .OrderByDescending(e => e.Date).ThenBy(e => e.Class!.GradeLevel).ThenBy(e => e.Class!.Section).ThenBy(e => e.Course!.Name)
            .Select(e => new
            {
                e.Id, e.ClassId, e.Class!.GradeLevel, e.Class.Section, e.CourseId, CourseName = e.Course!.Name, e.Title, e.Date,
                Scores = e.Grades.Select(g => g.Score).ToList(),
                StudentCount = e.Class.Students.Count(s => s.IsActive),
            })
            .ToListAsync();

        // Averages are worked out here: SQLite (used by the tests) cannot aggregate decimals.
        return rows.Select(r => new ExamRow(
            r.Id, r.ClassId, $"{r.GradeLevel}-{r.Section}", r.CourseId, r.CourseName, r.Title, r.Date,
            r.Scores.Count, r.StudentCount, r.Scores.Count == 0 ? null : Math.Round(r.Scores.Average(), 2))).ToList();
    }

    public async Task<ExamRow?> GetExamAsync(int id) => (await GetExamsWhereAsync(id)).FirstOrDefault();

    public async Task<Result<int>> CreateExamAsync(ExamInput input)
    {
        var title = Text.Clean(input.Title);
        if (title.Length < 2 || title.Length > Limits.ExamTitle)
            return Result<int>.Invalid($"Sınav adı 2 ile {Limits.ExamTitle} karakter arasında olmalı.", nameof(ExamInput.Title));
        if (!await context.Classes.AnyAsync(c => c.Id == input.ClassId))
            return Result<int>.Invalid("Bir sınıf seçin.", nameof(ExamInput.ClassId));
        if (!await context.Courses.AnyAsync(c => c.Id == input.CourseId))
            return Result<int>.Invalid("Bir ders seçin.", nameof(ExamInput.CourseId));

        var duplicate = await context.Exams.AnyAsync(e =>
            e.ClassId == input.ClassId && e.CourseId == input.CourseId && e.Title == title);
        if (duplicate)
            return Result<int>.Invalid("Bu sınıf ve derste aynı adla bir sınav var.", nameof(ExamInput.Title));

        var exam = new Data.Entities.Exam
        {
            ClassId = input.ClassId,
            CourseId = input.CourseId,
            Title = title,
            Date = input.Date,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        context.Exams.Add(exam);
        await context.SaveChangesAsync();
        return Result<int>.Ok(exam.Id, "Sınav eklendi. Şimdi notları girebilirsiniz.");
    }

    public async Task<Result> DeleteExamAsync(int id)
    {
        var exam = await context.Exams.FirstOrDefaultAsync(e => e.Id == id);
        if (exam is null)
            return Result.NotFound("Sınav bulunamadı.");

        context.Exams.Remove(exam);
        await context.SaveChangesAsync();
        return Result.Ok($"\"{exam.Title}\" ve notları silindi.");
    }

    public async Task<GradeSheet?> GetSheetAsync(int examId)
    {
        var exam = await GetExamAsync(examId);
        if (exam is null)
            return null;

        var scores = await context.Grades.AsNoTracking()
            .Where(g => g.ExamId == examId)
            .ToDictionaryAsync(g => g.StudentId, g => g.Score);
        var scoredIds = scores.Keys.ToList();

        var students = await context.Students
            .AsNoTracking()
            .Where(s => (s.ClassId == exam.ClassId && s.IsActive) || scoredIds.Contains(s.Id))
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, s.StudentNumber, s.FirstName, s.LastName })
            .ToListAsync();

        return new GradeSheet(exam, students
            .Select(s => new ScoreRow(s.Id, s.StudentNumber, $"{s.FirstName} {s.LastName}", scores.TryGetValue(s.Id, out var score) ? score : null))
            .ToList());
    }

    /// <summary>Saves every score on the sheet; an empty score removes the student's grade.</summary>
    public async Task<Result> SaveScoresAsync(int examId, IReadOnlyDictionary<int, decimal?> scores)
    {
        var exam = await context.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.Id == examId);
        if (exam is null)
            return Result.NotFound("Sınav bulunamadı.");

        if (scores.Values.Any(v => v is < 0 or > 100 || (v is decimal d && decimal.Round(d, 2) != d)))
            return Result.Invalid("Notlar 0 ile 100 arasında, en fazla iki ondalık olmalı.");

        var ids = scores.Keys.ToList();
        var classStudents = await context.Students
            .Where(s => ids.Contains(s.Id) && (s.ClassId == exam.ClassId || context.Grades.Any(g => g.ExamId == examId && g.StudentId == s.Id)))
            .CountAsync();
        if (classStudents != ids.Count)
            return Result.Invalid("Listede bu sınıfa ait olmayan öğrenciler var.");

        var existing = await context.Grades.Where(g => g.ExamId == examId && ids.Contains(g.StudentId)).ToDictionaryAsync(g => g.StudentId);

        foreach (var (studentId, score) in scores)
        {
            existing.TryGetValue(studentId, out var grade);
            if (score is null)
            {
                if (grade is not null)
                    context.Grades.Remove(grade);
            }
            else if (grade is null)
            {
                context.Grades.Add(new Data.Entities.Grade { ExamId = examId, StudentId = studentId, Score = score.Value });
            }
            else
            {
                grade.Score = score.Value;
            }
        }

        await context.SaveChangesAsync();
        return Result.Ok($"Notlar kaydedildi: {scores.Count(s => s.Value is not null)} öğrenci.");
    }

    public async Task<StudentGrades> GetStudentAsync(int studentId)
    {
        var rows = await context.Grades
            .AsNoTracking()
            .Where(g => g.StudentId == studentId)
            .Select(g => new { g.Exam!.CourseId, CourseName = g.Exam.Course!.Name, g.Exam.Title, g.Exam.Date, g.Score })
            .ToListAsync();

        return new StudentGrades(rows
            .GroupBy(r => new { r.CourseId, r.CourseName })
            .OrderBy(g => g.Key.CourseId)
            .Select(g => new CourseResult(g.Key.CourseId, g.Key.CourseName, g.OrderBy(r => r.Date).Select(r => (r.Title, r.Date, r.Score)).ToList()))
            .ToList());
    }

    /// <summary>The whole grade book of a class: graded exams oldest first and every active student's scores.</summary>
    public async Task<GradeBook> GetGradeBookAsync(int classId)
    {
        var exams = (await GetExamsAsync(classId)).Where(e => e.GradedCount > 0).OrderBy(e => e.CourseId).ThenBy(e => e.Date).ToList();
        var examIds = exams.Select(e => e.Id).ToList();

        var grades = await context.Grades
            .AsNoTracking()
            .Where(g => examIds.Contains(g.ExamId))
            .Select(g => new { g.StudentId, g.ExamId, g.Score })
            .ToListAsync();

        var students = await context.Students
            .AsNoTracking()
            .Where(s => s.ClassId == classId && s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, s.StudentNumber, s.FirstName, s.LastName })
            .ToListAsync();

        return new GradeBook(exams, students
            .Select(s => new GradeBookRow(
                s.StudentNumber,
                $"{s.FirstName} {s.LastName}",
                grades.Where(g => g.StudentId == s.Id).ToDictionary(g => g.ExamId, g => g.Score)))
            .ToList());
    }

    private async Task<List<ExamRow>> GetExamsWhereAsync(int id)
    {
        var exam = await context.Exams.AsNoTracking().Where(e => e.Id == id).Select(e => new { e.ClassId, e.CourseId }).FirstOrDefaultAsync();
        if (exam is null)
            return [];
        return (await GetExamsAsync(exam.ClassId, exam.CourseId)).Where(e => e.Id == id).ToList();
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Rollcall.Application.Attendance;
using Rollcall.Application.Classes;
using Rollcall.Application.Grades;
using Rollcall.Application.Students;
using Rollcall.Data;
using Rollcall.Data.Entities;

namespace Rollcall.Application.Tests;

/// <summary>
/// A fresh in-memory SQLite database per test with two classes. SQLite runs real SQL, so unique indexes,
/// check constraints and foreign keys behave as they do on SQL Server.
/// The clock is Wednesday 14 October 2026, 10:00 in Istanbul.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    public TestDatabase()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        Context = CreateContext();
        Context.Database.EnsureCreated();

        var now = Time.GetUtcNow().UtcDateTime;
        NineA = new SchoolClass { GradeLevel = 9, Section = "A", AdvisorName = "Selin Aydın", CreatedAt = now };
        NineB = new SchoolClass { GradeLevel = 9, Section = "B", CreatedAt = now };
        Context.AddRange(NineA, NineB);
        Context.SaveChanges();
    }

    public AppDbContext Context { get; }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 14, 7, 0, 0, TimeSpan.Zero));

    public DateOnly Today => new(2026, 10, 14);

    public SchoolClass NineA { get; }

    public SchoolClass NineB { get; }

    public ClassService Classes() => new(Context, Time);

    public StudentService Students() => new(Context, Time);

    public AttendanceService Attendance() => new(Context, Time);

    public GradeService Grades() => new(Context, Time);

    /// <summary>A separate context on the same database, to read what was really saved.</summary>
    public AppDbContext Fresh() => CreateContext();

    private int nextNumber = 100;

    public Student AddStudent(string firstName, string lastName, SchoolClass? schoolClass = null, bool active = true, string? email = null)
    {
        var student = new Student
        {
            StudentNumber = (nextNumber++).ToString(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            ClassId = (schoolClass ?? NineA).Id,
            IsActive = active,
            CreatedAt = Time.GetUtcNow().UtcDateTime,
        };
        Context.Students.Add(student);
        Context.SaveChanges();
        return student;
    }

    public void AddAttendance(Student student, DateOnly date, AttendanceStatus status)
    {
        Context.Attendance.Add(new AttendanceRecord { StudentId = student.Id, ClassId = student.ClassId, Date = date, Status = status, RecordedAt = Time.GetUtcNow().UtcDateTime });
        Context.SaveChanges();
    }

    public Exam AddExam(int courseId = 2, string title = "1. yazılı", SchoolClass? schoolClass = null, DateOnly? date = null)
    {
        var exam = new Exam { ClassId = (schoolClass ?? NineA).Id, CourseId = courseId, Title = title, Date = date ?? Today.AddDays(-3), CreatedAt = Time.GetUtcNow().UtcDateTime };
        Context.Exams.Add(exam);
        Context.SaveChanges();
        return exam;
    }

    public void AddGrade(Exam exam, Student student, decimal score)
    {
        Context.Grades.Add(new Grade { ExamId = exam.Id, StudentId = student.Id, Score = score });
        Context.SaveChanges();
    }

    public void Dispose()
    {
        Context.Dispose();
        connection.Dispose();
    }

    private AppDbContext CreateContext() =>
        new SqliteAppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    /// <summary>
    /// SQLite keeps decimals as text, which would make the score check constraint compare strings.
    /// Storing scores as REAL here keeps the constraint numeric, as it is on SQL Server.
    /// </summary>
    private sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Grade>().Property(g => g.Score).HasConversion<double>();
        }
    }
}

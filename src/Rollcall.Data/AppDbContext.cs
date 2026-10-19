using Microsoft.EntityFrameworkCore;
using Rollcall.Data.Entities;

namespace Rollcall.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<Exam> Exams => Set<Exam>();

    public DbSet<Grade> Grades => Set<Grade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolClass>(entity =>
        {
            entity.ToTable("Classes", table =>
            {
                table.HasCheckConstraint("CK_Classes_GradeLevel", "[GradeLevel] BETWEEN 1 AND 12");
            });
            entity.Property(c => c.Section).HasMaxLength(Limits.Section).IsRequired();
            entity.Property(c => c.AdvisorName).HasMaxLength(Limits.PersonName);
            entity.Property(c => c.Room).HasMaxLength(Limits.Room);
            entity.HasIndex(c => new { c.GradeLevel, c.Section }).IsUnique();
            entity.Ignore(c => c.Name);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.Property(s => s.StudentNumber).HasMaxLength(Limits.StudentNumber).IsRequired();
            entity.Property(s => s.FirstName).HasMaxLength(Limits.NamePart).IsRequired();
            entity.Property(s => s.LastName).HasMaxLength(Limits.NamePart).IsRequired();
            entity.Property(s => s.Email).HasMaxLength(Limits.Email);
            entity.HasIndex(s => s.StudentNumber).IsUnique();
            entity.HasIndex(s => new { s.ClassId, s.IsActive });
            entity.Ignore(s => s.FullName);

            // A class with students cannot disappear from under them.
            entity.HasOne(s => s.Class)
                .WithMany(c => c.Students)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("AttendanceRecords");
            entity.HasIndex(a => new { a.StudentId, a.Date }).IsUnique();
            entity.HasIndex(a => new { a.ClassId, a.Date });
            entity.HasIndex(a => new { a.Date, a.Status });
            entity.HasOne(a => a.Student).WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.Class).WithMany().HasForeignKey(a => a.ClassId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(Limits.CourseName).IsRequired();
            entity.HasIndex(c => c.Name).IsUnique();
            entity.HasData(CourseSeed.All);
        });

        modelBuilder.Entity<Exam>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(Limits.ExamTitle).IsRequired();
            entity.HasIndex(e => new { e.ClassId, e.CourseId, e.Date });
            entity.HasOne(e => e.Class).WithMany().HasForeignKey(e => e.ClassId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Course).WithMany(c => c.Exams).HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Grade>(entity =>
        {
            entity.ToTable("Grades", table =>
            {
                table.HasCheckConstraint("CK_Grades_Score", "[Score] BETWEEN 0 AND 100");
            });
            entity.Property(g => g.Score).HasPrecision(5, 2);
            entity.HasIndex(g => new { g.ExamId, g.StudentId }).IsUnique();
            entity.HasOne(g => g.Exam).WithMany(e => e.Grades).HasForeignKey(g => g.ExamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(g => g.Student).WithMany().HasForeignKey(g => g.StudentId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}

/// <summary>Column sizes shared by the model, the services and the forms.</summary>
public static class Limits
{
    public const int Section = 2;
    public const int PersonName = 100;
    public const int Room = 30;
    public const int StudentNumber = 10;
    public const int NamePart = 50;
    public const int Email = 100;
    public const int CourseName = 60;
    public const int ExamTitle = 60;
}

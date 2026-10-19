using Microsoft.EntityFrameworkCore;
using Rollcall.Data.Entities;

namespace Rollcall.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();

    public DbSet<Student> Students => Set<Student>();

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
}

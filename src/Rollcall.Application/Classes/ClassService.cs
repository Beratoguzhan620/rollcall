using Microsoft.EntityFrameworkCore;
using Rollcall.Application.Common;
using Rollcall.Data;
using Rollcall.Data.Entities;

namespace Rollcall.Application.Classes;

public sealed record ClassRow(int Id, int GradeLevel, string Section, string? AdvisorName, string? Room, int StudentCount)
{
    public string Name => $"{GradeLevel}-{Section}";
}

public sealed record ClassInput(int GradeLevel, string Section, string? AdvisorName, string? Room);

public sealed class ClassService(AppDbContext context, TimeProvider timeProvider)
{
    public Task<List<ClassRow>> GetAllAsync() =>
        context.Classes
            .AsNoTracking()
            .OrderBy(c => c.GradeLevel).ThenBy(c => c.Section)
            .Select(c => new ClassRow(c.Id, c.GradeLevel, c.Section, c.AdvisorName, c.Room, c.Students.Count(s => s.IsActive)))
            .ToListAsync();

    public Task<ClassRow?> GetAsync(int id) =>
        context.Classes
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ClassRow(c.Id, c.GradeLevel, c.Section, c.AdvisorName, c.Room, c.Students.Count(s => s.IsActive)))
            .FirstOrDefaultAsync();

    public async Task<Result<int>> CreateAsync(ClassInput input)
    {
        var check = await ValidateAsync(input, exceptId: null);
        if (!check.Success)
            return Result<int>.Invalid(check.Message, check.Field);

        var entity = new SchoolClass { CreatedAt = timeProvider.GetUtcNow().UtcDateTime };
        Apply(entity, input);
        context.Classes.Add(entity);
        await context.SaveChangesAsync();
        return Result<int>.Ok(entity.Id, $"{entity.Name} sınıfı eklendi.");
    }

    public async Task<Result> UpdateAsync(int id, ClassInput input)
    {
        var entity = await context.Classes.FirstOrDefaultAsync(c => c.Id == id);
        if (entity is null)
            return Result.NotFound("Sınıf bulunamadı.");

        var check = await ValidateAsync(input, exceptId: id);
        if (!check.Success)
            return check;

        Apply(entity, input);
        await context.SaveChangesAsync();
        return Result.Ok($"{entity.Name} sınıfı güncellendi.");
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var entity = await context.Classes.FirstOrDefaultAsync(c => c.Id == id);
        if (entity is null)
            return Result.NotFound("Sınıf bulunamadı.");

        var students = await context.Students.CountAsync(s => s.ClassId == id);
        if (students > 0)
            return Result.Invalid($"{entity.Name} sınıfında {students} öğrenci kayıtlı. Önce öğrencileri başka bir sınıfa taşıyın.");

        if (await context.Exams.AnyAsync(e => e.ClassId == id) || await context.Attendance.AnyAsync(a => a.ClassId == id))
            return Result.Invalid($"{entity.Name} sınıfının yoklama ya da sınav kayıtları var; silinemez.");

        context.Classes.Remove(entity);
        await context.SaveChangesAsync();
        return Result.Ok($"{entity.Name} sınıfı silindi.");
    }

    private async Task<Result> ValidateAsync(ClassInput input, int? exceptId)
    {
        if (input.GradeLevel is < 1 or > 12)
            return Result.Invalid("Sınıf seviyesi 1 ile 12 arasında olmalı.", nameof(ClassInput.GradeLevel));

        var section = NormalizeSection(input.Section);
        if (section is null)
            return Result.Invalid("Şube bir ya da iki harf olmalı (ör. A).", nameof(ClassInput.Section));

        if (await context.Classes.AnyAsync(c => c.Id != exceptId && c.GradeLevel == input.GradeLevel && c.Section == section))
            return Result.Invalid($"{input.GradeLevel}-{section} sınıfı zaten var.", nameof(ClassInput.Section));

        return Result.Ok();
    }

    private static void Apply(SchoolClass entity, ClassInput input)
    {
        entity.GradeLevel = input.GradeLevel;
        entity.Section = NormalizeSection(input.Section)!;
        entity.AdvisorName = Text.CleanOrNull(input.AdvisorName);
        entity.Room = Text.CleanOrNull(input.Room)?.ToUpper(Text.Turkish);
    }

    /// <summary>"a", " A " and "a" all become "A"; Turkish letters are upper-cased the Turkish way (i → İ).</summary>
    internal static string? NormalizeSection(string? section)
    {
        var value = (section ?? string.Empty).Trim().ToUpper(Text.Turkish);
        return value.Length is >= 1 and <= Limits.Section && value.All(char.IsLetter) ? value : null;
    }
}

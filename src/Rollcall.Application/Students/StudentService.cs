using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Rollcall.Application.Common;
using Rollcall.Data;
using Rollcall.Data.Entities;

namespace Rollcall.Application.Students;

public sealed record StudentQuery
{
    public string? Search { get; init; }
    public int? ClassId { get; init; }
    public bool IncludeInactive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record StudentRow(int Id, string StudentNumber, string FirstName, string LastName, string? Email, int ClassId, string ClassName, bool IsActive)
{
    public string FullName => $"{FirstName} {LastName}";
}

public sealed record StudentInput(string StudentNumber, string FirstName, string LastName, string? Email, int ClassId, bool IsActive = true);

public sealed class StudentService(AppDbContext context, TimeProvider timeProvider)
{
    public async Task<PagedList<StudentRow>> ListAsync(StudentQuery query)
    {
        var students = context.Students.AsNoTracking();

        if (!query.IncludeInactive)
            students = students.Where(s => s.IsActive);

        if (query.ClassId is int classId)
            students = students.Where(s => s.ClassId == classId);

        var search = Text.Clean(query.Search);
        if (search.Length > 0)
        {
            foreach (var word in search.Split(' '))
            {
                var pattern = $"%{word}%";
                students = students.Where(s =>
                    EF.Functions.Like(s.FirstName, pattern) ||
                    EF.Functions.Like(s.LastName, pattern) ||
                    EF.Functions.Like(s.StudentNumber, pattern));
            }
        }

        return await students
            .OrderBy(s => s.Class!.GradeLevel).ThenBy(s => s.Class!.Section)
            .ThenBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new StudentRow(
                s.Id, s.StudentNumber, s.FirstName, s.LastName, s.Email, s.ClassId,
                s.Class!.GradeLevel + "-" + s.Class.Section, s.IsActive))
            .ToPagedListAsync(query.Page, query.PageSize);
    }

    public Task<StudentRow?> GetAsync(int id) =>
        context.Students
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StudentRow(
                s.Id, s.StudentNumber, s.FirstName, s.LastName, s.Email, s.ClassId,
                s.Class!.GradeLevel + "-" + s.Class.Section, s.IsActive))
            .FirstOrDefaultAsync();

    /// <summary>Suggests the next free school number, one above the highest numeric one.</summary>
    public async Task<string> NextNumberAsync()
    {
        var numbers = await context.Students.Select(s => s.StudentNumber).ToListAsync();
        var highest = numbers.Select(n => int.TryParse(n, out var value) ? value : 0).DefaultIfEmpty(1000).Max();
        return (highest + 1).ToString();
    }

    public async Task<Result<int>> CreateAsync(StudentInput input)
    {
        var check = await ValidateAsync(input, exceptId: null);
        if (!check.Success)
            return Result<int>.Invalid(check.Message, check.Field);

        var entity = new Student { CreatedAt = timeProvider.GetUtcNow().UtcDateTime };
        Apply(entity, input);
        context.Students.Add(entity);
        await context.SaveChangesAsync();
        return Result<int>.Ok(entity.Id, $"{entity.FullName} eklendi.");
    }

    public async Task<Result> UpdateAsync(int id, StudentInput input)
    {
        var entity = await context.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (entity is null)
            return Result.NotFound("Öğrenci bulunamadı.");

        var check = await ValidateAsync(input, exceptId: id);
        if (!check.Success)
            return check;

        Apply(entity, input);
        await context.SaveChangesAsync();
        return Result.Ok($"{entity.FullName} güncellendi.");
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var entity = await context.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (entity is null)
            return Result.NotFound("Öğrenci bulunamadı.");

        var hasRecords = await context.Attendance.AnyAsync(a => a.StudentId == id) || await context.Grades.AnyAsync(g => g.StudentId == id);
        if (hasRecords)
            return Result.Invalid($"{entity.FullName} için yoklama ya da not kaydı var. Silmek yerine öğrenciyi \"okuldan ayrıldı\" olarak işaretleyin.");

        context.Students.Remove(entity);
        await context.SaveChangesAsync();
        return Result.Ok($"{entity.FullName} silindi.");
    }

    private async Task<Result> ValidateAsync(StudentInput input, int? exceptId)
    {
        var number = Text.Clean(input.StudentNumber);
        if (number.Length == 0 || number.Length > Limits.StudentNumber || !number.All(char.IsAsciiDigit))
            return Result.Invalid($"Okul numarası en fazla {Limits.StudentNumber} haneli bir sayı olmalı.", nameof(StudentInput.StudentNumber));

        if (Text.Clean(input.FirstName).Length < 2)
            return Result.Invalid("Ad en az 2 karakter olmalı.", nameof(StudentInput.FirstName));

        if (Text.Clean(input.LastName).Length < 2)
            return Result.Invalid("Soyad en az 2 karakter olmalı.", nameof(StudentInput.LastName));

        var email = NormalizeEmail(input.Email, out var emailValid);
        if (!emailValid)
            return Result.Invalid("Geçerli bir e-posta adresi yazın ya da boş bırakın.", nameof(StudentInput.Email));

        if (!await context.Classes.AnyAsync(c => c.Id == input.ClassId))
            return Result.Invalid("Bir sınıf seçin.", nameof(StudentInput.ClassId));

        if (await context.Students.AnyAsync(s => s.Id != exceptId && s.StudentNumber == number))
            return Result.Invalid($"{number} numarası başka bir öğrenciye ait.", nameof(StudentInput.StudentNumber));

        if (email is not null && await context.Students.AnyAsync(s => s.Id != exceptId && s.Email == email))
            return Result.Invalid("Bu e-posta adresi başka bir öğrenciye ait.", nameof(StudentInput.Email));

        return Result.Ok();
    }

    private static void Apply(Student entity, StudentInput input)
    {
        entity.StudentNumber = Text.Clean(input.StudentNumber);
        entity.FirstName = Text.TitleCase(input.FirstName);
        entity.LastName = Text.TitleCase(input.LastName);
        entity.Email = NormalizeEmail(input.Email, out _);
        entity.ClassId = input.ClassId;
        entity.IsActive = input.IsActive;
    }

    internal static string? NormalizeEmail(string? email, out bool valid)
    {
        var trimmed = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(trimmed))
        {
            valid = true;
            return null;
        }

        valid = trimmed.Length <= Limits.Email
            && MailAddress.TryCreate(trimmed, out var address)
            && address.Address == trimmed
            && address.Host.Contains('.', StringComparison.Ordinal);
        return valid ? trimmed : null;
    }
}

using Rollcall.Data.Entities;

namespace Rollcall.Data;

/// <summary>Courses created by the migration; exams are planned per class against these.</summary>
public static class CourseSeed
{
    public static readonly Course[] All =
    [
        new() { Id = 1, Name = "Türk Dili ve Edebiyatı" },
        new() { Id = 2, Name = "Matematik" },
        new() { Id = 3, Name = "Fizik" },
        new() { Id = 4, Name = "Kimya" },
        new() { Id = 5, Name = "Biyoloji" },
        new() { Id = 6, Name = "Tarih" },
        new() { Id = 7, Name = "Coğrafya" },
        new() { Id = 8, Name = "İngilizce" },
    ];
}

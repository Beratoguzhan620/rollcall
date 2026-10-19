using Microsoft.EntityFrameworkCore;
using Rollcall.Data.Entities;

namespace Rollcall.Data.Seeding;

/// <summary>A small high school: five classes with 24 students each.</summary>
public static class DemoDataSeeder
{
    private static readonly (int Grade, string Section, string Advisor, string Room)[] ClassPlans =
    [
        (9, "A", "Selin Aydın", "B-104"),
        (9, "B", "Murat Kaya", "B-105"),
        (10, "A", "Elif Şahin", "B-201"),
        (10, "B", "Hakan Demir", "B-202"),
        (11, "A", "Zeynep Arslan", "C-301"),
    ];

    private static readonly string[] FirstNames =
    [
        "Ada", "Alp", "Arda", "Aslı", "Ayşe", "Baran", "Berk", "Beren", "Can", "Cansu", "Defne", "Deniz",
        "Doruk", "Ece", "Efe", "Ege", "Elif", "Emir", "Eylül", "Hira", "İpek", "Kaan", "Kerem", "Lara",
        "Mert", "Mina", "Nehir", "Ozan", "Öykü", "Poyraz", "Selin", "Sena", "Tuna", "Umut", "Yağmur", "Zeynep",
    ];

    private static readonly string[] LastNames =
    [
        "Acar", "Aksoy", "Altun", "Aydın", "Bulut", "Çelik", "Çetin", "Demir", "Doğan", "Erdem", "Eren", "Güler",
        "Güneş", "Işık", "Kara", "Kaya", "Keskin", "Koç", "Kurt", "Önal", "Öztürk", "Polat", "Şahin", "Şimşek",
        "Tekin", "Turan", "Uçar", "Ünal", "Yalçın", "Yıldız",
    ];

    public const int StudentsPerClass = 24;

    public static async Task SeedAsync(AppDbContext context, TimeProvider timeProvider)
    {
        if (await context.Classes.AnyAsync())
            return;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var random = new Random(2026);
        var used = new HashSet<string>();
        var number = 1001;

        foreach (var plan in ClassPlans)
        {
            var schoolClass = new SchoolClass
            {
                GradeLevel = plan.Grade,
                Section = plan.Section,
                AdvisorName = plan.Advisor,
                Room = plan.Room,
                CreatedAt = now.AddDays(-45),
            };

            for (var i = 0; i < StudentsPerClass; i++)
            {
                string first, last;
                do
                {
                    first = FirstNames[random.Next(FirstNames.Length)];
                    last = LastNames[random.Next(LastNames.Length)];
                }
                while (!used.Add($"{first} {last}"));

                schoolClass.Students.Add(new Student
                {
                    StudentNumber = (number++).ToString(),
                    FirstName = first,
                    LastName = last,
                    Email = i % 4 == 3 ? null : $"{Ascii(first)}.{Ascii(last)}@ogrenci.example.com",
                    CreatedAt = now.AddDays(-45),
                });
            }

            context.Classes.Add(schoolClass);
        }

        await context.SaveChangesAsync();
    }

    internal static string Ascii(string value) => value
        .Replace("ı", "i").Replace("İ", "i").Replace("ş", "s").Replace("Ş", "s").Replace("ğ", "g")
        .Replace("ü", "u").Replace("Ü", "u").Replace("ö", "o").Replace("Ö", "o").Replace("ç", "c").Replace("Ç", "c")
        .ToLowerInvariant();
}

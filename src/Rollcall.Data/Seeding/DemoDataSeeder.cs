using Microsoft.EntityFrameworkCore;
using Rollcall.Data.Entities;

namespace Rollcall.Data.Seeding;

/// <summary>
/// A small high school: five classes with 24 students each, six weeks of daily attendance and two written
/// exams per course. Each part is only added when its table is empty.
/// </summary>
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
        await SeedClassesAsync(context, timeProvider);
        await SeedAttendanceAsync(context, timeProvider);
        await SeedGradesAsync(context, timeProvider);
    }

    private static async Task SeedClassesAsync(AppDbContext context, TimeProvider timeProvider)
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

    public const int AttendanceDays = 30;

    /// <summary>The last 30 school days before today; today itself is taken for the first three classes only.</summary>
    private static async Task SeedAttendanceAsync(AppDbContext context, TimeProvider timeProvider)
    {
        if (await context.Attendance.AnyAsync())
            return;

        var today = SchoolTime.Today(timeProvider);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var days = new List<DateOnly>();
        for (var day = today.AddDays(-1); days.Count < AttendanceDays; day = day.AddDays(-1))
        {
            if (SchoolTime.IsSchoolDay(day))
                days.Add(day);
        }

        var classes = await context.Classes.OrderBy(c => c.GradeLevel).ThenBy(c => c.Section)
            .Select(c => new { c.Id, Students = c.Students.Where(s => s.IsActive).OrderBy(s => s.Id).Select(s => s.Id).ToList() })
            .ToListAsync();

        var random = new Random(7);
        for (var index = 0; index < classes.Count; index++)
        {
            var schoolClass = classes[index];
            var classDays = SchoolTime.IsSchoolDay(today) && index < 3 ? days.Prepend(today) : days;

            // Most students are rarely away; two per class miss school often enough to show up on the dashboard.
            var risk = schoolClass.Students.ToDictionary(id => id, id => id % 24 is 5 or 17 ? 0.22 : 0.025 + random.NextDouble() * 0.03);

            foreach (var day in classDays)
            {
                foreach (var studentId in schoolClass.Students)
                {
                    var roll = random.NextDouble();
                    var status = roll < risk[studentId] ? AttendanceStatus.Absent
                        : roll < risk[studentId] + 0.03 ? AttendanceStatus.Late
                        : roll < risk[studentId] + 0.04 ? AttendanceStatus.Excused
                        : AttendanceStatus.Present;

                    context.Attendance.Add(new AttendanceRecord
                    {
                        StudentId = studentId,
                        ClassId = schoolClass.Id,
                        Date = day,
                        Status = status,
                        RecordedAt = now,
                    });
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private static readonly (int CourseId, int Offset)[] ExamCourses = [(1, 4), (2, -6), (3, -3), (6, 2), (8, 6)];

    /// <summary>Two graded written exams per course a few weeks back, and one upcoming exam without scores.</summary>
    private static async Task SeedGradesAsync(AppDbContext context, TimeProvider timeProvider)
    {
        if (await context.Exams.AnyAsync())
            return;

        var today = SchoolTime.Today(timeProvider);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var random = new Random(11);
        var classes = await context.Classes.Include(c => c.Students.Where(s => s.IsActive)).ToListAsync();

        foreach (var schoolClass in classes)
        {
            var ability = schoolClass.Students.ToDictionary(s => s.Id, _ => Normal(random, 68, 11));

            for (var c = 0; c < ExamCourses.Length; c++)
            {
                var (courseId, offset) = ExamCourses[c];
                var plan = new[]
                {
                    ("1. yazılı", today.AddDays(-30 + c * 2), true),
                    ("2. yazılı", today.AddDays(-9 + c), true),
                    ("3. yazılı", today.AddDays(12 + c * 2), false),
                };

                foreach (var (title, date, graded) in plan)
                {
                    var exam = new Exam
                    {
                        ClassId = schoolClass.Id,
                        CourseId = courseId,
                        Title = title,
                        Date = SchoolTime.IsSchoolDay(date) ? date : date.AddDays(date.DayOfWeek == DayOfWeek.Saturday ? 2 : 1),
                        CreatedAt = now,
                    };

                    if (graded)
                    {
                        foreach (var student in schoolClass.Students)
                        {
                            var score = Math.Clamp(Math.Round(ability[student.Id] + offset + Normal(random, 0, 8)), 15, 100);
                            exam.Grades.Add(new Grade { StudentId = student.Id, Score = (decimal)score });
                        }
                    }

                    context.Exams.Add(exam);
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private static double Normal(Random random, double mean, double deviation)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        return mean + deviation * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    internal static string Ascii(string value) => value
        .Replace("ı", "i").Replace("İ", "i").Replace("ş", "s").Replace("Ş", "s").Replace("ğ", "g")
        .Replace("ü", "u").Replace("Ü", "u").Replace("ö", "o").Replace("Ö", "o").Replace("ç", "c").Replace("Ç", "c")
        .ToLowerInvariant();
}

namespace Rollcall.Data.Entities;

public class Course
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<Exam> Exams { get; set; } = [];
}

/// <summary>An assessment for one class in one course, e.g. "1. yazılı" in 9-A Matematik.</summary>
public class Exam
{
    public int Id { get; set; }

    public int ClassId { get; set; }

    public SchoolClass? Class { get; set; }

    public int CourseId { get; set; }

    public Course? Course { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Grade> Grades { get; set; } = [];
}

public class Grade
{
    public int Id { get; set; }

    public int ExamId { get; set; }

    public Exam? Exam { get; set; }

    public int StudentId { get; set; }

    public Student? Student { get; set; }

    /// <summary>0–100, with at most two decimals.</summary>
    public decimal Score { get; set; }
}

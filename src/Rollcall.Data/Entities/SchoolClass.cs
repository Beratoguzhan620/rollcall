namespace Rollcall.Data.Entities;

/// <summary>A class (şube) such as 9-A: a grade level plus a section letter.</summary>
public class SchoolClass
{
    public int Id { get; set; }

    public int GradeLevel { get; set; }

    public string Section { get; set; } = string.Empty;

    /// <summary>Homeroom teacher (sınıf öğretmeni / rehber öğretmen).</summary>
    public string? AdvisorName { get; set; }

    public string? Room { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Student> Students { get; set; } = [];

    public string Name => $"{GradeLevel}-{Section}";
}

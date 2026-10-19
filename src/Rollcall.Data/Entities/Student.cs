namespace Rollcall.Data.Entities;

public class Student
{
    public int Id { get; set; }

    /// <summary>School number, unique across the school.</summary>
    public string StudentNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public int ClassId { get; set; }

    public SchoolClass? Class { get; set; }

    /// <summary>False once the student has left the school; their records stay.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}

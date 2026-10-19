namespace Rollcall.Data.Entities;

public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    Late = 3,
    Excused = 4,
}

/// <summary>One student's daily attendance on one school day.</summary>
public class AttendanceRecord
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public Student? Student { get; set; }

    /// <summary>The class the record was taken in, kept even if the student changes class later.</summary>
    public int ClassId { get; set; }

    public SchoolClass? Class { get; set; }

    public DateOnly Date { get; set; }

    public AttendanceStatus Status { get; set; }

    public DateTime RecordedAt { get; set; }
}

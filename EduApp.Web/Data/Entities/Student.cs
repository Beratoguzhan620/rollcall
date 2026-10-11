namespace EduApp.Web.Data.Entities;

public class Student
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;


    //Foreign key
    public int ClassId { get; set; }

    //Navigation property
    public virtual Class Class { get; set; } = default!;
}

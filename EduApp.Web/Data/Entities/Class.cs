namespace EduApp.Web.Data.Entities;

public class Class
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;


    //Navigation property
    public virtual List<Student> Students { get; set; } = default!;
}

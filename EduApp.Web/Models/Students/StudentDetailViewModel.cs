namespace EduApp.Web.Models;


//Veri tabanındaki öğrenci bilgilerini ekranda göstermek için kullandığım ViewModel
public class StudentDetailViewModel
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
}

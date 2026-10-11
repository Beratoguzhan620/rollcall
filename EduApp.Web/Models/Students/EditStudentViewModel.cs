using System.ComponentModel.DataAnnotations;

namespace EduApp.Web.Models;

public class EditStudentViewModel
{

    [Required]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public int ClassId { get; set; }
}


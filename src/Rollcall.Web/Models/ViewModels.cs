using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Rollcall.Application.Classes;
using Rollcall.Application.Common;
using Rollcall.Application.Students;
using Rollcall.Data;

namespace Rollcall.Web.Models;

public sealed class ClassFormModel
{
    public int? Id { get; set; }

    [Display(Name = "Sınıf seviyesi")]
    [Required(ErrorMessage = "Sınıf seviyesi zorunlu.")]
    [Range(1, 12, ErrorMessage = "Sınıf seviyesi 1 ile 12 arasında olmalı.")]
    public int? GradeLevel { get; set; }

    [Display(Name = "Şube")]
    [Required(ErrorMessage = "Şube zorunlu.")]
    [StringLength(Limits.Section, ErrorMessage = "Şube en fazla {1} harf olabilir.")]
    public string Section { get; set; } = string.Empty;

    [Display(Name = "Sınıf öğretmeni")]
    [StringLength(Limits.PersonName, ErrorMessage = "En fazla {1} karakter.")]
    public string? AdvisorName { get; set; }

    [Display(Name = "Derslik")]
    [StringLength(Limits.Room, ErrorMessage = "En fazla {1} karakter.")]
    public string? Room { get; set; }

    public bool IsEdit => Id.HasValue;
}

public sealed class ClassDetailViewModel
{
    public required ClassRow Class { get; init; }

    public required List<StudentRow> Students { get; init; }
}

public sealed class StudentListViewModel
{
    public required PagedList<StudentRow> Result { get; init; }

    public required StudentQuery Query { get; init; }

    public required List<SelectListItem> Classes { get; init; }

    public PagerViewModel Pager => new(Result.Page, Result.PageSize, Result.TotalCount, Result.TotalPages);

    public bool IsFiltered => !string.IsNullOrWhiteSpace(Query.Search) || Query.ClassId is not null || Query.IncludeInactive;
}

public sealed class StudentFormModel
{
    public int? Id { get; set; }

    [Display(Name = "Okul numarası")]
    [Required(ErrorMessage = "Okul numarası zorunlu.")]
    [RegularExpression(@"^\s*\d{1,10}\s*$", ErrorMessage = "Okul numarası en fazla 10 haneli bir sayı olmalı.")]
    public string StudentNumber { get; set; } = string.Empty;

    [Display(Name = "Ad")]
    [Required(ErrorMessage = "Ad zorunlu.")]
    [StringLength(Limits.NamePart, MinimumLength = 2, ErrorMessage = "Ad {2} ile {1} karakter arasında olmalı.")]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Soyad")]
    [Required(ErrorMessage = "Soyad zorunlu.")]
    [StringLength(Limits.NamePart, MinimumLength = 2, ErrorMessage = "Soyad {2} ile {1} karakter arasında olmalı.")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "E-posta")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi yazın.")]
    [StringLength(Limits.Email, ErrorMessage = "E-posta en fazla {1} karakter olabilir.")]
    public string? Email { get; set; }

    [Display(Name = "Sınıf")]
    [Required(ErrorMessage = "Bir sınıf seçin.")]
    public int? ClassId { get; set; }

    [Display(Name = "Okulda kayıtlı")]
    public bool IsActive { get; set; } = true;

    public List<SelectListItem> Classes { get; set; } = [];

    public bool IsEdit => Id.HasValue;

    public StudentInput ToInput() => new(StudentNumber, FirstName, LastName, Email, ClassId ?? 0, IsActive);
}

public sealed class AttendanceViewModel
{
    public Rollcall.Application.Attendance.AttendanceSheet? Sheet { get; init; }

    public required List<SelectListItem> Classes { get; init; }

    public required DateOnly Date { get; init; }

    public required DateOnly Today { get; init; }
}

public sealed class ExamListViewModel
{
    public required List<Rollcall.Application.Grades.ExamRow> Exams { get; init; }

    public required List<SelectListItem> Classes { get; init; }

    public required List<SelectListItem> Courses { get; init; }

    public int? ClassId { get; init; }

    public int? CourseId { get; init; }

    public required DateOnly Today { get; init; }
}

public sealed class ExamFormModel
{
    [Display(Name = "Sınıf")]
    [Required(ErrorMessage = "Bir sınıf seçin.")]
    public int? ClassId { get; set; }

    [Display(Name = "Ders")]
    [Required(ErrorMessage = "Bir ders seçin.")]
    public int? CourseId { get; set; }

    [Display(Name = "Sınav adı")]
    [Required(ErrorMessage = "Sınav adı zorunlu.")]
    [StringLength(Limits.ExamTitle, MinimumLength = 2, ErrorMessage = "Sınav adı {2} ile {1} karakter arasında olmalı.")]
    public string Title { get; set; } = "1. yazılı";

    [Display(Name = "Tarih")]
    [Required(ErrorMessage = "Tarih zorunlu.")]
    public DateOnly? Date { get; set; }

    public List<SelectListItem> Classes { get; set; } = [];

    public List<SelectListItem> Courses { get; set; } = [];
}

public sealed class GradeSheetViewModel
{
    public required Rollcall.Application.Grades.GradeSheet Sheet { get; init; }

    /// <summary>What the teacher typed, shown again when the sheet had errors.</summary>
    public Dictionary<int, string?>? Posted { get; init; }

    public Dictionary<int, string> Errors { get; init; } = [];
}

public sealed class StudentDetailViewModel
{
    public required StudentRow Student { get; init; }

    public required Rollcall.Application.Attendance.StudentAttendance Attendance { get; init; }

    public required Rollcall.Application.Grades.StudentGrades Grades { get; init; }
}

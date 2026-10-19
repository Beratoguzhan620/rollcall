using Microsoft.AspNetCore.Mvc;
using Rollcall.Application.Attendance;
using Rollcall.Application.Common;
using Rollcall.Application.Grades;
using Rollcall.Application.Students;
using Rollcall.Web.Infrastructure;
using Rollcall.Web.Models;

namespace Rollcall.Web.Controllers;

[Route("students")]
public sealed class StudentsController(StudentService students, AttendanceService attendance, GradeService grades, Lookups lookups) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, int? classId, bool all = false, int page = 1)
    {
        var query = new StudentQuery { Search = search, ClassId = classId, IncludeInactive = all, Page = page };
        return View(new StudentListViewModel
        {
            Result = await students.ListAsync(query),
            Query = query,
            Classes = await lookups.ClassesAsync(classId),
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var student = await students.GetAsync(id);
        if (student is null)
            return NotFound();

        return View(new StudentDetailViewModel
        {
            Student = student,
            Attendance = await attendance.GetStudentAsync(id),
            Grades = await grades.GetStudentAsync(id),
        });
    }

    [HttpGet("new")]
    public async Task<IActionResult> Create(int? classId)
    {
        var model = new StudentFormModel { ClassId = classId, StudentNumber = await students.NextNumberAsync() };
        model.Classes = await lookups.ClassesAsync(classId);
        return View("Form", model);
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create(StudentFormModel model)
    {
        model.Id = null;
        model.IsActive = true;
        if (!ModelState.IsValid)
            return await FormAsync(model);

        var result = await students.CreateAsync(model.ToInput());
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message);
            return await FormAsync(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Detail), new { id = result.Value });
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var student = await students.GetAsync(id);
        if (student is null)
            return NotFound();

        return await FormAsync(new StudentFormModel
        {
            Id = student.Id,
            StudentNumber = student.StudentNumber,
            FirstName = student.FirstName,
            LastName = student.LastName,
            Email = student.Email,
            ClassId = student.ClassId,
            IsActive = student.IsActive,
        });
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, StudentFormModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return await FormAsync(model);

        var result = await students.UpdateAsync(id, model.ToInput());
        if (result.Status == ResultStatus.NotFound)
            return NotFound();

        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message);
            return await FormAsync(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await students.DeleteAsync(id);
        if (result.Status == ResultStatus.NotFound)
            return NotFound();

        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return result.Success ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Detail), new { id });
    }

    private async Task<IActionResult> FormAsync(StudentFormModel model)
    {
        model.Classes = await lookups.ClassesAsync(model.ClassId);
        return View("Form", model);
    }
}

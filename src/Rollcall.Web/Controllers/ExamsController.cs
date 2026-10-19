using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Rollcall.Application.Common;
using Rollcall.Application.Grades;
using Rollcall.Web.Infrastructure;
using Rollcall.Web.Models;

namespace Rollcall.Web.Controllers;

[Route("exams")]
public sealed class ExamsController(GradeService grades, Lookups lookups, TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? classId, int? courseId)
    {
        var courses = await grades.GetCoursesAsync();
        return View(new ExamListViewModel
        {
            Exams = await grades.GetExamsAsync(classId, courseId),
            Classes = await lookups.ClassesAsync(classId),
            Courses = courses.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == courseId)).ToList(),
            ClassId = classId,
            CourseId = courseId,
            Today = Rollcall.Data.SchoolTime.Today(timeProvider),
        });
    }

    /// <summary>The grade book of one class: every student with every graded exam and the averages.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(int classId, string? format)
    {
        var classes = await lookups.ClassesAsync(classId);
        var name = classes.FirstOrDefault(c => c.Value == classId.ToString())?.Text;
        if (name is null)
            return NotFound();

        var book = await grades.GetGradeBookAsync(classId);
        var file = Exports.Build(book.Rows, Exports.GradeBookColumns(book), format, $"notlar-{name}", $"Notlar {name}");
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("new")]
    public async Task<IActionResult> Create(int? classId) =>
        await FormAsync(new ExamFormModel { ClassId = classId, Date = Rollcall.Data.SchoolTime.Today(timeProvider) });

    [HttpPost("new")]
    public async Task<IActionResult> Create(ExamFormModel model)
    {
        if (!ModelState.IsValid)
            return await FormAsync(model);

        var result = await grades.CreateExamAsync(new ExamInput(model.ClassId!.Value, model.CourseId!.Value, model.Title, model.Date!.Value));
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message);
            return await FormAsync(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Sheet), new { id = result.Value });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Sheet(int id)
    {
        var sheet = await grades.GetSheetAsync(id);
        return sheet is null ? NotFound() : View(new GradeSheetViewModel { Sheet = sheet });
    }

    /// <summary>Scores arrive as text so both "85,5" and "85.5" work whatever the browser's locale.</summary>
    [HttpPost("{id:int}")]
    public async Task<IActionResult> Sheet(int id, Dictionary<int, string?> scores)
    {
        var parsed = new Dictionary<int, decimal?>();
        var errors = new Dictionary<int, string>();
        foreach (var (studentId, text) in scores)
        {
            var value = text?.Trim().Replace(',', '.');
            if (string.IsNullOrEmpty(value))
                parsed[studentId] = null;
            else if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var score) && score is >= 0 and <= 100 && decimal.Round(score, 2) == score)
                parsed[studentId] = score;
            else
                errors[studentId] = "0–100 arası bir not yazın.";
        }

        if (errors.Count == 0)
        {
            var result = await grades.SaveScoresAsync(id, parsed);
            if (result.Status == ResultStatus.NotFound)
                return NotFound();
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Sheet), new { id });
            }
            ModelState.AddModelError(string.Empty, result.Message);
        }
        else
        {
            ModelState.AddModelError(string.Empty, $"{errors.Count} notta hata var; işaretli satırları düzeltin.");
        }

        var sheet = await grades.GetSheetAsync(id);
        if (sheet is null)
            return NotFound();

        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return View(new GradeSheetViewModel { Sheet = sheet, Posted = scores, Errors = errors });
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await grades.DeleteExamAsync(id);
        if (result.Status == ResultStatus.NotFound)
            return NotFound();

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> FormAsync(ExamFormModel model)
    {
        model.Classes = await lookups.ClassesAsync(model.ClassId);
        model.Courses = (await grades.GetCoursesAsync()).Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == model.CourseId)).ToList();
        return View("Form", model);
    }
}

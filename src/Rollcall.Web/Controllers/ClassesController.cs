using Microsoft.AspNetCore.Mvc;
using Rollcall.Application.Classes;
using Rollcall.Application.Common;
using Rollcall.Application.Students;
using Rollcall.Web.Models;

namespace Rollcall.Web.Controllers;

[Route("classes")]
public sealed class ClassesController(ClassService classes, StudentService students) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await classes.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var schoolClass = await classes.GetAsync(id);
        if (schoolClass is null)
            return NotFound();

        var roster = await students.ListAsync(new StudentQuery { ClassId = id, PageSize = 100 });
        return View(new ClassDetailViewModel { Class = schoolClass, Students = roster.Items });
    }

    [HttpGet("new")]
    public IActionResult Create() => View("Form", new ClassFormModel());

    [HttpPost("new")]
    public async Task<IActionResult> Create(ClassFormModel model)
    {
        model.Id = null;
        if (!ModelState.IsValid)
            return View("Form", model);

        var result = await classes.CreateAsync(ToInput(model));
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message);
            return View("Form", model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Detail), new { id = result.Value });
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var schoolClass = await classes.GetAsync(id);
        if (schoolClass is null)
            return NotFound();

        return View("Form", new ClassFormModel
        {
            Id = schoolClass.Id,
            GradeLevel = schoolClass.GradeLevel,
            Section = schoolClass.Section,
            AdvisorName = schoolClass.AdvisorName,
            Room = schoolClass.Room,
        });
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, ClassFormModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return View("Form", model);

        var result = await classes.UpdateAsync(id, ToInput(model));
        if (result.Status == ResultStatus.NotFound)
            return NotFound();

        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message);
            return View("Form", model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await classes.DeleteAsync(id);
        if (result.Status == ResultStatus.NotFound)
            return NotFound();

        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return result.Success ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Detail), new { id });
    }

    private static ClassInput ToInput(ClassFormModel model) =>
        new(model.GradeLevel ?? 0, model.Section, model.AdvisorName, model.Room);
}

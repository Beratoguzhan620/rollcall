using Microsoft.AspNetCore.Mvc;
using Rollcall.Application.Attendance;
using Rollcall.Application.Common;
using Rollcall.Data.Entities;
using Rollcall.Web.Infrastructure;
using Rollcall.Web.Models;

namespace Rollcall.Web.Controllers;

[Route("attendance")]
public sealed class AttendanceController(AttendanceService attendance, Lookups lookups) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? classId, DateOnly? date)
    {
        var classes = await lookups.ClassesAsync(classId);
        var day = date ?? attendance.Today;
        var selected = classId ?? (classes.Count > 0 ? int.Parse(classes[0].Value) : (int?)null);
        if (classId is null && classes.Count > 0)
            classes[0].Selected = true;

        var sheet = selected is int id ? await attendance.GetSheetAsync(id, day) : null;
        if (selected is not null && sheet is null)
            return NotFound();

        return View(new AttendanceViewModel { Sheet = sheet, Classes = classes, Date = day, Today = attendance.Today });
    }

    [HttpPost("")]
    public async Task<IActionResult> Save(int classId, DateOnly date, Dictionary<int, AttendanceStatus> statuses)
    {
        var result = await attendance.SaveAsync(classId, date, statuses);
        if (result.Status == ResultStatus.NotFound)
            return NotFound();

        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Index), new { classId, date = date.ToString("yyyy-MM-dd") });
    }
}

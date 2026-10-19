using Microsoft.AspNetCore.Mvc;
using Rollcall.Application.Dashboard;

namespace Rollcall.Web.Controllers;

public sealed class HomeController(DashboardService dashboard) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await dashboard.GetAsync());

    [Route("error")]
    public IActionResult Error() => View("~/Views/Shared/Error.cshtml");

    [Route("not-found")]
    public IActionResult PageNotFound()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("~/Views/Shared/NotFound.cshtml");
    }
}

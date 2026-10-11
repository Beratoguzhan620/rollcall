using EduApp.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace EduApp.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
 
}

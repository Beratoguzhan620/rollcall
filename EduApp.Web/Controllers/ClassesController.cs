using EduApp.Web.Data;
using Microsoft.AspNetCore.Mvc;

namespace EduApp.Web.Controllers;

public class ClassesController(AppDbContext context) : Controller
{

    //TODO: StudentController'da yapıldığı gibi her bir işlem için ayrı ayrı ViewModel'ler oluşturulacak. CRUD işlemleri tamamlanacak.
    public IActionResult Index()
    {
        var classes = context.Classes.ToList();
        return View(classes);
    }
}

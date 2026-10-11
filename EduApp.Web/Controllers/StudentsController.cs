using EduApp.Web.Data;
using EduApp.Web.Data.Entities;
using EduApp.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduApp.Web.Controllers;

public class StudentsController(AppDbContext dbContext) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {

        //Include: Veri tabanından öğrenci verileri ile birlikte sınıf bilgilerini de çekmek için kullanılır.

        //Select: Veritabanından çekilen öğrenci verilerini StudentListViewModel'e dönüştürmek için kullanılır. 
        var students = dbContext.Students
                        .Include(s => s.Class)
                        .Select(s => new StudentDetailViewModel
                        {
                            Id = s.Id,
                            FirstName = s.FirstName,
                            LastName = s.LastName,
                            Email = s.Email,
                            ClassId = s.ClassId,
                            ClassName = s.Class.Name
                        }).ToList();



        return View(students);
    }


    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.Classes = GetClasses();

        return View();
    }


    [HttpPost]
    public IActionResult Create(CreateStudentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Classes = GetClasses();
            return View(model);
        }


        //ModelView -> Entity'ye dönüştürme işlemi (Mapping)
        var entity = new Student
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            ClassId = model.ClassId
        };

        dbContext.Students.Add(entity);
        dbContext.SaveChanges();



        return RedirectToAction(nameof(Index));
    }


    [HttpGet]
    public IActionResult Edit(int id)
    {
        ViewBag.Classes = GetClasses();

        var student = dbContext.Students.Find(id);

        if (student is null)
            return RedirectToAction(nameof(Index));


        var viewModel = new EditStudentViewModel
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            Email = student.Email,
            ClassId = student.ClassId
        };


        return View(viewModel);
    }


    [HttpPost]
    public IActionResult Edit(int id, EditStudentViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Classes = GetClasses();
            return View(viewModel);
        }

        var student = dbContext.Students.Find(id);

        if (student is null)
            return RedirectToAction(nameof(Index));


        //EfCore change tracker bu değişikleri algılar ve SaveChanges() çağrıldığında ilgili SQL UPDATE komutunu oluşturur.
        student.FirstName = viewModel.FirstName;
        student.LastName = viewModel.LastName;
        student.Email = viewModel.Email;
        student.ClassId = viewModel.ClassId;

        dbContext.SaveChanges();

        return RedirectToAction(nameof(Index));
    }



    [HttpGet]
    public IActionResult Detail(int id)
    {
        var student = dbContext.Students.Include(s => s.Class).FirstOrDefault(s => s.Id == id);

        if (student is null)
            return RedirectToAction(nameof(Index));


        var viewModel = new StudentDetailViewModel
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            Email = student.Email,
            ClassId = student.ClassId,
            ClassName = student.Class.Name
        };

        return View(viewModel);
    }


    [HttpGet]
    public IActionResult Delete(int id)
    {
        var student = dbContext.Students.Include(s => s.Class).FirstOrDefault(s => s.Id == id);

        if (student is null)
            return RedirectToAction(nameof(Index));


        var viewModel = new StudentDetailViewModel
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            Email = student.Email,
            ClassId = student.ClassId,
            ClassName = student.Class.Name
        };

        return View(viewModel);
    }

    [HttpPost]
    public IActionResult DeleteConfirm(int id)
    {
        var student = dbContext.Students.Find(id);

        if (student is null)
        {
            return RedirectToAction("Index");
        }

        dbContext.Students.Remove(student);
        dbContext.SaveChanges();

        return RedirectToAction("Index");
    }



    private List<ClassDetailViewModel> GetClasses()
    {
        return dbContext.Classes.Select(c => new ClassDetailViewModel
        {
            Id = c.Id,
            Name = c.Name
        }).ToList();
    }


}

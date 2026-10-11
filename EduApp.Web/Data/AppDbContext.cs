using EduApp.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace EduApp.Web.Data;


//DbContext: Veri tabanının uygulama tarafındaki temsilcisi
//Primary Constructor
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    //DbSet: Veri tabanındaki bir tabloyu temsil eder.
    public DbSet<Student> Students { get; set; }
    public DbSet<Class> Classes { get; set; }


    override protected void OnModelCreating(ModelBuilder modelBuilder)
    {
        //Bu projedeki tüm config dosyalarını otomatik olarak uygula.   
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

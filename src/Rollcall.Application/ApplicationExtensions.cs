using Microsoft.Extensions.DependencyInjection;
using Rollcall.Application.Attendance;
using Rollcall.Application.Classes;
using Rollcall.Application.Dashboard;
using Rollcall.Application.Grades;
using Rollcall.Application.Students;

namespace Rollcall.Application;

public static class ApplicationExtensions
{
    public static IServiceCollection AddRollcallServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ClassService>();
        services.AddScoped<StudentService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<GradeService>();
        return services;
    }
}

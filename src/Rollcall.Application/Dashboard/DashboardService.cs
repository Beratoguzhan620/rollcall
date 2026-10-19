using Microsoft.EntityFrameworkCore;
using Rollcall.Application.Attendance;
using Rollcall.Application.Classes;
using Rollcall.Data;

namespace Rollcall.Application.Dashboard;

public sealed record DashboardSummary(
    int ClassCount,
    int StudentCount,
    int InactiveCount,
    List<ClassRow> Classes,
    DateOnly Today,
    List<ClassToday> TodayByClass,
    List<AbsenceLeader> MostAbsent)
{
    public int TakenToday => TodayByClass.Count(c => c.IsTaken);
    public int AbsentToday => TodayByClass.Sum(c => c.Absent);
}

public sealed class DashboardService(AppDbContext context, ClassService classes, AttendanceService attendance)
{
    public async Task<DashboardSummary> GetAsync()
    {
        var classList = await classes.GetAllAsync();
        var active = await context.Students.CountAsync(s => s.IsActive);
        var inactive = await context.Students.CountAsync(s => !s.IsActive);
        return new DashboardSummary(
            classList.Count, active, inactive, classList,
            attendance.Today, await attendance.GetTodayAsync(), await attendance.GetMostAbsentAsync());
    }
}

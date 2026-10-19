using Microsoft.EntityFrameworkCore;
using Rollcall.Application.Classes;
using Rollcall.Data;

namespace Rollcall.Application.Dashboard;

public sealed record DashboardSummary(int ClassCount, int StudentCount, int InactiveCount, List<ClassRow> Classes);

public sealed class DashboardService(AppDbContext context, ClassService classes)
{
    public async Task<DashboardSummary> GetAsync()
    {
        var classList = await classes.GetAllAsync();
        var active = await context.Students.CountAsync(s => s.IsActive);
        var inactive = await context.Students.CountAsync(s => !s.IsActive);
        return new DashboardSummary(classList.Count, active, inactive, classList);
    }
}

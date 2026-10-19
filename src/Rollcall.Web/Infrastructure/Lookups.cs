using Microsoft.AspNetCore.Mvc.Rendering;
using Rollcall.Application.Classes;

namespace Rollcall.Web.Infrastructure;

/// <summary>Select lists shared by several views.</summary>
public sealed class Lookups(ClassService classes)
{
    public async Task<List<SelectListItem>> ClassesAsync(int? selected = null) =>
        (await classes.GetAllAsync())
            .Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == selected))
            .ToList();
}

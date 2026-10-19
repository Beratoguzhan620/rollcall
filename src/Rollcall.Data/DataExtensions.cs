using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rollcall.Data.Seeding;

namespace Rollcall.Data;

public static class DataExtensions
{
    public static IServiceCollection AddRollcallData(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }

    /// <summary>Applies pending migrations and, when asked, fills an empty database with a demo school.</summary>
    public static async Task PrepareDatabaseAsync(this IServiceProvider services, bool seedDemoData, TimeProvider? timeProvider = null)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.MigrateAsync();

        if (seedDemoData)
            await DemoDataSeeder.SeedAsync(context, timeProvider ?? TimeProvider.System);
    }
}

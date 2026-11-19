using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rollcall.Data.Seeding;

namespace Rollcall.Data;

public static class DataExtensions
{
    /// <param name="provider">
    /// "SqlServer" (the default) or "Sqlite". SQLite is for the public demo: one small container with the database
    /// in a file that starts empty on every restart, so each visitor finds the demo school as it was seeded.
    /// </param>
    public static IServiceCollection AddRollcallData(this IServiceCollection services, string? connectionString, string? provider = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        else
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

        return services;
    }

    /// <summary>
    /// Applies pending migrations (SQLite: creates the schema from the model, as the migrations are written for SQL
    /// Server) and, when asked, fills an empty database with a demo school.
    /// </summary>
    public static async Task PrepareDatabaseAsync(this IServiceProvider services, bool seedDemoData, TimeProvider? timeProvider = null)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (context.Database.IsSqlite())
            await context.Database.EnsureCreatedAsync();
        else
            await context.Database.MigrateAsync();

        if (seedDemoData)
            await DemoDataSeeder.SeedAsync(context, timeProvider ?? TimeProvider.System);
    }
}

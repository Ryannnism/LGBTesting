using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Tools;

/// <summary>
/// Wipes dev transactional data. Keeps package catalog, form/workflow templates, division groups,
/// and LGB internal staff (Sharon + resolution prep team).
/// </summary>
public static class DevDatabaseReset
{
    public static async Task RunAsync(AppDbContext context, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var isSqlite = string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase);
        var isPostgres = string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "Postgresql", StringComparison.OrdinalIgnoreCase);

        if (!isSqlite && !isPostgres)
        {
            Console.Error.WriteLine("reset-dev-db is only supported for SQLite or Postgres development databases.");
            return;
        }

        Console.WriteLine($"Resetting development database ({provider})…");

        if (isSqlite)
        {
            await context.Database.EnsureDeletedAsync();
            DatabaseBootstrap.ApplyMigrations(context, runSqliteHandMigrator: true);
        }
        else
        {
            // Postgres: drop/recreate schema via EF (no local file to delete).
            await context.Database.EnsureDeletedAsync();
            DatabaseBootstrap.ApplyMigrations(context, runSqliteHandMigrator: false);
        }

        WorkflowConfigSeeder.SeedReferenceData(context);
        FigmaProductCatalog.SyncCatalog(context);
        InternalStaffSeeder.Seed(context, resetPasswordsInDevelopment: true);

        foreach (var user in await context.Users
                     .Where(u => u.Role == UserRoles.Admin || u.Role == UserRoles.User)
                     .ToListAsync())
            user.MustChangePassword = false;

        await context.SaveChangesAsync();

        ClearUploadArtifacts(configuration);

        var productCount = await context.Products.CountAsync();
        var staffCount = await context.Users.CountAsync(u => u.Role == UserRoles.Admin || u.Role == UserRoles.User);
        var customerCount = await context.Customers.CountAsync();

        Console.WriteLine($"Done. Products: {productCount}, internal users: {staffCount}, customers: {customerCount}.");
        Console.WriteLine("Internal logins (password: password123): sharon@lgb.test, ngpohli@lgb.test, nita@lgb.test, siti@lgb.test, nadia@lgb.test");
    }

    private static void ClearUploadArtifacts(IConfiguration configuration)
    {
        var uploadRoot = Environment.GetEnvironmentVariable("LGB_UPLOAD_ROOT")
            ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        if (!Directory.Exists(uploadRoot))
            return;

        try
        {
            Directory.Delete(uploadRoot, recursive: true);
            Directory.CreateDirectory(uploadRoot);
            Console.WriteLine($"Cleared uploads at {uploadRoot}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: could not clear uploads: {ex.Message}");
        }
    }
}

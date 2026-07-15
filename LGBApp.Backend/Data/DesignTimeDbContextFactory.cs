using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LGBApp.Backend.Data;

/// <summary>Design-time factory so <c>dotnet ef migrations</c> honors Database__Provider.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "Sqlite";
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                   ?? "Data Source=ef-design-time.db";

        var builder = new DbContextOptionsBuilder<AppDbContext>();
        if (provider.StartsWith("Postgres", StringComparison.OrdinalIgnoreCase))
            builder.UseNpgsql(conn);
        else if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            builder.UseSqlServer(conn);
        else
            builder.UseSqlite(conn);

        builder.ReplaceService<IMigrationsAssembly, ProviderFilteredMigrationsAssembly>();
        return new AppDbContext(builder.Options);
    }
}

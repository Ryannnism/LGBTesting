using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;

namespace LGBApp.Backend.Data;

/// <summary>
/// Keeps SQLite and Postgres EF migrations in one assembly without applying both.
/// Postgres migrations live under <c>Migrations.Postgres</c>; everything else is for Sqlite/SqlServer.
/// </summary>
#pragma warning disable EF1001 // Internal MigrationsAssembly — dual-provider filter
public sealed class ProviderFilteredMigrationsAssembly : MigrationsAssembly
{
    private readonly bool _postgres;
    private IReadOnlyDictionary<string, TypeInfo>? _filtered;

    public ProviderFilteredMigrationsAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator idGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
        : base(currentContext, options, idGenerator, logger)
    {
        _postgres = currentContext.Context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
    }

    public override IReadOnlyDictionary<string, TypeInfo> Migrations
    {
        get
        {
            if (_filtered != null)
                return _filtered;

            var filtered = base.Migrations
                .Where(pair =>
                {
                    var ns = pair.Value.Namespace ?? string.Empty;
                    var isPostgresMigration = ns.Contains(".Postgres", StringComparison.Ordinal)
                        || ns.EndsWith(".Postgres", StringComparison.Ordinal);
                    return _postgres ? isPostgresMigration : !isPostgresMigration;
                })
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            _filtered = new ReadOnlyDictionary<string, TypeInfo>(filtered);
            return _filtered;
        }
    }
}
#pragma warning restore EF1001

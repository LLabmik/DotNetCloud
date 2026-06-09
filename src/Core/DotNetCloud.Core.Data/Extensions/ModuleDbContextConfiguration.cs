using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DotNetCloud.Core.Data.Extensions;

/// <summary>
/// Shared helper for configuring module DbContexts with the correct database provider.
/// Used by both Core.Server and module main projects for Blazor UI service registration.
/// </summary>
public static class ModuleDbContextConfiguration
{
    /// <summary>
    /// Configures a module DbContext with the appropriate database provider settings.
    /// </summary>
    /// <param name="options">The DbContext options builder.</param>
    /// <param name="provider">The configured database provider.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="migrationsAssembly">Optional migration assembly name (e.g., "DotNetCloud.Modules.Tracks.Data.SqlServer").</param>
    public static void Configure(
        DbContextOptionsBuilder options,
        DatabaseProvider provider,
        string connectionString,
        string? migrationsAssembly = null)
    {
        switch (provider)
        {
            case DatabaseProvider.PostgreSQL:
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    npgsqlOptions.CommandTimeout(30);
                });
                break;

            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, sqlServerOptions =>
                {
                    sqlServerOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    sqlServerOptions.CommandTimeout(30);

                    if (!string.IsNullOrEmpty(migrationsAssembly))
                    {
                        sqlServerOptions.MigrationsAssembly(migrationsAssembly);
                    }
                });
                break;

            default:
                throw new ArgumentException($"Unsupported database provider: {provider}", nameof(provider));
        }

        // Suppress pending model changes warning for modules that don't have
        // a dedicated SQL Server migrations assembly. Their migrations were
        // generated for the PostgreSQL provider.
        options.ConfigureWarnings(warnings =>
        {
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning);
        });
    }
}

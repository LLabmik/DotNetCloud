using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Extensions;

/// <summary>
/// Centralized database resilience configuration used by every EF Core DbContext
/// in the platform (Core, modules, CLI, server). Keeps retry counts, retry delays,
/// and command timeouts consistent so a transient database outage fails fast and
/// recovers automatically instead of hanging requests.
/// </summary>
public static class DbResiliencePolicy
{
    /// <summary>Maximum number of transient retry attempts per command.</summary>
    public const int MaxRetryCount = 5;

    /// <summary>Upper bound for the exponential retry delay.</summary>
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>EF Core command timeout (per query/command execution).</summary>
    public static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Applies the standard provider-specific resilience settings to a DbContext
    /// options builder. Call this for every relational DbContext registration.
    /// </summary>
    /// <param name="options">The options builder being configured.</param>
    /// <param name="provider">Resolved <see cref="DatabaseProvider"/>.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="migrationsAssembly">Optional migrations assembly (SQL Server only).</param>
    public static void Configure(
        DbContextOptionsBuilder options,
        DatabaseProvider provider,
        string connectionString,
        string? migrationsAssembly = null)
    {
        switch (provider)
        {
            case DatabaseProvider.PostgreSQL:
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay, null);
                    npgsql.CommandTimeout((int)CommandTimeout.TotalSeconds);
                });
                break;

            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay, null);
                    sql.CommandTimeout((int)CommandTimeout.TotalSeconds);

                    if (!string.IsNullOrEmpty(migrationsAssembly))
                        sql.MigrationsAssembly(migrationsAssembly);
                });
                break;

            default:
                throw new ArgumentException($"Unsupported database provider: {provider}", nameof(provider));
        }
    }
}

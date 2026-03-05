using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DotNetCloud.Core.Data.Naming;

namespace DotNetCloud.Core.Data.Context;

/// <summary>
/// Design-time factory for creating CoreDbContext instances.
/// This is required by EF Core tooling to generate migrations.
/// Uses PostgreSQL as the default provider for migration generation.
/// </summary>
/// <remarks>
/// <para>
/// To generate a PostgreSQL migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Core/DotNetCloud.Core.Data --output-dir Migrations
/// </code>
/// </para>
/// <para>
/// To generate a SQL Server migration, set the environment variable before running:
/// <code>
/// $env:CORE_DB_PROVIDER = "SqlServer"
/// dotnet ef migrations add MigrationName_SqlServer --project src/Core/DotNetCloud.Core.Data --output-dir Migrations/SqlServer
/// </code>
/// </para>
/// </remarks>
public class CoreDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    /// <inheritdoc />
    public CoreDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("CORE_DB_PROVIDER") ?? "PostgreSQL";
        var options = new DbContextOptionsBuilder<CoreDbContext>();

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            const string connectionString = "Server=localhost;Database=dotnetcloud_dev;Trusted_Connection=True;TrustServerCertificate=True";
            var namingStrategy = new SqlServerNamingStrategy();

            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                sqlOptions.CommandTimeout(30);
            });

            return new CoreDbContext(options.Options, namingStrategy);
        }
        else
        {
            const string connectionString = "Host=localhost;Database=dotnetcloud_dev;Username=postgres;Password=postgres";
            var namingStrategy = new PostgreSqlNamingStrategy();

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                npgsqlOptions.CommandTimeout(30);
            });

            return new CoreDbContext(options.Options, namingStrategy);
        }
    }
}

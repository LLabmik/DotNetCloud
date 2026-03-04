using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Files.Data;

/// <summary>
/// Design-time factory for creating <see cref="FilesDbContext"/> instances.
/// Required by EF Core tooling to generate migrations.
/// </summary>
/// <remarks>
/// Set the <c>EF_PROVIDER</c> environment variable to <c>sqlserver</c> to generate
/// SQL Server migrations. Defaults to PostgreSQL.
/// </remarks>
public class FilesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FilesDbContext>
{
    /// <inheritdoc />
    public FilesDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("EF_PROVIDER")?.ToLowerInvariant();
        var options = new DbContextOptionsBuilder<FilesDbContext>();

        if (provider == "sqlserver")
        {
            const string connectionString = "Server=localhost;Database=dotnetcloud_files_dev;Trusted_Connection=True;TrustServerCertificate=True";
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                sqlOptions.CommandTimeout(30);
            });
        }
        else
        {
            const string connectionString = "Host=localhost;Database=dotnetcloud_files_dev;Username=postgres;Password=postgres";
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                npgsqlOptions.CommandTimeout(30);
            });
        }

        return new FilesDbContext(options.Options);
    }
}

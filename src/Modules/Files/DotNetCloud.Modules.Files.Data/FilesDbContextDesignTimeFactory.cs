using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Files.Data;

/// <summary>
/// Design-time factory for creating <see cref="FilesDbContext"/> instances.
/// Required by EF Core tooling to generate migrations.
/// </summary>
public class FilesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FilesDbContext>
{
    /// <inheritdoc />
    public FilesDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Host=localhost;Database=dotnetcloud_files_dev;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<FilesDbContext>();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        var namingStrategy = new PostgreSqlNamingStrategy();
        return new FilesDbContext(options.Options, namingStrategy);
    }
}

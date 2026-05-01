using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Video.Data;

/// <summary>
/// Design-time factory for creating <see cref="VideoDbContext"/> instances.
/// Required by EF Core tooling to generate migrations.
/// </summary>
/// <remarks>
/// To generate a migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Video/DotNetCloud.Modules.Video.Data
/// </code>
/// </remarks>
public class VideoDbContextDesignTimeFactory : IDesignTimeDbContextFactory<VideoDbContext>
{
    /// <inheritdoc />
    public VideoDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Host=localhost;Database=dotnetcloud_video_dev;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<VideoDbContext>();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        var namingStrategy = new PostgreSqlNamingStrategy();
        return new VideoDbContext(options.Options, namingStrategy);
    }
}

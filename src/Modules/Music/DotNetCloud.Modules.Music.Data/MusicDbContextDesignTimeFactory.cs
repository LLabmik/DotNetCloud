using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Music.Data;

/// <summary>
/// Design-time factory for creating <see cref="MusicDbContext"/> instances.
/// Required by EF Core tooling to generate migrations.
/// </summary>
/// <remarks>
/// To generate a migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Music/DotNetCloud.Modules.Music.Data
/// </code>
/// </remarks>
public class MusicDbContextDesignTimeFactory : IDesignTimeDbContextFactory<MusicDbContext>
{
    /// <inheritdoc />
    public MusicDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Host=localhost;Database=dotnetcloud_music_dev;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<MusicDbContext>();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        var namingStrategy = new PostgreSqlNamingStrategy();
        return new MusicDbContext(options.Options, namingStrategy);
    }
}

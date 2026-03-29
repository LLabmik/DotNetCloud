using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Tracks.Data;

/// <summary>
/// Design-time factory for <see cref="TracksDbContext"/> used by EF Core CLI tools.
/// </summary>
public class TracksDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TracksDbContext>
{
    /// <inheritdoc />
    public TracksDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Host=localhost;Database=dotnetcloud_tracks_dev;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<TracksDbContext>();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        return new TracksDbContext(options.Options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Photos.Data;

/// <summary>
/// Design-time factory for creating <see cref="PhotosDbContext"/> instances.
/// Required by EF Core tooling to generate migrations.
/// </summary>
/// <remarks>
/// To generate a migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Photos/DotNetCloud.Modules.Photos.Data
/// </code>
/// </remarks>
public class PhotosDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PhotosDbContext>
{
    /// <inheritdoc />
    public PhotosDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Host=localhost;Database=dotnetcloud_photos_dev;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<PhotosDbContext>();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        return new PhotosDbContext(options.Options);
    }
}

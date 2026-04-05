using DotNetCloud.Modules.Photos.Data.Configuration;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Photos.Data;

/// <summary>
/// Database context for the Photos module.
/// Manages all photo entities: photos, albums, metadata, tags, shares, and edit records.
/// </summary>
/// <remarks>
/// <para>
/// <b>Module DbContext Pattern:</b>
/// Each module owns its own DbContext, separate from the core <c>CoreDbContext</c>.
/// This provides schema isolation, independent migrations, and testability.
/// </para>
/// <para>
/// <b>Multi-Database Support:</b>
/// Works with PostgreSQL, SQL Server, and MariaDB through provider-specific configuration.
/// </para>
/// </remarks>
public class PhotosDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhotosDbContext"/> class.
    /// </summary>
    public PhotosDbContext(DbContextOptions<PhotosDbContext> options)
        : base(options)
    {
    }

    /// <summary>Photos in the gallery.</summary>
    public DbSet<Photo> Photos => Set<Photo>();

    /// <summary>Photo albums.</summary>
    public DbSet<Album> Albums => Set<Album>();

    /// <summary>Album-photo junction records.</summary>
    public DbSet<AlbumPhoto> AlbumPhotos => Set<AlbumPhoto>();

    /// <summary>Photo EXIF/GPS metadata.</summary>
    public DbSet<PhotoMetadata> PhotoMetadata => Set<PhotoMetadata>();

    /// <summary>Tags applied to photos.</summary>
    public DbSet<PhotoTag> PhotoTags => Set<PhotoTag>();

    /// <summary>Photo and album shares.</summary>
    public DbSet<PhotoShare> PhotoShares => Set<PhotoShare>();

    /// <summary>Non-destructive edit history records.</summary>
    public DbSet<PhotoEditRecord> PhotoEditRecords => Set<PhotoEditRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new PhotoConfiguration());
        modelBuilder.ApplyConfiguration(new AlbumConfiguration());
        modelBuilder.ApplyConfiguration(new AlbumPhotoConfiguration());
        modelBuilder.ApplyConfiguration(new PhotoMetadataConfiguration());
        modelBuilder.ApplyConfiguration(new PhotoTagConfiguration());
        modelBuilder.ApplyConfiguration(new PhotoShareConfiguration());
        modelBuilder.ApplyConfiguration(new PhotoEditRecordConfiguration());
    }
}

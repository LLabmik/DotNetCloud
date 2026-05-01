using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Video.Data.Configuration;
using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Video.Data;

/// <summary>
/// Database context for the Video module.
/// Manages all video entities: videos, collections, subtitles, watch progress, and sharing.
/// </summary>
public class VideoDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoDbContext"/> class.
    /// </summary>
    public VideoDbContext(DbContextOptions<VideoDbContext> options)
        : this(options, new PostgreSqlNamingStrategy())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoDbContext"/> class with a specific naming strategy.
    /// </summary>
    public VideoDbContext(DbContextOptions<VideoDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>Videos in the library.</summary>
    public DbSet<Models.Video> Videos => Set<Models.Video>();

    /// <summary>Video metadata (resolution, codecs, etc.).</summary>
    public DbSet<VideoMetadata> VideoMetadata => Set<VideoMetadata>();

    /// <summary>Video collections (series, playlists).</summary>
    public DbSet<VideoCollection> VideoCollections => Set<VideoCollection>();

    /// <summary>Collection-video junction records.</summary>
    public DbSet<VideoCollectionItem> VideoCollectionItems => Set<VideoCollectionItem>();

    /// <summary>Subtitle tracks.</summary>
    public DbSet<Subtitle> Subtitles => Set<Subtitle>();

    /// <summary>Watch history records.</summary>
    public DbSet<WatchHistory> WatchHistories => Set<WatchHistory>();

    /// <summary>Watch progress (resume position) records.</summary>
    public DbSet<WatchProgress> WatchProgresses => Set<WatchProgress>();

    /// <summary>Video share records.</summary>
    public DbSet<VideoShare> VideoShares => Set<VideoShare>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("video"));
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new VideoConfiguration());
        modelBuilder.ApplyConfiguration(new VideoMetadataConfiguration());
        modelBuilder.ApplyConfiguration(new VideoCollectionConfiguration());
        modelBuilder.ApplyConfiguration(new VideoCollectionItemConfiguration());
        modelBuilder.ApplyConfiguration(new SubtitleConfiguration());
        modelBuilder.ApplyConfiguration(new WatchHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new WatchProgressConfiguration());
        modelBuilder.ApplyConfiguration(new VideoShareConfiguration());
    }
}

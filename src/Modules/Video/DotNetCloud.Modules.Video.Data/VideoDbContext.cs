using DotNetCloud.Core.Data.Configuration.Extensions;
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

    // ── Canonical (shared) tables ──

    /// <summary>Canonical videos — shared video metadata, keyed by ContentHash.</summary>
    public DbSet<CanonicalVideo> CanonicalVideos => Set<CanonicalVideo>();

    /// <summary>Canonical video metadata (resolution, codecs, etc.).</summary>
    public DbSet<CanonicalVideoMetadata> CanonicalVideoMetadata => Set<CanonicalVideoMetadata>();

    /// <summary>Canonical TMDB enrichment data.</summary>
    public DbSet<CanonicalTmdbData> CanonicalTmdbData => Set<CanonicalTmdbData>();

    /// <summary>Canonical video series (TV series and movie franchises).</summary>
    public DbSet<CanonicalVideoSeries> CanonicalVideoSeries => Set<CanonicalVideoSeries>();

    /// <summary>Canonical seasons within TV series.</summary>
    public DbSet<CanonicalVideoSeason> CanonicalVideoSeasons => Set<CanonicalVideoSeason>();

    /// <summary>Canonical episode junction records.</summary>
    public DbSet<CanonicalVideoEpisode> CanonicalVideoEpisodes => Set<CanonicalVideoEpisode>();

    /// <summary>Canonical subtitles (intrinsic to the video file).</summary>
    public DbSet<CanonicalSubtitle> CanonicalSubtitles => Set<CanonicalSubtitle>();

    /// <summary>Canonical video series items (for movie franchises).</summary>
    public DbSet<CanonicalVideoSeriesItem> CanonicalVideoSeriesItems => Set<CanonicalVideoSeriesItem>();

    // ── Per-user junction tables ──

    /// <summary>User-video junctions — lightweight per-user records referencing canonical videos.</summary>
    public DbSet<UserVideo> UserVideos => Set<UserVideo>();

    /// <summary>Per-user video collections (e.g., "Favorites", "Watch Later").</summary>
    public DbSet<UserVideoCollection> UserVideoCollections => Set<UserVideoCollection>();

    /// <summary>Junction linking user video collections to canonical videos via content hash.</summary>
    public DbSet<UserVideoCollectionItem> UserVideoCollectionItems => Set<UserVideoCollectionItem>();

    // Note: WatchHistory, VideoShare, and WatchProgress entities have been removed.
    // VideoId columns in any remaining tables now point to UserVideo.Id.

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("video"));
        base.OnModelCreating(modelBuilder);

        // Canonical (shared) tables
        modelBuilder.ApplyConfiguration(new CanonicalVideoConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalVideoMetadataConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalTmdbDataConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalVideoSeriesConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalVideoSeasonConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalVideoEpisodeConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalSubtitleConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalVideoSeriesItemConfiguration());

        // Per-user junction tables
        modelBuilder.ApplyConfiguration(new UserVideoConfiguration());
        modelBuilder.ApplyConfiguration(new UserVideoCollectionConfiguration());
        modelBuilder.ApplyConfiguration(new UserVideoCollectionItemConfiguration());

        SequentialGuidConfigurationExtensions.ApplySequentialGuidDefaults(modelBuilder, _namingStrategy.Provider);
    }
}

using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Music.Data.Configuration;
using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Music.Data;

/// <summary>
/// Database context for the Music module.
/// Manages all music entities: artists, albums, tracks, playlists, playback history, and preferences.
/// </summary>
public class MusicDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicDbContext"/> class with a specific naming strategy.
    /// </summary>
    public MusicDbContext(DbContextOptions<MusicDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>User playlists.</summary>
    public DbSet<Playlist> Playlists => Set<Playlist>();

    /// <summary>Playlist-track junction records.</summary>
    public DbSet<PlaylistTrack> PlaylistTracks => Set<PlaylistTrack>();

    /// <summary>Track playback history.</summary>
    public DbSet<PlaybackHistory> PlaybackHistories => Set<PlaybackHistory>();

    /// <summary>Equalizer presets.</summary>
    public DbSet<EqPreset> EqPresets => Set<EqPreset>();

    /// <summary>User music preferences.</summary>
    public DbSet<UserMusicPreference> UserMusicPreferences => Set<UserMusicPreference>();

    /// <summary>Scrobble records.</summary>
    public DbSet<ScrobbleRecord> ScrobbleRecords => Set<ScrobbleRecord>();

    /// <summary>Starred (favorited) items.</summary>
    public DbSet<StarredItem> StarredItems => Set<StarredItem>();

    // ── Canonical (shared) tables ──

    /// <summary>Canonical tracks — shared audio metadata, keyed by ContentHash.</summary>
    public DbSet<CanonicalTrack> CanonicalTracks => Set<CanonicalTrack>();

    /// <summary>Canonical albums — shared album metadata.</summary>
    public DbSet<CanonicalAlbum> CanonicalAlbums => Set<CanonicalAlbum>();

    /// <summary>Canonical artists — shared artist metadata.</summary>
    public DbSet<CanonicalArtist> CanonicalArtists => Set<CanonicalArtist>();

    /// <summary>Canonical genres — shared genre names.</summary>
    public DbSet<CanonicalGenre> CanonicalGenres => Set<CanonicalGenre>();

    /// <summary>Canonical track-artist junction.</summary>
    public DbSet<CanonicalTrackArtist> CanonicalTrackArtists => Set<CanonicalTrackArtist>();

    /// <summary>Canonical track-genre junction.</summary>
    public DbSet<CanonicalTrackGenre> CanonicalTrackGenres => Set<CanonicalTrackGenre>();

    /// <summary>Canonical album-artist junction.</summary>
    public DbSet<CanonicalAlbumArtist> CanonicalAlbumArtists => Set<CanonicalAlbumArtist>();

    // ── Per-user junction tables ──

    /// <summary>User-track junctions — lightweight per-user records referencing canonical tracks.</summary>
    public DbSet<UserTrack> UserTracks => Set<UserTrack>();

    /// <summary>User-artist junctions — lightweight per-user records referencing canonical artists.</summary>
    public DbSet<UserArtist> UserArtists => Set<UserArtist>();

    /// <summary>User-album junctions — lightweight per-user records referencing canonical albums.</summary>
    public DbSet<UserAlbum> UserAlbums => Set<UserAlbum>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("music"));
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new PlaylistConfiguration());
        modelBuilder.ApplyConfiguration(new PlaylistTrackConfiguration());
        modelBuilder.ApplyConfiguration(new PlaybackHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new EqPresetConfiguration());
        modelBuilder.ApplyConfiguration(new UserMusicPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new ScrobbleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new StarredItemConfiguration());

        // Canonical (shared) tables
        modelBuilder.ApplyConfiguration(new CanonicalTrackConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalAlbumConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalArtistConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalGenreConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalTrackArtistConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalTrackGenreConfiguration());
        modelBuilder.ApplyConfiguration(new CanonicalAlbumArtistConfiguration());

        // Per-user junction tables
        modelBuilder.ApplyConfiguration(new UserTrackConfiguration());
        modelBuilder.ApplyConfiguration(new UserArtistConfiguration());
        modelBuilder.ApplyConfiguration(new UserAlbumConfiguration());
    }
}

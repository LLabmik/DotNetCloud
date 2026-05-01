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
    /// Initializes a new instance of the <see cref="MusicDbContext"/> class.
    /// </summary>
    public MusicDbContext(DbContextOptions<MusicDbContext> options)
        : this(options, new PostgreSqlNamingStrategy())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicDbContext"/> class with a specific naming strategy.
    /// </summary>
    public MusicDbContext(DbContextOptions<MusicDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>Artists in the music library.</summary>
    public DbSet<Artist> Artists => Set<Artist>();

    /// <summary>Music albums.</summary>
    public DbSet<MusicAlbum> Albums => Set<MusicAlbum>();

    /// <summary>Music tracks.</summary>
    public DbSet<Track> Tracks => Set<Track>();

    /// <summary>Track-artist junction records.</summary>
    public DbSet<TrackArtist> TrackArtists => Set<TrackArtist>();

    /// <summary>Music genres.</summary>
    public DbSet<Genre> Genres => Set<Genre>();

    /// <summary>Track-genre junction records.</summary>
    public DbSet<TrackGenre> TrackGenres => Set<TrackGenre>();

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

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("music"));
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ArtistConfiguration());
        modelBuilder.ApplyConfiguration(new MusicAlbumConfiguration());
        modelBuilder.ApplyConfiguration(new TrackConfiguration());
        modelBuilder.ApplyConfiguration(new TrackArtistConfiguration());
        modelBuilder.ApplyConfiguration(new GenreConfiguration());
        modelBuilder.ApplyConfiguration(new TrackGenreConfiguration());
        modelBuilder.ApplyConfiguration(new PlaylistConfiguration());
        modelBuilder.ApplyConfiguration(new PlaylistTrackConfiguration());
        modelBuilder.ApplyConfiguration(new PlaybackHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new EqPresetConfiguration());
        modelBuilder.ApplyConfiguration(new UserMusicPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new ScrobbleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new StarredItemConfiguration());
    }
}

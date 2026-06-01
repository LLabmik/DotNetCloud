using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Shared helpers for Music module service tests — uses canonical+junction tables only.
/// </summary>
internal static class TestHelpers
{
    public static MusicDbContext CreateDb()
    {
        var (db, _) = CreateDbWithFactory();
        return db;
    }

    /// <summary>
    /// Creates a DbContext and an <see cref="IDbContextFactory{MusicDbContext}"/> that
    /// produces fresh contexts sharing the same in-memory database (by name).
    /// </summary>
    public static (MusicDbContext db, IDbContextFactory<MusicDbContext> factory) CreateDbWithFactory()
    {
        var dbName = Guid.NewGuid().ToString();
        var db = CreateDbContext(dbName);
        var factory = new TestDbContextFactory<MusicDbContext>(() => CreateDbContext(dbName));
        return (db, factory);
    }

    private static MusicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new MusicDbContext(options, new PostgreSqlNamingStrategy());
    }

    public static CallerContext CreateCaller(Guid? userId = null)
        => new(userId ?? Guid.NewGuid(), ["user"], CallerType.User);

    public static async Task<CanonicalArtist> SeedCanonicalArtistAsync(
        MusicDbContext db,
        string name = "Test Artist",
        string? sortName = null)
    {
        var artist = new CanonicalArtist
        {
            Name = name,
            SortName = sortName ?? name
        };
        db.CanonicalArtists.Add(artist);
        await db.SaveChangesAsync();
        return artist;
    }

    public static async Task<UserArtist> SeedUserArtistAsync(
        MusicDbContext db,
        Guid canonicalArtistId,
        Guid ownerId)
    {
        var ua = new UserArtist
        {
            OwnerId = ownerId,
            CanonicalArtistId = canonicalArtistId
        };
        db.UserArtists.Add(ua);
        await db.SaveChangesAsync();
        return ua;
    }

    public static async Task<CanonicalAlbum> SeedCanonicalAlbumAsync(
        MusicDbContext db,
        string title = "Test Album",
        int? year = 2024)
    {
        var album = new CanonicalAlbum
        {
            Title = title,
            Year = year,
            TotalDurationTicks = TimeSpan.FromMinutes(45).Ticks
        };
        db.CanonicalAlbums.Add(album);
        await db.SaveChangesAsync();
        return album;
    }

    public static async Task<UserAlbum> SeedUserAlbumAsync(
        MusicDbContext db,
        Guid canonicalAlbumId,
        Guid ownerId)
    {
        var ua = new UserAlbum
        {
            OwnerId = ownerId,
            CanonicalAlbumId = canonicalAlbumId
        };
        db.UserAlbums.Add(ua);
        await db.SaveChangesAsync();
        return ua;
    }

    public static async Task<CanonicalTrack> SeedCanonicalTrackAsync(
        MusicDbContext db,
        string contentHash,
        string title = "Test Track",
        int trackNumber = 1,
        int discNumber = 1,
        string mimeType = "audio/flac")
    {
        var track = new CanonicalTrack
        {
            ContentHash = contentHash,
            Title = title,
            TrackNumber = trackNumber,
            DiscNumber = discNumber,
            DurationTicks = TimeSpan.FromMinutes(4).Ticks,
            Bitrate = 1_411_000,
            SampleRate = 44100,
            Channels = 2,
            MimeType = mimeType
        };
        db.CanonicalTracks.Add(track);
        await db.SaveChangesAsync();
        return track;
    }

    public static async Task<UserTrack> SeedUserTrackAsync(
        MusicDbContext db,
        Guid ownerId,
        Guid fileNodeId,
        string canonicalTrackHash,
        Guid? canonicalAlbumId = null)
    {
        var ut = new UserTrack
        {
            OwnerId = ownerId,
            FileNodeId = fileNodeId,
            CanonicalTrackHash = canonicalTrackHash,
            ContentHash = canonicalTrackHash,
            CanonicalAlbumId = canonicalAlbumId
        };
        db.UserTracks.Add(ut);
        await db.SaveChangesAsync();
        return ut;
    }

    public static async Task<CanonicalTrackArtist> SeedCanonicalTrackArtistAsync(
        MusicDbContext db,
        string trackContentHash,
        Guid artistId,
        bool isPrimary = true)
    {
        var cta = new CanonicalTrackArtist
        {
            TrackContentHash = trackContentHash,
            ArtistId = artistId,
            IsPrimary = isPrimary
        };
        db.CanonicalTrackArtists.Add(cta);
        await db.SaveChangesAsync();
        return cta;
    }

    public static async Task<CanonicalGenre> SeedCanonicalGenreAsync(
        MusicDbContext db,
        string name = "Rock")
    {
        var genre = new CanonicalGenre { Name = name };
        db.CanonicalGenres.Add(genre);
        await db.SaveChangesAsync();
        return genre;
    }

    public static async Task<CanonicalTrackGenre> SeedCanonicalTrackGenreAsync(
        MusicDbContext db,
        string trackContentHash,
        Guid genreId)
    {
        var ctg = new CanonicalTrackGenre
        {
            TrackContentHash = trackContentHash,
            GenreId = genreId
        };
        db.CanonicalTrackGenres.Add(ctg);
        await db.SaveChangesAsync();
        return ctg;
    }

    public static async Task<Playlist> SeedPlaylistAsync(
        MusicDbContext db,
        Guid ownerId,
        string name = "My Playlist",
        bool isPublic = false)
    {
        var playlist = new Playlist
        {
            OwnerId = ownerId,
            Name = name,
            IsPublic = isPublic
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync();
        return playlist;
    }

    public static async Task<PlaylistTrack> SeedPlaylistTrackAsync(
        MusicDbContext db,
        Guid playlistId,
        Guid userTrackId,
        int sortOrder = 0)
    {
        var pt = new PlaylistTrack
        {
            PlaylistId = playlistId,
            UserTrackId = userTrackId,
            SortOrder = sortOrder
        };
        db.PlaylistTracks.Add(pt);
        await db.SaveChangesAsync();
        return pt;
    }

    public static async Task<PlaybackHistory> SeedPlaybackHistoryAsync(
        MusicDbContext db,
        Guid userId,
        Guid userTrackId,
        int durationPlayedSeconds = 30)
    {
        var ph = new PlaybackHistory
        {
            UserId = userId,
            UserTrackId = userTrackId,
            DurationPlayedSeconds = durationPlayedSeconds
        };
        db.PlaybackHistories.Add(ph);
        await db.SaveChangesAsync();
        return ph;
    }

    public static async Task<EqPreset> SeedEqPresetAsync(MusicDbContext db, Guid? ownerId = null, string name = "Flat", bool isBuiltIn = false)
    {
        var preset = new EqPreset { OwnerId = ownerId, Name = name, IsBuiltIn = isBuiltIn, BandsJson = "{\"60Hz\":0,\"230Hz\":0,\"910Hz\":0,\"3k6Hz\":0,\"14kHz\":0}" };
        db.EqPresets.Add(preset);
        await db.SaveChangesAsync();
        return preset;
    }

    public static async Task SeedStarredItemAsync(MusicDbContext db, Guid userId, Guid itemId, StarredItemType itemType = StarredItemType.Track)
    {
        db.StarredItems.Add(new StarredItem { UserId = userId, ItemId = itemId, ItemType = itemType });
        await db.SaveChangesAsync();
    }

    public static async Task<(CanonicalArtist artist, CanonicalAlbum album, UserTrack track)> SeedCompleteTrackAsync(
        MusicDbContext db, string artistName = "Test Artist", string albumTitle = "Test Album",
        string trackTitle = "Test Track", string genreName = "Rock", Guid? ownerId = null)
    {
        var owner = ownerId ?? Guid.NewGuid();
        var artist = await SeedCanonicalArtistAsync(db, artistName);
        await SeedUserArtistAsync(db, artist.Id, owner);
        var album = await SeedCanonicalAlbumAsync(db, albumTitle);
        await SeedUserAlbumAsync(db, album.Id, owner);
        var contentHash = Guid.NewGuid().ToString("N");
        await SeedCanonicalTrackAsync(db, contentHash, trackTitle);
        await SeedCanonicalTrackArtistAsync(db, contentHash, artist.Id);
        db.CanonicalAlbumArtists.Add(new CanonicalAlbumArtist
        {
            AlbumId = album.Id,
            ArtistId = artist.Id,
            IsPrimary = true
        });
        await db.SaveChangesAsync();
        var genre = await SeedCanonicalGenreAsync(db, genreName);
        await SeedCanonicalTrackGenreAsync(db, contentHash, genre.Id);
        var userTrack = await SeedUserTrackAsync(db, owner, Guid.NewGuid(), contentHash, album.Id);
        return (artist, album, userTrack);
    }

    // ── Legacy-compatible wrappers (create canonical+junction data) ──

    public static async Task<CanonicalArtist> SeedArtistAsync(MusicDbContext db, string name = "Test Artist", string? sortName = null, Guid? ownerId = null)
    {
        var a = await SeedCanonicalArtistAsync(db, name, sortName);
        if (ownerId.HasValue)
            await SeedUserArtistAsync(db, a.Id, ownerId.Value);
        return a;
    }

    public static async Task<CanonicalAlbum> SeedAlbumAsync(MusicDbContext db, Guid artistId, string title = "Test Album", int? year = 2024, Guid? ownerId = null)
    {
        var a = await SeedCanonicalAlbumAsync(db, title, year);
        db.CanonicalAlbumArtists.Add(new CanonicalAlbumArtist
        {
            AlbumId = a.Id,
            ArtistId = artistId,
            IsPrimary = true
        });
        await db.SaveChangesAsync();
        if (ownerId.HasValue)
            await SeedUserAlbumAsync(db, a.Id, ownerId.Value);
        return a;
    }

    public static async Task<UserTrack> SeedTrackAsync(MusicDbContext db, Guid? albumId = null, string title = "Test Track",
        int trackNumber = 1, int discNumber = 1, string mimeType = "audio/flac", long sizeBytes = 30_000_000, Guid? ownerId = null)
    {
        _ = sizeBytes; // Size is on FileNode, not tracked in canonical
        var owner = ownerId ?? Guid.NewGuid();
        var contentHash = Guid.NewGuid().ToString("N");
        await SeedCanonicalTrackAsync(db, contentHash, title, trackNumber, discNumber, mimeType);
        return await SeedUserTrackAsync(db, owner, Guid.NewGuid(), contentHash, albumId);
    }

    public static async Task<CanonicalTrackArtist> SeedTrackArtistAsync(MusicDbContext db, Guid trackId, Guid artistId, bool isPrimary = true)
    {
        var userTrack = await db.UserTracks.FirstOrDefaultAsync(ut => ut.Id == trackId);
        var contentHash = userTrack?.CanonicalTrackHash ?? Guid.NewGuid().ToString("N");
        return await SeedCanonicalTrackArtistAsync(db, contentHash, artistId, isPrimary);
    }

    public static async Task<CanonicalGenre> SeedGenreAsync(MusicDbContext db, string name = "Rock")
        => await SeedCanonicalGenreAsync(db, name);

    public static async Task<CanonicalTrackGenre> SeedTrackGenreAsync(MusicDbContext db, Guid trackId, Guid genreId)
    {
        var userTrack = await db.UserTracks.FirstOrDefaultAsync(ut => ut.Id == trackId);
        var contentHash = userTrack?.CanonicalTrackHash ?? Guid.NewGuid().ToString("N");
        return await SeedCanonicalTrackGenreAsync(db, contentHash, genreId);
    }
}

/// <summary>
/// Simple <see cref="IDbContextFactory{TContext}"/> wrapper for unit tests
/// that creates a new DbContext instance on each call using the supplied factory function.
/// </summary>
internal sealed class TestDbContextFactory<TContext> : IDbContextFactory<TContext> where TContext : DbContext
{
    private readonly Func<TContext> _factory;

    public TestDbContextFactory(Func<TContext> factory) => _factory = factory;

    public TContext CreateDbContext() => _factory();
}

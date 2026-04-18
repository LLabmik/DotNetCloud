using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Shared helpers for Music module service tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>Creates a fresh InMemory MusicDbContext.</summary>
    public static MusicDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MusicDbContext(options);
    }

    /// <summary>Creates a CallerContext for a user.</summary>
    public static CallerContext CreateCaller(Guid? userId = null)
        => new(userId ?? Guid.NewGuid(), ["user"], CallerType.User);

    /// <summary>Seeds an artist in the database.</summary>
    public static async Task<Artist> SeedArtistAsync(
        MusicDbContext db,
        string name = "Test Artist",
        string? sortName = null,
        Guid? ownerId = null)
    {
        var artist = new Artist
        {
            Name = name,
            SortName = sortName ?? name,
            OwnerId = ownerId ?? Guid.NewGuid()
        };
        db.Artists.Add(artist);
        await db.SaveChangesAsync();
        return artist;
    }

    /// <summary>Seeds a music album in the database.</summary>
    public static async Task<MusicAlbum> SeedAlbumAsync(
        MusicDbContext db,
        Guid artistId,
        string title = "Test Album",
        int? year = 2024,
        Guid? ownerId = null)
    {
        var album = new MusicAlbum
        {
            Title = title,
            ArtistId = artistId,
            Year = year,
            TotalDurationTicks = TimeSpan.FromMinutes(45).Ticks,
            OwnerId = ownerId ?? Guid.NewGuid()
        };
        db.Albums.Add(album);
        await db.SaveChangesAsync();
        return album;
    }

    /// <summary>Seeds a track in the database.</summary>
    public static async Task<Track> SeedTrackAsync(
        MusicDbContext db,
        Guid? albumId = null,
        string title = "Test Track",
        int trackNumber = 1,
        int discNumber = 1,
        string mimeType = "audio/flac",
        long sizeBytes = 30_000_000,
        Guid? ownerId = null)
    {
        var track = new Track
        {
            FileNodeId = Guid.NewGuid(),
            OwnerId = ownerId ?? Guid.NewGuid(),
            Title = title,
            FileName = $"{title.Replace(' ', '_').ToLowerInvariant()}.flac",
            TrackNumber = trackNumber,
            DiscNumber = discNumber,
            DurationTicks = TimeSpan.FromMinutes(4).Ticks,
            SizeBytes = sizeBytes,
            Bitrate = 1_411_000,
            SampleRate = 44100,
            Channels = 2,
            MimeType = mimeType,
            AlbumId = albumId
        };
        db.Tracks.Add(track);
        await db.SaveChangesAsync();
        return track;
    }

    /// <summary>Seeds a track-artist link.</summary>
    public static async Task<TrackArtist> SeedTrackArtistAsync(
        MusicDbContext db,
        Guid trackId,
        Guid artistId,
        bool isPrimary = true)
    {
        var ta = new TrackArtist
        {
            TrackId = trackId,
            ArtistId = artistId,
            IsPrimary = isPrimary
        };
        db.Set<TrackArtist>().Add(ta);
        await db.SaveChangesAsync();
        return ta;
    }

    /// <summary>Seeds a genre.</summary>
    public static async Task<Genre> SeedGenreAsync(
        MusicDbContext db,
        string name = "Rock")
    {
        var genre = new Genre { Name = name };
        db.Genres.Add(genre);
        await db.SaveChangesAsync();
        return genre;
    }

    /// <summary>Seeds a track-genre link.</summary>
    public static async Task<TrackGenre> SeedTrackGenreAsync(
        MusicDbContext db,
        Guid trackId,
        Guid genreId)
    {
        var tg = new TrackGenre
        {
            TrackId = trackId,
            GenreId = genreId
        };
        db.Set<TrackGenre>().Add(tg);
        await db.SaveChangesAsync();
        return tg;
    }

    /// <summary>Seeds a playlist.</summary>
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

    /// <summary>Seeds a playlist track entry.</summary>
    public static async Task<PlaylistTrack> SeedPlaylistTrackAsync(
        MusicDbContext db,
        Guid playlistId,
        Guid trackId,
        int sortOrder = 0)
    {
        var pt = new PlaylistTrack
        {
            PlaylistId = playlistId,
            TrackId = trackId,
            SortOrder = sortOrder
        };
        db.Set<PlaylistTrack>().Add(pt);
        await db.SaveChangesAsync();
        return pt;
    }

    /// <summary>Seeds an EQ preset.</summary>
    public static async Task<EqPreset> SeedEqPresetAsync(
        MusicDbContext db,
        Guid? ownerId = null,
        string name = "Custom Preset",
        bool isBuiltIn = false)
    {
        var preset = new EqPreset
        {
            OwnerId = ownerId,
            Name = name,
            IsBuiltIn = isBuiltIn,
            BandsJson = "{\"60Hz\":0,\"230Hz\":0,\"910Hz\":0,\"3600Hz\":0,\"14000Hz\":0}"
        };
        db.EqPresets.Add(preset);
        await db.SaveChangesAsync();
        return preset;
    }

    /// <summary>Seeds a starred item.</summary>
    public static async Task<StarredItem> SeedStarredItemAsync(
        MusicDbContext db,
        Guid userId,
        Guid itemId,
        StarredItemType itemType = StarredItemType.Track)
    {
        var star = new StarredItem
        {
            UserId = userId,
            ItemId = itemId,
            ItemType = itemType,
            StarredAt = DateTime.UtcNow
        };
        db.StarredItems.Add(star);
        await db.SaveChangesAsync();
        return star;
    }

    /// <summary>Seeds a playback history entry.</summary>
    public static async Task<PlaybackHistory> SeedPlaybackHistoryAsync(
        MusicDbContext db,
        Guid userId,
        Guid trackId)
    {
        var history = new PlaybackHistory
        {
            UserId = userId,
            TrackId = trackId,
            PlayedAt = DateTime.UtcNow
        };
        db.PlaybackHistories.Add(history);
        await db.SaveChangesAsync();
        return history;
    }

    /// <summary>Seeds a complete track with artist and album.</summary>
    public static async Task<(Artist Artist, MusicAlbum Album, Track Track)> SeedCompleteTrackAsync(
        MusicDbContext db,
        string artistName = "Test Artist",
        string albumTitle = "Test Album",
        string trackTitle = "Test Track",
        Guid? ownerId = null)
    {
        var artist = await SeedArtistAsync(db, artistName, ownerId: ownerId);
        var album = await SeedAlbumAsync(db, artist.Id, albumTitle, ownerId: ownerId);
        var track = await SeedTrackAsync(db, album.Id, trackTitle, ownerId: ownerId);
        await SeedTrackArtistAsync(db, track.Id, artist.Id);
        return (artist, album, track);
    }

    /// <summary>Seeds an album with HasCoverArt = false, no CoverArtPath.</summary>
    public static async Task<MusicAlbum> SeedAlbumWithoutArtAsync(
        MusicDbContext db,
        Guid artistId,
        string title = "No Art Album",
        Guid? ownerId = null)
    {
        var album = new MusicAlbum
        {
            Title = title,
            ArtistId = artistId,
            HasCoverArt = false,
            CoverArtPath = null,
            TotalDurationTicks = TimeSpan.FromMinutes(45).Ticks,
            OwnerId = ownerId ?? Guid.NewGuid()
        };
        db.Albums.Add(album);
        await db.SaveChangesAsync();
        return album;
    }

    /// <summary>Seeds an artist with MusicBrainz enrichment data populated.</summary>
    public static async Task<Artist> SeedEnrichedArtistAsync(
        MusicDbContext db,
        string name = "Enriched Artist",
        Guid? ownerId = null)
    {
        var artist = new Artist
        {
            Name = name,
            SortName = name,
            OwnerId = ownerId ?? Guid.NewGuid(),
            MusicBrainzId = Guid.NewGuid().ToString(),
            Biography = $"{name} is a well-known musical act.",
            WikipediaUrl = $"https://en.wikipedia.org/wiki/{name.Replace(' ', '_')}",
            DiscogsUrl = "https://www.discogs.com/artist/12345",
            OfficialUrl = $"https://www.{name.Replace(' ', '-').ToLowerInvariant()}.com",
            LastEnrichedAt = DateTime.UtcNow.AddDays(-10)
        };
        db.Artists.Add(artist);
        await db.SaveChangesAsync();
        return artist;
    }

    /// <summary>Creates a JSON string mimicking a MusicBrainz artist search response.</summary>
    public static string CreateMockMusicBrainzArtistJson(string name, string mbid, int score)
    {
        return $$"""
        {
            "artists": [
                {"id":"{{mbid}}","name":"{{name}}","score":{{score}},"disambiguation":""}
            ]
        }
        """;
    }
}

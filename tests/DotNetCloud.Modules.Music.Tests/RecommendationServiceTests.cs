using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class RecommendationServiceTests
{
    private MusicDbContext _db;
    private RecommendationService _service;
    private TrackService _trackService;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _trackService = new TrackService(_db, new Mock<IEventBus>().Object, Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(), NullLogger<TrackService>.Instance);
        _service = new RecommendationService(_db, _trackService, NullLogger<RecommendationService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── GetRecentlyPlayed ────────────────────────────────────────────

    [TestMethod]
    public async Task GetRecentlyPlayed_ReturnsTracksFromHistory()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedPlaybackHistoryAsync(_db, _caller.UserId, track.Id);

        var result = await _service.GetRecentlyPlayedAsync(_caller, 10);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task GetRecentlyPlayed_NoHistory_ReturnsEmpty()
    {
        var result = await _service.GetRecentlyPlayedAsync(_caller, 10);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetRecentlyPlayed_OrdersMostRecentFirst()
    {
        var (_, album, older) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "Older Track", ownerId: _caller.UserId);
        var newer = await TestHelpers.SeedTrackAsync(_db, album.Id, "Newer Track", ownerId: _caller.UserId);

        _db.PlaybackHistories.Add(new PlaybackHistory
        {
            UserId = _caller.UserId,
            UserTrackId = older.Id,
            PlayedAt = DateTime.UtcNow.AddHours(-2)
        });
        _db.PlaybackHistories.Add(new PlaybackHistory
        {
            UserId = _caller.UserId,
            UserTrackId = newer.Id,
            PlayedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetRecentlyPlayedAsync(_caller, 10);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Newer Track", result[0].Title);
        Assert.AreEqual("Older Track", result[1].Title);
    }

    [TestMethod]
    public async Task GetRecentlyPlayed_DeduplicatesRepeatedPlays()
    {
        var (_, album, trackA) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "Track A", ownerId: _caller.UserId);
        var trackB = await TestHelpers.SeedTrackAsync(_db, album.Id, "Track B", ownerId: _caller.UserId);

        // Track A played twice (old + recent), Track B once in between.
        _db.PlaybackHistories.Add(new PlaybackHistory
        {
            UserId = _caller.UserId,
            UserTrackId = trackA.Id,
            PlayedAt = DateTime.UtcNow.AddHours(-3)
        });
        _db.PlaybackHistories.Add(new PlaybackHistory
        {
            UserId = _caller.UserId,
            UserTrackId = trackB.Id,
            PlayedAt = DateTime.UtcNow.AddHours(-2)
        });
        _db.PlaybackHistories.Add(new PlaybackHistory
        {
            UserId = _caller.UserId,
            UserTrackId = trackA.Id,
            PlayedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetRecentlyPlayedAsync(_caller, 10);

        // Track A must appear exactly once, ordered by its most recent play (at top).
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Track A", result[0].Title);
        Assert.AreEqual("Track B", result[1].Title);
    }

    // ─── GetMostPlayed ────────────────────────────────────────────────

    [TestMethod]
    public async Task GetMostPlayed_OrdersByPlayCount()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);

        var popular = await TestHelpers.SeedTrackAsync(_db, album.Id, "Popular", 1, ownerId: _caller.UserId);
        await TestHelpers.SeedTrackArtistAsync(_db, popular.Id, artist.Id);
        popular.PlayCount = 50;

        var unpopular = await TestHelpers.SeedTrackAsync(_db, album.Id, "Less Popular", 2, ownerId: _caller.UserId);
        await TestHelpers.SeedTrackArtistAsync(_db, unpopular.Id, artist.Id);
        unpopular.PlayCount = 5;

        await _db.SaveChangesAsync();

        var result = await _service.GetMostPlayedAsync(_caller, 10);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Popular", result[0].Title);
    }

    // ─── GetNewAdditions ──────────────────────────────────────────────

    [TestMethod]
    public async Task GetNewAdditions_ReturnsRecentTracks()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        var result = await _service.GetNewAdditionsAsync(_caller, 10);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task GetNewAdditions_LimitsCount()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);
        for (int i = 0; i < 10; i++)
        {
            var t = await TestHelpers.SeedTrackAsync(_db, album.Id, $"Track {i}", i + 1, ownerId: _caller.UserId);
            await TestHelpers.SeedTrackArtistAsync(_db, t.Id, artist.Id);
        }

        var result = await _service.GetNewAdditionsAsync(_caller, 5);

        Assert.AreEqual(5, result.Count);
    }

    // ─── GetGenres ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetGenres_ReturnsDistinctGenres()
    {
        var genre1 = await TestHelpers.SeedGenreAsync(_db, "Rock");
        var genre2 = await TestHelpers.SeedGenreAsync(_db, "Jazz");

        // Genres are returned via TrackGenre associations for user's tracks
        var (_, _, track1) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "T1", ownerId: _caller.UserId);
        var (_, _, track2) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "T2", artistName: "A2", albumTitle: "Al2", ownerId: _caller.UserId);
        await TestHelpers.SeedTrackGenreAsync(_db, track1.Id, genre1.Id);
        await TestHelpers.SeedTrackGenreAsync(_db, track2.Id, genre2.Id);

        var result = await _service.GetGenresAsync(_caller.UserId);

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Contains("Rock"));
        Assert.IsTrue(result.Contains("Jazz"));
    }

    [TestMethod]
    public async Task GetGenres_EmptyDb_ReturnsEmpty()
    {
        var result = await _service.GetGenresAsync(_caller.UserId);
        Assert.AreEqual(0, result.Count);
    }

    // ─── GetSimilarTracks ─────────────────────────────────────────────

    [TestMethod]
    public async Task GetSimilarTracks_ReturnsTracks()
    {
        var genre = await TestHelpers.SeedGenreAsync(_db, "Rock");
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);

        var t1 = await TestHelpers.SeedTrackAsync(_db, album.Id, "Source", 1, ownerId: _caller.UserId);
        await TestHelpers.SeedTrackArtistAsync(_db, t1.Id, artist.Id);
        await TestHelpers.SeedTrackGenreAsync(_db, t1.Id, genre.Id);

        var t2 = await TestHelpers.SeedTrackAsync(_db, album.Id, "Similar", 2, ownerId: _caller.UserId);
        await TestHelpers.SeedTrackArtistAsync(_db, t2.Id, artist.Id);
        await TestHelpers.SeedTrackGenreAsync(_db, t2.Id, genre.Id);

        var result = await _service.GetSimilarTracksAsync(t1.Id, _caller, 10);

        // Should return at least the other track with same genre/artist
        Assert.IsTrue(result.Count >= 1);
    }
}

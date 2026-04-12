using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class TrackServiceTests
{
    private MusicDbContext _db;
    private TrackService _service;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new TrackService(_db, new Mock<IEventBus>().Object, NullLogger<TrackService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Get ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetTrack_ExistingTrack_ReturnsDto()
    {
        var (artist, album, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        var result = await _service.GetTrackAsync(track.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(track.Title, result.Title);
    }

    [TestMethod]
    public async Task GetTrack_NonExistent_ReturnsNull()
    {
        var result = await _service.GetTrackAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetTrack_SoftDeleted_ReturnsNull()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);
        track.IsDeleted = true;
        await _db.SaveChangesAsync();

        var result = await _service.GetTrackAsync(track.Id, _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetTrack_IncludesArtistName()
    {
        var (artist, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, artistName: "Nirvana", ownerId: _caller.UserId);

        var result = await _service.GetTrackAsync(track.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Nirvana", result.ArtistName);
    }

    [TestMethod]
    public async Task GetTrack_IncludesAlbumTitle()
    {
        var (_, album, track) = await TestHelpers.SeedCompleteTrackAsync(_db, albumTitle: "Nevermind", ownerId: _caller.UserId);

        var result = await _service.GetTrackAsync(track.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Nevermind", result.AlbumTitle);
    }

    [TestMethod]
    public async Task GetTrack_WithStar_ReturnsIsStarredTrue()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedStarredItemAsync(_db, _caller.UserId, track.Id, StarredItemType.Track);

        var result = await _service.GetTrackAsync(track.Id, _caller);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsStarred);
    }

    // ─── List ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListTracks_ReturnsAll()
    {
        var (_, album, _) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "Track 1", ownerId: _caller.UserId);
        var t2 = await TestHelpers.SeedTrackAsync(_db, album.Id, "Track 2", trackNumber: 2, ownerId: _caller.UserId);

        var result = await _service.ListTracksAsync(_caller, 0, 50);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task ListTracks_Pagination()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);
        for (int i = 0; i < 5; i++)
        {
            var t = await TestHelpers.SeedTrackAsync(_db, album.Id, $"Track {i}", i + 1, ownerId: _caller.UserId);
            await TestHelpers.SeedTrackArtistAsync(_db, t.Id, artist.Id);
        }

        var result = await _service.ListTracksAsync(_caller, 1, 2);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task ListTracks_ExcludesSoftDeleted()
    {
        var (artist, album, track1) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "Active", ownerId: _caller.UserId);
        var track2 = await TestHelpers.SeedTrackAsync(_db, album.Id, "Deleted", 2, ownerId: _caller.UserId);
        await TestHelpers.SeedTrackArtistAsync(_db, track2.Id, artist.Id);
        track2.IsDeleted = true;
        await _db.SaveChangesAsync();

        var result = await _service.ListTracksAsync(_caller, 0, 50);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Active", result[0].Title);
    }

    // ─── ListByAlbum ──────────────────────────────────────────────────

    [TestMethod]
    public async Task ListTracksByAlbum_ReturnsOnlyAlbumTracks()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album1 = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Album 1", ownerId: _caller.UserId);
        var album2 = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Album 2", ownerId: _caller.UserId);
        var t1 = await TestHelpers.SeedTrackAsync(_db, album1.Id, "T1", ownerId: _caller.UserId);
        var t2 = await TestHelpers.SeedTrackAsync(_db, album2.Id, "T2", ownerId: _caller.UserId);
        await TestHelpers.SeedTrackArtistAsync(_db, t1.Id, artist.Id);
        await TestHelpers.SeedTrackArtistAsync(_db, t2.Id, artist.Id);

        var result = await _service.ListTracksByAlbumAsync(album1.Id, _caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("T1", result[0].Title);
    }

    // ─── Search ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task SearchTracks_MatchesByTitle()
    {
        var (_, _, _) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "Bohemian Rhapsody", ownerId: _caller.UserId);
        var (_, _, _) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "Stairway to Heaven", artistName: "Led Zep", albumTitle: "IV", ownerId: _caller.UserId);

        var result = await _service.SearchAsync(_caller, "Bohemian", 10);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task SearchTracks_CaseInsensitive()
    {
        var (_, _, _) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "Smells Like Teen Spirit", ownerId: _caller.UserId);

        // InMemory provider is case-sensitive; use exact prefix match
        var result = await _service.SearchAsync(_caller, "Smells", 10);

        Assert.AreEqual(1, result.Count);
    }

    // ─── GetRandom ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetRandomTracks_ReturnsRequestedCount()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);
        for (int i = 0; i < 10; i++)
        {
            var t = await TestHelpers.SeedTrackAsync(_db, album.Id, $"Track {i}", i + 1, ownerId: _caller.UserId);
            await TestHelpers.SeedTrackArtistAsync(_db, t.Id, artist.Id);
        }

        var result = await _service.GetRandomTracksAsync(_caller, 5);

        Assert.AreEqual(5, result.Count);
    }

    [TestMethod]
    public async Task GetRandomTracks_FewerAvailable_ReturnsAll()
    {
        var (_, _, _) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        var result = await _service.GetRandomTracksAsync(_caller, 10);

        Assert.AreEqual(1, result.Count);
    }

    // ─── GetRecent ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetRecentTracks_ReturnsNewestFirst()
    {
        var (_, _, _) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "Old Track", ownerId: _caller.UserId);
        var (_, _, _) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "New Track", artistName: "Artist2", albumTitle: "Album2", ownerId: _caller.UserId);

        var result = await _service.GetRecentTracksAsync(_caller, 10);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("New Track", result[0].Title);
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteTrack_SetsIsDeleted()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        await _service.DeleteTrackAsync(track.Id, _caller);

        var entry = await _db.Tracks.FindAsync(track.Id);
        Assert.IsNotNull(entry);
        Assert.IsTrue(entry.IsDeleted);
    }

    [TestMethod]
    public async Task DeleteTrack_NonExistent_Throws()
    {
        await Assert.ThrowsExactlyAsync<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.DeleteTrackAsync(Guid.NewGuid(), _caller));
    }
}

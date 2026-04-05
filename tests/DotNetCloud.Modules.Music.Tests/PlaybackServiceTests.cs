using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class PlaybackServiceTests
{
    private MusicDbContext _db;
    private PlaybackService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _service = new PlaybackService(_db, _eventBusMock.Object, NullLogger<PlaybackService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── RecordPlay ───────────────────────────────────────────────────

    [TestMethod]
    public async Task RecordPlay_IncrementsPlayCount()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);
        Assert.AreEqual(0, track.PlayCount);

        await _service.RecordPlayAsync(track.Id, 0, _caller);

        var entry = await _db.Tracks.FindAsync(track.Id);
        Assert.IsNotNull(entry);
        Assert.AreEqual(1, entry.PlayCount);
    }

    [TestMethod]
    public async Task RecordPlay_CreatesPlaybackHistory()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        await _service.RecordPlayAsync(track.Id, 0, _caller);

        Assert.AreEqual(1, _db.PlaybackHistories.Count());
        var history = _db.PlaybackHistories.First();
        Assert.AreEqual(_caller.UserId, history.UserId);
        Assert.AreEqual(track.Id, history.TrackId);
    }

    [TestMethod]
    public async Task RecordPlay_PublishesEvent()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        await _service.RecordPlayAsync(track.Id, 0, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<TrackPlayedEvent>(e => e.TrackId == track.Id),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RecordPlay_MultiplePlays_IncrementsCorrectly()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        await _service.RecordPlayAsync(track.Id, 0, _caller);
        await _service.RecordPlayAsync(track.Id, 0, _caller);
        await _service.RecordPlayAsync(track.Id, 0, _caller);

        var entry = await _db.Tracks.FindAsync(track.Id);
        Assert.AreEqual(3, entry!.PlayCount);
    }

    // ─── Scrobble ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task Scrobble_CreatesScrobbleRecord()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        await _service.ScrobbleAsync(track.Id, _caller);

        Assert.AreEqual(1, _db.ScrobbleRecords.Count());
    }

    [TestMethod]
    public async Task Scrobble_PublishesEvent()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        await _service.ScrobbleAsync(track.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<TrackScrobbledEvent>(e => e.TrackId == track.Id),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── GetRecentlyPlayed ────────────────────────────────────────────

    [TestMethod]
    public async Task GetRecentlyPlayed_ReturnsHistory()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedPlaybackHistoryAsync(_db, _caller.UserId, track.Id);

        var result = await _service.GetRecentlyPlayedAsync(_caller.UserId, 10);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task GetRecentlyPlayed_LimitsResults()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);
        for (int i = 0; i < 5; i++)
        {
            var t = await TestHelpers.SeedTrackAsync(_db, album.Id, $"Track {i}", i + 1, ownerId: _caller.UserId);
            await TestHelpers.SeedTrackArtistAsync(_db, t.Id, artist.Id);
            await TestHelpers.SeedPlaybackHistoryAsync(_db, _caller.UserId, t.Id);
        }

        var result = await _service.GetRecentlyPlayedAsync(_caller.UserId, 3);

        Assert.AreEqual(3, result.Count);
    }

    // ─── GetMostPlayed ────────────────────────────────────────────────

    [TestMethod]
    public async Task GetMostPlayed_OrdersByPlayCount()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);

        var t1 = await TestHelpers.SeedTrackAsync(_db, album.Id, "Popular", 1, ownerId: _caller.UserId);
        await TestHelpers.SeedTrackArtistAsync(_db, t1.Id, artist.Id);
        t1.PlayCount = 100;

        var t2 = await TestHelpers.SeedTrackAsync(_db, album.Id, "Unpopular", 2, ownerId: _caller.UserId);
        await TestHelpers.SeedTrackArtistAsync(_db, t2.Id, artist.Id);
        t2.PlayCount = 1;

        await _db.SaveChangesAsync();

        var result = await _service.GetMostPlayedAsync(_caller.UserId, 10);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Popular", result[0].Title);
    }

    // ─── ToggleStar ───────────────────────────────────────────────────

    [TestMethod]
    public async Task ToggleStar_AddsStarWhenNotExisting()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        await _service.ToggleStarAsync(track.Id, StarredItemType.Track, _caller);

        Assert.AreEqual(1, _db.StarredItems.Count());
    }

    [TestMethod]
    public async Task ToggleStar_RemovesStarWhenExisting()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedStarredItemAsync(_db, _caller.UserId, track.Id);

        await _service.ToggleStarAsync(track.Id, StarredItemType.Track, _caller);

        Assert.AreEqual(0, _db.StarredItems.Count());
    }

    [TestMethod]
    public async Task ToggleStar_Album_Works()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);

        await _service.ToggleStarAsync(album.Id, StarredItemType.Album, _caller);

        var star = _db.StarredItems.First();
        Assert.AreEqual(StarredItemType.Album, star.ItemType);
    }

    [TestMethod]
    public async Task ToggleStar_Artist_Works()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);

        await _service.ToggleStarAsync(artist.Id, StarredItemType.Artist, _caller);

        var star = _db.StarredItems.First();
        Assert.AreEqual(StarredItemType.Artist, star.ItemType);
    }

    // ─── GetStarred ───────────────────────────────────────────────────

    [TestMethod]
    public async Task GetStarred_ReturnsStarredByType()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedStarredItemAsync(_db, _caller.UserId, track.Id, StarredItemType.Track);

        var artist = await TestHelpers.SeedArtistAsync(_db, "Starred Artist", ownerId: _caller.UserId);
        await TestHelpers.SeedStarredItemAsync(_db, _caller.UserId, artist.Id, StarredItemType.Artist);

        var tracks = await _service.GetStarredAsync(_caller.UserId, StarredItemType.Track);
        var artists = await _service.GetStarredAsync(_caller.UserId, StarredItemType.Artist);

        Assert.AreEqual(1, tracks.Count);
        Assert.AreEqual(1, artists.Count);
    }

    [TestMethod]
    public async Task GetStarred_OtherUser_ReturnsEmpty()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedStarredItemAsync(_db, Guid.NewGuid(), track.Id, StarredItemType.Track);

        var result = await _service.GetStarredAsync(_caller.UserId, StarredItemType.Track);

        Assert.AreEqual(0, result.Count);
    }

    // ─── IsStarred ────────────────────────────────────────────────────

    [TestMethod]
    public async Task IsStarred_WhenStarred_ReturnsTrue()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedStarredItemAsync(_db, _caller.UserId, track.Id, StarredItemType.Track);

        var result = await _service.IsStarredAsync(_caller.UserId, track.Id, StarredItemType.Track);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsStarred_WhenNotStarred_ReturnsFalse()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        var result = await _service.IsStarredAsync(_caller.UserId, track.Id, StarredItemType.Track);

        Assert.IsFalse(result);
    }
}

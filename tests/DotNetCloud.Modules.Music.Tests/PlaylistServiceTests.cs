using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class PlaylistServiceTests
{
    private MusicDbContext _db;
    private PlaylistService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _service = new PlaylistService(_db, _eventBusMock.Object, NullLogger<PlaylistService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreatePlaylist_ReturnsDto()
    {
        var dto = new CreatePlaylistDto { Name = "My Playlist" };

        var result = await _service.CreatePlaylistAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("My Playlist", result.Name);
        Assert.AreEqual(_caller.UserId, result.OwnerId);
    }

    [TestMethod]
    public async Task CreatePlaylist_PersistedToDatabase()
    {
        var dto = new CreatePlaylistDto { Name = "Saved Playlist" };

        await _service.CreatePlaylistAsync(dto, _caller);

        Assert.AreEqual(1, _db.Playlists.Count());
    }

    [TestMethod]
    public async Task CreatePlaylist_PublishesEvent()
    {
        var dto = new CreatePlaylistDto { Name = "Event Playlist" };

        await _service.CreatePlaylistAsync(dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<PlaylistCreatedEvent>(e => e.Name == "Event Playlist"),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreatePlaylist_SetsIsPublic()
    {
        var dto = new CreatePlaylistDto { Name = "Public PL", IsPublic = true };

        var result = await _service.CreatePlaylistAsync(dto, _caller);

        Assert.IsTrue(result.IsPublic);
    }

    // ─── Get ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetPlaylist_AsOwner_Returns()
    {
        var pl = await TestHelpers.SeedPlaylistAsync(_db, _caller.UserId, "My PL");

        var result = await _service.GetPlaylistAsync(pl.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("My PL", result.Name);
    }

    [TestMethod]
    public async Task GetPlaylist_PublicPlaylist_OtherUser_Returns()
    {
        var other = Guid.NewGuid();
        var pl = await TestHelpers.SeedPlaylistAsync(_db, other, "Public PL", isPublic: true);

        var result = await _service.GetPlaylistAsync(pl.Id, _caller);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetPlaylist_PrivatePlaylist_OtherUser_ReturnsNull()
    {
        var other = Guid.NewGuid();
        var pl = await TestHelpers.SeedPlaylistAsync(_db, other, "Private PL");

        var result = await _service.GetPlaylistAsync(pl.Id, _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetPlaylist_NonExistent_ReturnsNull()
    {
        var result = await _service.GetPlaylistAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    // ─── List ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListPlaylists_ReturnsOwnPlaylists()
    {
        await TestHelpers.SeedPlaylistAsync(_db, _caller.UserId, "Mine");
        await TestHelpers.SeedPlaylistAsync(_db, Guid.NewGuid(), "Other Public", isPublic: true);
        await TestHelpers.SeedPlaylistAsync(_db, Guid.NewGuid(), "Other Private", isPublic: false);

        var result = await _service.ListPlaylistsAsync(_caller);

        // Service returns only playlists owned by the caller
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Mine", result[0].Name);
    }

    // ─── Update ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdatePlaylist_UpdatesName()
    {
        var pl = await TestHelpers.SeedPlaylistAsync(_db, _caller.UserId, "Old Name");

        var result = await _service.UpdatePlaylistAsync(pl.Id, new UpdatePlaylistDto { Name = "New Name" }, _caller);

        Assert.AreEqual("New Name", result.Name);
    }

    [TestMethod]
    public async Task UpdatePlaylist_NonOwner_Throws()
    {
        var pl = await TestHelpers.SeedPlaylistAsync(_db, Guid.NewGuid(), "Other's PL");

        await Assert.ThrowsExactlyAsync<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.UpdatePlaylistAsync(pl.Id, new UpdatePlaylistDto { Name = "Hack" }, _caller));
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeletePlaylist_SetsIsDeleted()
    {
        var pl = await TestHelpers.SeedPlaylistAsync(_db, _caller.UserId);

        await _service.DeletePlaylistAsync(pl.Id, _caller);

        var entry = await _db.Playlists.FindAsync(pl.Id);
        Assert.IsNotNull(entry);
        Assert.IsTrue(entry.IsDeleted);
    }

    [TestMethod]
    public async Task DeletePlaylist_NonExistent_Throws()
    {
        await Assert.ThrowsExactlyAsync<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.DeletePlaylistAsync(Guid.NewGuid(), _caller));
    }

    // ─── AddTrack / RemoveTrack ───────────────────────────────────────

    [TestMethod]
    public async Task AddTrack_AddsToPlaylist()
    {
        var pl = await TestHelpers.SeedPlaylistAsync(_db, _caller.UserId);
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        await _service.AddTrackAsync(pl.Id, track.Id, _caller);

        var tracks = await _service.GetPlaylistTracksAsync(pl.Id, _caller);
        Assert.AreEqual(1, tracks.Count);
    }

    [TestMethod]
    public async Task AddTrack_Duplicate_Throws()
    {
        var pl = await TestHelpers.SeedPlaylistAsync(_db, _caller.UserId);
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);
        await _service.AddTrackAsync(pl.Id, track.Id, _caller);

        await Assert.ThrowsExactlyAsync<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.AddTrackAsync(pl.Id, track.Id, _caller));
    }

    [TestMethod]
    public async Task RemoveTrack_RemovesFromPlaylist()
    {
        var pl = await TestHelpers.SeedPlaylistAsync(_db, _caller.UserId);
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedPlaylistTrackAsync(_db, pl.Id, track.Id);

        await _service.RemoveTrackAsync(pl.Id, track.Id, _caller);

        var tracks = await _service.GetPlaylistTracksAsync(pl.Id, _caller);
        Assert.AreEqual(0, tracks.Count);
    }

    // ─── GetPlaylistTracks ────────────────────────────────────────────

    [TestMethod]
    public async Task GetPlaylistTracks_OrdersBySortOrder()
    {
        var pl = await TestHelpers.SeedPlaylistAsync(_db, _caller.UserId);
        var (a1, al1, t1) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "First", ownerId: _caller.UserId);
        var (a2, al2, t2) = await TestHelpers.SeedCompleteTrackAsync(_db, trackTitle: "Second", artistName: "A2", albumTitle: "Al2", ownerId: _caller.UserId);
        await TestHelpers.SeedPlaylistTrackAsync(_db, pl.Id, t2.Id, 0);
        await TestHelpers.SeedPlaylistTrackAsync(_db, pl.Id, t1.Id, 1);

        var result = await _service.GetPlaylistTracksAsync(pl.Id, _caller);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Second", result[0].Title);
        Assert.AreEqual("First", result[1].Title);
    }

    [TestMethod]
    public async Task GetPlaylistTracks_Empty_ReturnsEmptyList()
    {
        var pl = await TestHelpers.SeedPlaylistAsync(_db, _caller.UserId);

        var result = await _service.GetPlaylistTracksAsync(pl.Id, _caller);

        Assert.AreEqual(0, result.Count);
    }
}

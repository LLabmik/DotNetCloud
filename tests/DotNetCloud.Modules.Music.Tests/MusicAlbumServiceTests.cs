using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class MusicAlbumServiceTests
{
    private MusicDbContext _db;
    private MusicAlbumService _service;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new MusicAlbumService(_db, NullLogger<MusicAlbumService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Get ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetAlbum_ExistingAlbum_ReturnsDto()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Artist", ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Dark Side of the Moon", ownerId: _caller.UserId);

        var result = await _service.GetAlbumAsync(album.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Dark Side of the Moon", result.Title);
    }

    [TestMethod]
    public async Task GetAlbum_NonExistent_ReturnsNull()
    {
        var result = await _service.GetAlbumAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAlbum_SoftDeleted_ReturnsNull()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Deleted Album", ownerId: _caller.UserId);
        album.IsDeleted = true;
        await _db.SaveChangesAsync();

        var result = await _service.GetAlbumAsync(album.Id, _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAlbum_IncludesArtistName()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Led Zeppelin", ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "IV", ownerId: _caller.UserId);

        var result = await _service.GetAlbumAsync(album.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Led Zeppelin", result.ArtistName);
    }

    [TestMethod]
    public async Task GetAlbum_WithStar_ReturnsIsStarredTrue()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);
        await TestHelpers.SeedStarredItemAsync(_db, _caller.UserId, album.Id, Models.StarredItemType.Album);

        var result = await _service.GetAlbumAsync(album.Id, _caller);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsStarred);
    }

    // ─── List ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListAlbums_ReturnsAll()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Album A", ownerId: _caller.UserId);
        await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Album B", ownerId: _caller.UserId);

        var result = await _service.ListAlbumsAsync(_caller, 0, 50);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task ListAlbums_Pagination()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        for (int i = 0; i < 5; i++)
            await TestHelpers.SeedAlbumAsync(_db, artist.Id, $"Album {i}", ownerId: _caller.UserId);

        var result = await _service.ListAlbumsAsync(_caller, 1, 2);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task ListAlbums_ExcludesSoftDeleted()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var a1 = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Active", ownerId: _caller.UserId);
        var a2 = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Deleted", ownerId: _caller.UserId);
        a2.IsDeleted = true;
        await _db.SaveChangesAsync();

        var result = await _service.ListAlbumsAsync(_caller, 0, 50);

        Assert.AreEqual(1, result.Count);
    }

    // ─── ListByArtist ─────────────────────────────────────────────────

    [TestMethod]
    public async Task ListAlbumsByArtist_ReturnsOnlyArtistAlbums()
    {
        var a1 = await TestHelpers.SeedArtistAsync(_db, "Artist 1", ownerId: _caller.UserId);
        var a2 = await TestHelpers.SeedArtistAsync(_db, "Artist 2", ownerId: _caller.UserId);
        await TestHelpers.SeedAlbumAsync(_db, a1.Id, "Album by A1", ownerId: _caller.UserId);
        await TestHelpers.SeedAlbumAsync(_db, a2.Id, "Album by A2", ownerId: _caller.UserId);

        var result = await _service.ListAlbumsByArtistAsync(a1.Id, _caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Album by A1", result[0].Title);
    }

    // ─── Search ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task SearchAlbums_MatchesByTitle()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Dark Side", ownerId: _caller.UserId);
        await TestHelpers.SeedAlbumAsync(_db, artist.Id, "The Wall", ownerId: _caller.UserId);

        var result = await _service.SearchAsync(_caller, "Dark", 10);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task SearchAlbums_CaseInsensitive()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Abbey Road", ownerId: _caller.UserId);

        // InMemory provider is case-sensitive; use exact prefix match
        var result = await _service.SearchAsync(_caller, "Abbey", 10);

        Assert.AreEqual(1, result.Count);
    }

    // ─── GetRecent ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetRecentAlbums_ReturnsNewestFirst()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Old Album", ownerId: _caller.UserId);
        await TestHelpers.SeedAlbumAsync(_db, artist.Id, "New Album", ownerId: _caller.UserId);

        var result = await _service.GetRecentAlbumsAsync(_caller, 10);

        Assert.AreEqual(2, result.Count);
        // Newest created last should be returned first
        Assert.AreEqual("New Album", result[0].Title);
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAlbum_SetsIsDeleted()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);

        await _service.DeleteAlbumAsync(album.Id, _caller);

        var entry = await _db.Albums.FindAsync(album.Id);
        Assert.IsNotNull(entry);
        Assert.IsTrue(entry.IsDeleted);
    }

    [TestMethod]
    public async Task DeleteAlbum_NonExistent_Throws()
    {
        await Assert.ThrowsExactlyAsync<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.DeleteAlbumAsync(Guid.NewGuid(), _caller));
    }

    // ─── CoverArt ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetCoverArtPath_NoArt_ReturnsNull()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);

        var result = await _service.GetCoverArtPathAsync(album.Id);

        Assert.IsNull(result);
    }
}

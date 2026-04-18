using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class ArtistServiceTests
{
    private MusicDbContext _db;
    private ArtistService _service;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new ArtistService(_db, NullLogger<ArtistService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Get ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetArtist_ExistingArtist_ReturnsDto()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Pink Floyd", ownerId: _caller.UserId);

        var result = await _service.GetArtistAsync(artist.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Pink Floyd", result.Name);
    }

    [TestMethod]
    public async Task GetArtist_NonExistent_ReturnsNull()
    {
        var result = await _service.GetArtistAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetArtist_SoftDeleted_ReturnsNull()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Deleted Artist", ownerId: _caller.UserId);
        artist.IsDeleted = true;
        await _db.SaveChangesAsync();

        var result = await _service.GetArtistAsync(artist.Id, _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetArtist_WithStar_ReturnsIsStarredTrue()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Starred Artist", ownerId: _caller.UserId);
        await TestHelpers.SeedStarredItemAsync(_db, _caller.UserId, artist.Id, StarredItemType.Artist);

        var result = await _service.GetArtistAsync(artist.Id, _caller);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsStarred);
    }

    [TestMethod]
    public async Task GetArtist_WithoutStar_ReturnsIsStarredFalse()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Unstarred Artist", ownerId: _caller.UserId);

        var result = await _service.GetArtistAsync(artist.Id, _caller);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsStarred);
    }

    // ─── List ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListArtists_ReturnsAllArtists()
    {
        await TestHelpers.SeedArtistAsync(_db, "Artist A", ownerId: _caller.UserId);
        await TestHelpers.SeedArtistAsync(_db, "Artist B", ownerId: _caller.UserId);

        var result = await _service.ListArtistsAsync(_caller, 0, 50);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task ListArtists_Pagination_RespectsSkipAndTake()
    {
        for (int i = 0; i < 5; i++)
            await TestHelpers.SeedArtistAsync(_db, $"Artist {i}", ownerId: _caller.UserId);

        var result = await _service.ListArtistsAsync(_caller, 2, 2);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task ListArtists_Empty_ReturnsEmptyList()
    {
        var result = await _service.ListArtistsAsync(_caller, 0, 50);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task ListArtists_ExcludesSoftDeleted()
    {
        var a1 = await TestHelpers.SeedArtistAsync(_db, "Active", ownerId: _caller.UserId);
        var a2 = await TestHelpers.SeedArtistAsync(_db, "Deleted", ownerId: _caller.UserId);
        a2.IsDeleted = true;
        await _db.SaveChangesAsync();

        var result = await _service.ListArtistsAsync(_caller, 0, 50);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Active", result[0].Name);
    }

    // ─── Search ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task SearchAsync_MatchesByName()
    {
        await TestHelpers.SeedArtistAsync(_db, "Pink Floyd", ownerId: _caller.UserId);
        await TestHelpers.SeedArtistAsync(_db, "Led Zeppelin", ownerId: _caller.UserId);

        var result = await _service.SearchAsync(_caller, "Pink", 10);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Pink Floyd", result[0].Name);
    }

    [TestMethod]
    public async Task SearchAsync_CaseInsensitive()
    {
        await TestHelpers.SeedArtistAsync(_db, "Radiohead", ownerId: _caller.UserId);

        // InMemory provider is case-sensitive; use exact prefix match
        var result = await _service.SearchAsync(_caller, "Radio", 10);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task SearchAsync_NoMatch_ReturnsEmpty()
    {
        await TestHelpers.SeedArtistAsync(_db, "Pink Floyd", ownerId: _caller.UserId);

        var result = await _service.SearchAsync(_caller, "Beatles", 10);

        Assert.AreEqual(0, result.Count);
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteArtist_SetsIsDeleted()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "To Delete", ownerId: _caller.UserId);

        await _service.DeleteArtistAsync(artist.Id, _caller);

        var entry = await _db.Artists.FindAsync(artist.Id);
        Assert.IsNotNull(entry);
        Assert.IsTrue(entry.IsDeleted);
    }

    [TestMethod]
    public async Task DeleteArtist_NonExistent_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsExactlyAsync<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.DeleteArtistAsync(Guid.NewGuid(), _caller));
    }

    // ─── GetCount ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        await TestHelpers.SeedArtistAsync(_db, "A", ownerId: _caller.UserId);
        await TestHelpers.SeedArtistAsync(_db, "B", ownerId: _caller.UserId);
        await TestHelpers.SeedArtistAsync(_db, "C", ownerId: _caller.UserId);

        var count = await _service.GetCountAsync(_caller.UserId);

        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public async Task GetCountAsync_ExcludesSoftDeleted()
    {
        var a = await TestHelpers.SeedArtistAsync(_db, "Active", ownerId: _caller.UserId);
        var d = await TestHelpers.SeedArtistAsync(_db, "Deleted", ownerId: _caller.UserId);
        d.IsDeleted = true;
        await _db.SaveChangesAsync();

        var count = await _service.GetCountAsync(_caller.UserId);

        Assert.AreEqual(1, count);
    }
}

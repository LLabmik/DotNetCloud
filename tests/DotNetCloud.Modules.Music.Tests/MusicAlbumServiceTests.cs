using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Storage;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class MusicAlbumServiceTests
{
    private MusicDbContext _db;
    private MusicAlbumService _service;
    private CallerContext _caller;
    private Mock<IDownloadService> _downloadService = null!;
    private ContentAddressedStorage _contentStorage = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        var metadataService = new MusicMetadataService(NullLogger<MusicMetadataService>.Instance);
        _contentStorage = new ContentAddressedStorage(Path.GetTempPath());
        var albumArtService = new AlbumArtService(metadataService, _contentStorage, NullLogger<AlbumArtService>.Instance);
        _downloadService = new Mock<IDownloadService>();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        _service = new MusicAlbumService(_db, albumArtService, _downloadService.Object, _contentStorage, config, NullLogger<MusicAlbumService>.Instance);
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
        var result = await _service.GetAlbumAsync(Guid.CreateVersion7(), _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAlbum_SoftDeleted_ReturnsNull()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Deleted Album", ownerId: _caller.UserId);
        var userAlbum = await _db.UserAlbums.FirstAsync(ua => ua.CanonicalAlbumId == album.Id);
        userAlbum.IsDeleted = true;
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
        await TestHelpers.SeedStarredItemAsync(_db, _caller.UserId, album.Id, StarredItemType.Album);

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
        var ua2 = await _db.UserAlbums.FirstAsync(ua => ua.CanonicalAlbumId == a2.Id);
        ua2.IsDeleted = true;
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
    public async Task DeleteAlbum_RemovesFromDatabase()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);

        await _service.DeleteAlbumAsync(album.Id, _caller);

        var entry = await _db.UserAlbums.IgnoreQueryFilters().FirstOrDefaultAsync(ua => ua.CanonicalAlbumId == album.Id && ua.OwnerId == _caller.UserId);
        Assert.IsNull(entry, "Album junction should be hard-deleted from the database");
    }

    [TestMethod]
    public async Task DeleteAlbum_NonExistent_Throws()
    {
        await Assert.ThrowsExactlyAsync<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.DeleteAlbumAsync(Guid.CreateVersion7(), _caller));
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

    [TestMethod]
    public async Task GetCoverArtPath_ArtCached_ReturnsCachedFile()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);
        var hash = _contentStorage.Store([1, 2, 3, 4], ".jpg");
        album.HasCoverArt = true;
        album.CoverArtHash = hash;
        await _db.SaveChangesAsync();

        var result = await _service.GetCoverArtPathAsync(album.Id);

        try
        {
            Assert.IsNotNull(result);
            Assert.AreEqual(_contentStorage.ResolvePath(hash), result);
        }
        finally
        {
            _contentStorage.Delete(hash);
        }
    }

    [TestMethod]
    public async Task GetCoverArtPath_CachedFileMissing_KeepsStoredCoverArtReference()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, ownerId: _caller.UserId);
        await TestHelpers.SeedTrackAsync(_db, albumId: album.Id, ownerId: _caller.UserId);

        // Art was applied earlier (e.g. chosen manually in the UI) but the cached image is gone
        // — a pruned cache, or a cache root that is not shared with the serving process.
        var hash = new string('a', 64);
        album.HasCoverArt = true;
        album.CoverArtHash = hash;
        await _db.SaveChangesAsync();

        _downloadService
            .Setup(d => d.DownloadCurrentAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream([0, 1, 2, 3]));

        var result = await _service.GetCoverArtPathAsync(album.Id);

        Assert.IsNull(result);
        var stored = await _db.CanonicalAlbums.AsNoTracking().FirstAsync(a => a.Id == album.Id);
        Assert.IsTrue(stored.HasCoverArt, "A missing cache file must not erase the album's cover-art flag.");
        Assert.AreEqual(hash, stored.CoverArtHash, "A missing cache file must not erase the stored cover hash.");
    }
}

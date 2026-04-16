using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class MetadataEnrichmentServiceTests
{
    private MusicDbContext _db = null!;
    private Mock<IMusicBrainzClient> _mockMbClient = null!;
    private Mock<ICoverArtArchiveClient> _mockCaaClient = null!;
    private CallerContext _caller = null!;
    private IConfiguration _configuration = null!;
    private string _tempArtDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _mockMbClient = new Mock<IMusicBrainzClient>();
        _mockCaaClient = new Mock<ICoverArtArchiveClient>();
        _caller = TestHelpers.CreateCaller();
        _tempArtDir = Path.Combine(Path.GetTempPath(), $"dnc-test-art-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempArtDir);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Files:Storage:RootPath"] = _tempArtDir
            })
            .Build();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
        if (Directory.Exists(_tempArtDir))
        {
            try { Directory.Delete(_tempArtDir, true); } catch { }
        }
    }

    private MetadataEnrichmentService CreateService()
    {
        var metadataService = new MusicMetadataService(NullLogger<MusicMetadataService>.Instance);
        var albumArtService = new AlbumArtService(metadataService, NullLogger<AlbumArtService>.Instance);
        return new MetadataEnrichmentService(
            _db,
            _mockMbClient.Object,
            _mockCaaClient.Object,
            albumArtService,
            _configuration,
            NullLogger<MetadataEnrichmentService>.Instance);
    }

    // ── Album Enrichment ─────────────────────────────────────────────

    [TestMethod]
    public async Task EnrichAlbum_NoArt_FetchesFromCAA()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Pink Floyd", ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "The Dark Side of the Moon", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync("The Dark Side of the Moon", "Pink Floyd", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>
            {
                new() { Id = "rg-1", Title = "The Dark Side of the Moon", Score = 100 }
            });

        _mockMbClient.Setup(x => x.GetReleaseGroupAsync("rg-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzReleaseGroupDetail
            {
                Id = "rg-1",
                Title = "The Dark Side of the Moon",
                Releases = [new MusicBrainzRelease { Id = "r-1", Title = "The Dark Side of the Moon" }]
            });

        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        _mockCaaClient.Setup(x => x.GetFrontCoverFromReleasesAsync(It.IsAny<IReadOnlyList<MusicBrainzRelease>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CoverArtResult { Data = imageData, MimeType = "image/jpeg", ReleaseMbid = "r-1" });

        var service = CreateService();
        await service.EnrichAlbumAsync(album.Id, _caller);

        var updatedAlbum = await _db.Albums.FindAsync(album.Id);
        Assert.IsNotNull(updatedAlbum);
        Assert.IsTrue(updatedAlbum.HasCoverArt);
        Assert.IsNotNull(updatedAlbum.CoverArtPath);
    }

    [TestMethod]
    public async Task EnrichAlbum_AlreadyHasArt_SkipsCAAFetch()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Pink Floyd", ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "The Wall", ownerId: _caller.UserId);

        // Create a real temp file so File.Exists returns true in the service
        var tempCoverPath = Path.Combine(Path.GetTempPath(), $"test-cover-{Guid.NewGuid()}.jpg");
        await File.WriteAllBytesAsync(tempCoverPath, [0xFF, 0xD8]);
        try
        {
            album.HasCoverArt = true;
            album.CoverArtPath = tempCoverPath;
            await _db.SaveChangesAsync();

            _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>
                {
                    new() { Id = "rg-1", Title = "The Wall", Score = 95 }
                });

            _mockMbClient.Setup(x => x.GetReleaseGroupAsync("rg-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MusicBrainzReleaseGroupDetail
                {
                    Id = "rg-1",
                    Title = "The Wall",
                    Releases = [new MusicBrainzRelease { Id = "r-1", Title = "The Wall" }]
                });

            var service = CreateService();
            await service.EnrichAlbumAsync(album.Id, _caller);

            _mockCaaClient.Verify(
                x => x.GetFrontCoverFromReleasesAsync(It.IsAny<IReadOnlyList<MusicBrainzRelease>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            File.Delete(tempCoverPath);
        }
    }

    [TestMethod]
    public async Task EnrichAlbum_StoresMusicBrainzIds()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Tool", ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Lateralus", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync("Lateralus", "Tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>
            {
                new() { Id = "rg-lat", Title = "Lateralus", Score = 100 }
            });

        _mockMbClient.Setup(x => x.GetReleaseGroupAsync("rg-lat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzReleaseGroupDetail
            {
                Id = "rg-lat",
                Title = "Lateralus",
                Releases = [new MusicBrainzRelease { Id = "r-lat", Title = "Lateralus" }]
            });

        _mockCaaClient.Setup(x => x.GetFrontCoverFromReleasesAsync(It.IsAny<IReadOnlyList<MusicBrainzRelease>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoverArtResult?)null);

        var service = CreateService();
        await service.EnrichAlbumAsync(album.Id, _caller);

        var updated = await _db.Albums.FindAsync(album.Id);
        Assert.AreEqual("rg-lat", updated!.MusicBrainzReleaseGroupId);
        Assert.AreEqual("r-lat", updated.MusicBrainzReleaseId);
    }

    [TestMethod]
    public async Task EnrichAlbum_NoMBMatch_LeavesUnchanged()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Unknown Band", ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Unknown Album", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>());

        var service = CreateService();
        await service.EnrichAlbumAsync(album.Id, _caller);

        var updated = await _db.Albums.FindAsync(album.Id);
        Assert.IsNull(updated!.MusicBrainzReleaseGroupId);
        Assert.IsFalse(updated.HasCoverArt);
    }

    [TestMethod]
    public async Task EnrichAlbum_CAAReturnsNull_ArtStaysFalse()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Led Zeppelin", ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Led Zeppelin IV", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>
            {
                new() { Id = "rg-lz4", Title = "Led Zeppelin IV", Score = 95 }
            });

        _mockMbClient.Setup(x => x.GetReleaseGroupAsync("rg-lz4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzReleaseGroupDetail
            {
                Id = "rg-lz4",
                Title = "Led Zeppelin IV",
                Releases = [new MusicBrainzRelease { Id = "r-lz4", Title = "Led Zeppelin IV" }]
            });

        _mockCaaClient.Setup(x => x.GetFrontCoverFromReleasesAsync(It.IsAny<IReadOnlyList<MusicBrainzRelease>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoverArtResult?)null);

        var service = CreateService();
        await service.EnrichAlbumAsync(album.Id, _caller);

        var updated = await _db.Albums.FindAsync(album.Id);
        Assert.IsFalse(updated!.HasCoverArt);
    }

    [TestMethod]
    public async Task EnrichAlbum_SetsLastEnrichedAt()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Radiohead", ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "OK Computer", ownerId: _caller.UserId);
        Assert.IsNull(album.LastEnrichedAt);

        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>());

        var beforeEnrich = DateTime.UtcNow;
        var service = CreateService();
        await service.EnrichAlbumAsync(album.Id, _caller);

        var updated = await _db.Albums.FindAsync(album.Id);
        Assert.IsNotNull(updated!.LastEnrichedAt);
        Assert.IsTrue(updated.LastEnrichedAt >= beforeEnrich.AddSeconds(-1));
    }

    [TestMethod]
    public async Task EnrichAlbum_RecentlyEnriched_Skips()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Muse", ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Absolution", ownerId: _caller.UserId);
        album.LastEnrichedAt = DateTime.UtcNow.AddDays(-5);
        await _db.SaveChangesAsync();

        var service = CreateService();
        await service.EnrichAlbumAsync(album.Id, _caller);

        _mockMbClient.Verify(
            x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task EnrichAlbum_RecentlyEnriched_ForceFlag_Enriches()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Muse", ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Absolution", ownerId: _caller.UserId);
        album.LastEnrichedAt = DateTime.UtcNow.AddDays(-5);
        await _db.SaveChangesAsync();

        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>());

        var service = CreateService();
        await service.EnrichAlbumAsync(album.Id, _caller, force: true);

        _mockMbClient.Verify(
            x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task EnrichAlbum_LowMBScore_Skips()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Nirvana", ownerId: _caller.UserId);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Nevermind", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>
            {
                new() { Id = "rg-low", Title = "Nevermind maybe", Score = 50 }
            });

        var service = CreateService();
        await service.EnrichAlbumAsync(album.Id, _caller);

        var updated = await _db.Albums.FindAsync(album.Id);
        Assert.IsNull(updated!.MusicBrainzReleaseGroupId);
        _mockMbClient.Verify(x => x.GetReleaseGroupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task EnrichAlbum_NonExistentAlbum_ReturnsGracefully()
    {
        var service = CreateService();
        await service.EnrichAlbumAsync(Guid.NewGuid(), _caller);

        _mockMbClient.Verify(
            x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Artist Enrichment ────────────────────────────────────────────

    [TestMethod]
    public async Task EnrichArtist_FetchesBioAndLinks()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Pink Floyd", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchArtistAsync("Pink Floyd", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzArtistResult>
            {
                new() { Id = "mb-pf", Name = "Pink Floyd", Score = 100 }
            });

        _mockMbClient.Setup(x => x.GetArtistAsync("mb-pf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzArtistDetail
            {
                Id = "mb-pf",
                Name = "Pink Floyd",
                Annotation = "Pink Floyd were an English rock band.",
                WikipediaUrl = "https://en.wikipedia.org/wiki/Pink_Floyd",
                DiscogsUrl = "https://www.discogs.com/artist/45467",
                OfficialUrl = "https://www.pinkfloyd.com"
            });

        var service = CreateService();
        await service.EnrichArtistAsync(artist.Id, _caller);

        var updated = await _db.Artists.FindAsync(artist.Id);
        Assert.AreEqual("Pink Floyd were an English rock band.", updated!.Biography);
        Assert.AreEqual("https://en.wikipedia.org/wiki/Pink_Floyd", updated.WikipediaUrl);
        Assert.AreEqual("https://www.discogs.com/artist/45467", updated.DiscogsUrl);
        Assert.AreEqual("https://www.pinkfloyd.com", updated.OfficialUrl);
    }

    [TestMethod]
    public async Task EnrichArtist_StoresMusicBrainzId()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Radiohead", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchArtistAsync("Radiohead", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzArtistResult>
            {
                new() { Id = "mb-rh", Name = "Radiohead", Score = 100 }
            });

        _mockMbClient.Setup(x => x.GetArtistAsync("mb-rh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzArtistDetail
            {
                Id = "mb-rh",
                Name = "Radiohead"
            });

        var service = CreateService();
        await service.EnrichArtistAsync(artist.Id, _caller);

        var updated = await _db.Artists.FindAsync(artist.Id);
        Assert.AreEqual("mb-rh", updated!.MusicBrainzId);
    }

    [TestMethod]
    public async Task EnrichArtist_NoAnnotation_BiographyStaysNull()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Tool", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchArtistAsync("Tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzArtistResult>
            {
                new() { Id = "mb-tool", Name = "Tool", Score = 95 }
            });

        _mockMbClient.Setup(x => x.GetArtistAsync("mb-tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzArtistDetail
            {
                Id = "mb-tool",
                Name = "Tool",
                Annotation = null
            });

        var service = CreateService();
        await service.EnrichArtistAsync(artist.Id, _caller);

        var updated = await _db.Artists.FindAsync(artist.Id);
        Assert.IsNull(updated!.Biography);
    }

    [TestMethod]
    public async Task EnrichArtist_NoRelations_LinksStayNull()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Obscure Band", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchArtistAsync("Obscure Band", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzArtistResult>
            {
                new() { Id = "mb-ob", Name = "Obscure Band", Score = 92 }
            });

        _mockMbClient.Setup(x => x.GetArtistAsync("mb-ob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzArtistDetail
            {
                Id = "mb-ob",
                Name = "Obscure Band",
                WikipediaUrl = null,
                DiscogsUrl = null,
                OfficialUrl = null
            });

        var service = CreateService();
        await service.EnrichArtistAsync(artist.Id, _caller);

        var updated = await _db.Artists.FindAsync(artist.Id);
        Assert.IsNull(updated!.WikipediaUrl);
        Assert.IsNull(updated.DiscogsUrl);
        Assert.IsNull(updated.OfficialUrl);
    }

    [TestMethod]
    public async Task EnrichArtist_NoMBMatch_LeavesUnchanged()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Nobody Artist", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchArtistAsync("Nobody Artist", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzArtistResult>());

        var service = CreateService();
        await service.EnrichArtistAsync(artist.Id, _caller);

        var updated = await _db.Artists.FindAsync(artist.Id);
        Assert.IsNull(updated!.MusicBrainzId);
        Assert.IsNull(updated.Biography);
    }

    [TestMethod]
    public async Task EnrichArtist_SetsLastEnrichedAt()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Test Artist", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzArtistResult>());

        var beforeEnrich = DateTime.UtcNow;
        var service = CreateService();
        await service.EnrichArtistAsync(artist.Id, _caller);

        var updated = await _db.Artists.FindAsync(artist.Id);
        Assert.IsNotNull(updated!.LastEnrichedAt);
        Assert.IsTrue(updated.LastEnrichedAt >= beforeEnrich.AddSeconds(-1));
    }

    [TestMethod]
    public async Task EnrichArtist_RecentlyEnriched_Skips()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Recent Artist", ownerId: _caller.UserId);
        artist.LastEnrichedAt = DateTime.UtcNow.AddDays(-5);
        await _db.SaveChangesAsync();

        var service = CreateService();
        await service.EnrichArtistAsync(artist.Id, _caller);

        _mockMbClient.Verify(
            x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task EnrichArtist_RecentlyEnriched_ForceFlag_Enriches()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Force Artist", ownerId: _caller.UserId);
        artist.LastEnrichedAt = DateTime.UtcNow.AddDays(-5);
        await _db.SaveChangesAsync();

        _mockMbClient.Setup(x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzArtistResult>());

        var service = CreateService();
        await service.EnrichArtistAsync(artist.Id, _caller, force: true);

        _mockMbClient.Verify(
            x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task EnrichArtist_PartialRelations_OnlyPopulatesFound()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Partial Artist", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchArtistAsync("Partial Artist", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzArtistResult>
            {
                new() { Id = "mb-pa", Name = "Partial Artist", Score = 95 }
            });

        _mockMbClient.Setup(x => x.GetArtistAsync("mb-pa", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzArtistDetail
            {
                Id = "mb-pa",
                Name = "Partial Artist",
                WikipediaUrl = "https://en.wikipedia.org/wiki/Partial_Artist",
                DiscogsUrl = null,
                OfficialUrl = null
            });

        var service = CreateService();
        await service.EnrichArtistAsync(artist.Id, _caller);

        var updated = await _db.Artists.FindAsync(artist.Id);
        Assert.AreEqual("https://en.wikipedia.org/wiki/Partial_Artist", updated!.WikipediaUrl);
        Assert.IsNull(updated.DiscogsUrl);
        Assert.IsNull(updated.OfficialUrl);
    }

    [TestMethod]
    public async Task EnrichArtist_NonExistent_ReturnsGracefully()
    {
        var service = CreateService();
        await service.EnrichArtistAsync(Guid.NewGuid(), _caller);

        _mockMbClient.Verify(
            x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Track Enrichment ─────────────────────────────────────────────

    [TestMethod]
    public async Task EnrichTrack_StoresMusicBrainzRecordingId()
    {
        var (artist, album, track) = await TestHelpers.SeedCompleteTrackAsync(
            _db, "Pink Floyd", "Wish You Were Here", "Shine On You Crazy Diamond", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchRecordingAsync("Shine On You Crazy Diamond", "Pink Floyd", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzRecordingResult>
            {
                new() { Id = "rec-shine", Title = "Shine On You Crazy Diamond", Score = 98 }
            });

        var service = CreateService();
        await service.EnrichTrackAsync(track.Id, _caller);

        var updated = await _db.Tracks.FindAsync(track.Id);
        Assert.AreEqual("rec-shine", updated!.MusicBrainzRecordingId);
    }

    [TestMethod]
    public async Task EnrichTrack_NoMatch_LeavesUnchanged()
    {
        var (artist, album, track) = await TestHelpers.SeedCompleteTrackAsync(
            _db, "Unknown", "Unknown Album", "Unknown Track", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchRecordingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzRecordingResult>());

        var service = CreateService();
        await service.EnrichTrackAsync(track.Id, _caller);

        var updated = await _db.Tracks.FindAsync(track.Id);
        Assert.IsNull(updated!.MusicBrainzRecordingId);
    }

    [TestMethod]
    public async Task EnrichTrack_SetsLastEnrichedAt()
    {
        var (artist, album, track) = await TestHelpers.SeedCompleteTrackAsync(
            _db, "Artist", "Album", "Track", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchRecordingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzRecordingResult>());

        var beforeEnrich = DateTime.UtcNow;
        var service = CreateService();
        await service.EnrichTrackAsync(track.Id, _caller);

        var updated = await _db.Tracks.FindAsync(track.Id);
        Assert.IsNotNull(updated!.LastEnrichedAt);
        Assert.IsTrue(updated.LastEnrichedAt >= beforeEnrich.AddSeconds(-1));
    }

    // ── Batch Enrichment ─────────────────────────────────────────────

    [TestMethod]
    public async Task EnrichAlbumsWithoutArt_ProcessesOnlyMissingArt()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Batch Artist", ownerId: _caller.UserId);
        var albumWithArt = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Has Art", ownerId: _caller.UserId);
        albumWithArt.HasCoverArt = true;
        albumWithArt.CoverArtPath = "/existing.jpg";

        var albumNoArt1 = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "No Art 1", ownerId: _caller.UserId);
        var albumNoArt2 = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "No Art 2", ownerId: _caller.UserId);
        await _db.SaveChangesAsync();

        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>());

        var service = CreateService();
        await service.EnrichAlbumsWithoutArtAsync(_caller.UserId);

        // Should only search for the 2 albums without art, not the one with art
        _mockMbClient.Verify(
            x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task EnrichAlbumsWithoutArt_ReportsProgress()
    {
        var artist = await TestHelpers.SeedArtistAsync(_db, "Progress Artist", ownerId: _caller.UserId);
        await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Album 1", ownerId: _caller.UserId);
        await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Album 2", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>());

        var progressReports = new List<EnrichmentProgress>();
        var progress = new Progress<EnrichmentProgress>(p => progressReports.Add(p));

        var service = CreateService();
        await service.EnrichAlbumsWithoutArtAsync(_caller.UserId, progress);

        // Allow progress callbacks to fire (Progress<T> uses SynchronizationContext)
        await Task.Delay(100);

        Assert.IsTrue(progressReports.Count >= 2, $"Expected at least 2 progress reports, got {progressReports.Count}");
        Assert.IsTrue(progressReports.Any(p => p.CurrentItem == "Album 1"));
        Assert.IsTrue(progressReports.Any(p => p.CurrentItem == "Album 2"));
    }

    [TestMethod]
    public async Task EnrichAll_ProcessesAllEntityTypes()
    {
        var (artist, album, track) = await TestHelpers.SeedCompleteTrackAsync(
            _db, "Enrich All Artist", "Enrich All Album", "Enrich All Track", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzArtistResult>());
        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>());
        _mockMbClient.Setup(x => x.SearchRecordingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzRecordingResult>());

        var service = CreateService();
        await service.EnrichAllAsync(_caller.UserId);

        // Verify all three types were searched
        _mockMbClient.Verify(x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockMbClient.Verify(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockMbClient.Verify(x => x.SearchRecordingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task EnrichAll_SkipsAlreadyEnriched()
    {
        var artist1 = await TestHelpers.SeedArtistAsync(_db, "Enriched Artist", ownerId: _caller.UserId);
        artist1.LastEnrichedAt = DateTime.UtcNow.AddDays(-5);

        var artist2 = await TestHelpers.SeedArtistAsync(_db, "Unenriched Artist", ownerId: _caller.UserId);
        await _db.SaveChangesAsync();

        _mockMbClient.Setup(x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzArtistResult>());
        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>());
        _mockMbClient.Setup(x => x.SearchRecordingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzRecordingResult>());

        var service = CreateService();
        await service.EnrichAllAsync(_caller.UserId);

        // Only unenriched artist should be searched (artist1 has LastEnrichedAt set)
        _mockMbClient.Verify(
            x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task EnrichAll_CancellationToken_StopsEarly()
    {
        var artist1 = await TestHelpers.SeedArtistAsync(_db, "Artist 1", ownerId: _caller.UserId);
        var artist2 = await TestHelpers.SeedArtistAsync(_db, "Artist 2", ownerId: _caller.UserId);
        var artist3 = await TestHelpers.SeedArtistAsync(_db, "Artist 3", ownerId: _caller.UserId);

        var cts = new CancellationTokenSource();
        var callCount = 0;

        _mockMbClient.Setup(x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (name, ct) =>
            {
                callCount++;
                if (callCount >= 2) cts.Cancel();
                return new List<MusicBrainzArtistResult>();
            });

        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>());
        _mockMbClient.Setup(x => x.SearchRecordingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzRecordingResult>());

        var service = CreateService();

        var threw = false;
        try
        {
            await service.EnrichAllAsync(_caller.UserId, cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Assert.IsTrue(threw, "Expected OperationCanceledException to be thrown");
        Assert.IsTrue(callCount < 3, $"Expected fewer than 3 calls, got {callCount}");
    }

    [TestMethod]
    public async Task EnrichAll_ProgressReporting_AllPhases()
    {
        var (artist, album, track) = await TestHelpers.SeedCompleteTrackAsync(
            _db, "Phase Artist", "Phase Album", "Phase Track", ownerId: _caller.UserId);

        _mockMbClient.Setup(x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzArtistResult>());
        _mockMbClient.Setup(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzReleaseGroupResult>());
        _mockMbClient.Setup(x => x.SearchRecordingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzRecordingResult>());

        var phases = new List<string>();
        var progress = new Progress<EnrichmentProgress>(p =>
        {
            if (!phases.Contains(p.Phase))
                phases.Add(p.Phase);
        });

        var service = CreateService();
        await service.EnrichAllAsync(_caller.UserId, progress);

        await Task.Delay(100);

        Assert.IsTrue(phases.Any(p => p.Contains("artist", StringComparison.OrdinalIgnoreCase)),
            $"Expected artist phase, got phases: {string.Join(", ", phases)}");
        Assert.IsTrue(phases.Any(p => p.Contains("album", StringComparison.OrdinalIgnoreCase)),
            $"Expected album phase, got phases: {string.Join(", ", phases)}");
        Assert.IsTrue(phases.Any(p => p.Contains("track", StringComparison.OrdinalIgnoreCase)),
            $"Expected track phase, got phases: {string.Join(", ", phases)}");
    }

    [TestMethod]
    public async Task EnrichAll_EmptyLibrary_CompletesImmediately()
    {
        var service = CreateService();
        await service.EnrichAllAsync(_caller.UserId);

        _mockMbClient.Verify(x => x.SearchArtistAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockMbClient.Verify(x => x.SearchReleaseGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockMbClient.Verify(x => x.SearchRecordingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

using System.IO.Compression;
using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Storage;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Host.Controllers;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Tests for <see cref="MusicController.DownloadAlbumAsync"/> endpoint.
/// Covers not-found, empty-tracks, FileNodeId-skipping, and successful download cases.
/// </summary>
[TestClass]
public class MusicControllerTests
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid AlbumId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private MusicDbContext _db = null!;
    private IDbContextFactory<MusicDbContext> _dbFactory = null!;
    private Mock<IDownloadService> _downloadMock = null!;
    private MusicController _controller = null!;

    [TestInitialize]
    public async Task Setup()
    {
        (_db, _dbFactory) = TestHelpers.CreateDbWithFactory();

        // Seed canonical album
        var canonicalAlbum = new CanonicalAlbum { Id = AlbumId, Title = "Test Album", Year = 2024 };
        _db.CanonicalAlbums.Add(canonicalAlbum);

        // Seed user album junction (required for querying)
        var userAlbum = new UserAlbum
        {
            CanonicalAlbumId = AlbumId,
            OwnerId = TestUserId
        };
        _db.UserAlbums.Add(userAlbum);
        await _db.SaveChangesAsync();

        _downloadMock = new Mock<IDownloadService>();

        // Build real services with in-memory DB
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var metadataService = new MusicMetadataService(NullLogger<MusicMetadataService>.Instance);
        var contentStorage = new ContentAddressedStorage(Path.GetTempPath());
        var albumArtService = new AlbumArtService(metadataService, contentStorage, NullLogger<AlbumArtService>.Instance);
        var eventBus = new Mock<IEventBus>();

        var artistService = new ArtistService(_dbFactory, NullLogger<ArtistService>.Instance);
        var albumService = new MusicAlbumService(
            _db, albumArtService, _downloadMock.Object, contentStorage,
            config, NullLogger<MusicAlbumService>.Instance);
        var trackService = new TrackService(_db, eventBus.Object, Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(), NullLogger<TrackService>.Instance);

        _controller = new MusicController(
            artistService,
            albumService,
            trackService,
            null!,  // PlaylistService - not used by download
            null!,  // PlaybackService
            null!,  // RecommendationService
            null!,  // MusicStreamingService
            null!,  // EqPresetService
            null!,  // IMetadataEnrichmentService
            null!,  // ScanProgressState
            _downloadMock.Object,
            NullLogger<MusicController>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────

    private void SetupAuthenticatedUser()
    {
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
            new Claim(ClaimTypes.Role, "user")
        ], "Test");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private async Task<CanonicalTrack> SeedTrackAsync(string title, Guid fileNodeId, int trackNum = 1)
    {
        var hash = $"SHA256-{Guid.CreateVersion7():N}";
        var ct = new CanonicalTrack
        {
            ContentHash = hash,
            Title = title,
            TrackNumber = trackNum,
            MimeType = "audio/mpeg",
            DurationTicks = TimeSpan.FromMinutes(3).Ticks
        };
        _db.CanonicalTracks.Add(ct);

        var ut = new UserTrack
        {
            OwnerId = TestUserId,
            CanonicalTrackHash = hash,
            CanonicalAlbumId = AlbumId,
            FileNodeId = fileNodeId,
            ContentHash = hash
        };
        _db.UserTracks.Add(ut);
        await _db.SaveChangesAsync();

        // Reload with navigation properties
        ct = (await _db.CanonicalTracks.FindAsync(hash))!;
        return ct;
    }

    // ── Guard cases ───────────────────────────────────────────────────

    [TestMethod]
    public async Task DownloadAlbum_AlbumNotFound_ReturnsNotFound()
    {
        SetupAuthenticatedUser();

        var result = await _controller.DownloadAlbumAsync(Guid.CreateVersion7());

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DownloadAlbum_NoTracks_ReturnsNotFound()
    {
        SetupAuthenticatedUser();

        var result = await _controller.DownloadAlbumAsync(AlbumId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // ── FileNodeId skipping ───────────────────────────────────────────

    [TestMethod]
    public async Task DownloadAlbum_FileNodeIdEmpty_Skipped()
    {
        SetupAuthenticatedUser();
        await SeedTrackAsync("Empty Track", Guid.Empty);

        // Album has a track but FileNodeId is Guid.Empty → skipped → 0 valid tracks
        var result = await _controller.DownloadAlbumAsync(AlbumId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DownloadAlbum_MixedNodeIds_SkipsEmpty()
    {
        SetupAuthenticatedUser();
        await SeedTrackAsync("Skipped", Guid.Empty);
        await SeedTrackAsync("Downloaded", Guid.Parse("cccccccc-3333-3333-3333-333333333333"), trackNum: 2);

        var testAudio = new byte[] { 0xFF, 0xFB, 0x90, 0x00 };
        _downloadMock
            .Setup(d => d.DownloadCurrentAsync(Guid.Parse("cccccccc-3333-3333-3333-333333333333"),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(testAudio));

        var result = await _controller.DownloadAlbumAsync(AlbumId);

        Assert.IsInstanceOfType<FileStreamResult>(result);
        var fileResult = (FileStreamResult)result;
        Assert.AreEqual("application/zip", fileResult.ContentType);
        Assert.IsTrue(fileResult.FileDownloadName.EndsWith(".zip"));

        // Clean up
        try
        { fileResult.FileStream?.Dispose(); }
        catch { }
    }

    // ── Successful download ───────────────────────────────────────────

    [TestMethod]
    public async Task DownloadAlbum_ValidTracks_ReturnsZipFile()
    {
        SetupAuthenticatedUser();
        var fileNodeId1 = Guid.Parse("dddddddd-4444-4444-4444-444444444444");
        var fileNodeId2 = Guid.Parse("eeeeeeee-5555-5555-5555-555555555555");
        await SeedTrackAsync("Song One", fileNodeId1, 1);
        await SeedTrackAsync("Song Two", fileNodeId2, 2);

        var audio1 = new byte[] { 0xFF, 0xFB, 0x90, 0x00, 0x01, 0x02 };
        var audio2 = new byte[] { 0xFF, 0xFB, 0x90, 0x00, 0x03, 0x04 };
        _downloadMock
            .Setup(d => d.DownloadCurrentAsync(fileNodeId1, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(audio1));
        _downloadMock
            .Setup(d => d.DownloadCurrentAsync(fileNodeId2, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(audio2));

        var result = await _controller.DownloadAlbumAsync(AlbumId);

        Assert.IsInstanceOfType<FileStreamResult>(result);
        var fileResult = (FileStreamResult)result;
        Assert.AreEqual("application/zip", fileResult.ContentType);
        Assert.IsTrue(fileResult.FileDownloadName.EndsWith(".zip"));

        // Read stream into memory to verify ZIP contents
        using var ms = new MemoryStream();
        await fileResult.FileStream!.CopyToAsync(ms);
        ms.Position = 0;

        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var entries = archive.Entries.OrderBy(e => e.Name).ToList();
        Assert.AreEqual(2, entries.Count, "ZIP should contain 2 tracks.");
        Assert.AreEqual("01 - Song One.mp3", entries[0].Name);
        Assert.AreEqual("02 - Song Two.mp3", entries[1].Name);

        // Verify no compression (stored)
        foreach (var entry in entries)
        {
            Assert.AreEqual(0, entry.CompressedLength - entry.Length, 1,
                $"Entry '{entry.Name}' should use no compression.");
        }

        // Verify download service was called
        _downloadMock.Verify(d => d.DownloadCurrentAsync(fileNodeId1, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        _downloadMock.Verify(d => d.DownloadCurrentAsync(fileNodeId2, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DownloadAlbum_ZipFileName_ContainsAlbumTitle()
    {
        SetupAuthenticatedUser();
        var fileNodeId = Guid.Parse("ffffffff-6666-6666-6666-666666666666");
        await SeedTrackAsync("Only Track", fileNodeId, 1);

        _downloadMock
            .Setup(d => d.DownloadCurrentAsync(fileNodeId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([0xFF, 0xFB]));

        var result = await _controller.DownloadAlbumAsync(AlbumId);

        Assert.IsInstanceOfType<FileStreamResult>(result);
        var fileResult = (FileStreamResult)result;
        Assert.AreEqual("Unknown_Artist - Test_Album.zip", fileResult.FileDownloadName);

        // Clean up
        try
        { fileResult.FileStream?.Dispose(); }
        catch { }
    }
}

using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class MusicIndexingCallbackTests
{
    private MusicDbContext _db = null!;
    private LibraryScanService _libraryScanService = null!;
    private MusicIndexingCallback _callback = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        var metadataService = new MusicMetadataService(Mock.Of<ILogger<MusicMetadataService>>());
        var albumArtService = new AlbumArtService(metadataService, Mock.Of<ILogger<AlbumArtService>>());
        _libraryScanService = new LibraryScanService(
            _db, metadataService, albumArtService,
            Mock.Of<IEventBus>(), Mock.Of<ILogger<LibraryScanService>>());

        // Mock IDownloadService — returns empty stream (metadata extraction will fall back to filename)
        var downloadMock = new Mock<IDownloadService>();
        downloadMock
            .Setup(d => d.DownloadCurrentAsync(It.IsAny<Guid>(), It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stream.Null);
        _callback = new MusicIndexingCallback(_libraryScanService, downloadMock.Object, Mock.Of<ILogger<MusicIndexingCallback>>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task IndexAudioAsync_CreatesTrackInDatabase()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await _callback.IndexAudioAsync(fileNodeId, "song.mp3", "audio/mpeg", 5_000_000, ownerId);

        // LibraryScanService should create a track (metadata extraction may fail but track
        // record should still be created from the filename)
        var count = _db.Tracks.Count(t => t.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexAudioAsync_SetsCorrectOwner()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await _callback.IndexAudioAsync(fileNodeId, "track.flac", "audio/flac", 30_000_000, ownerId);

        var track = _db.Tracks.FirstOrDefault(t => t.FileNodeId == fileNodeId);
        Assert.IsNotNull(track);
        Assert.AreEqual(ownerId, track.OwnerId);
    }

    [TestMethod]
    public async Task IndexAudioAsync_DuplicateFileNode_DoesNotCreateSecond()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await _callback.IndexAudioAsync(fileNodeId, "song.mp3", "audio/mpeg", 1024, ownerId);
        await _callback.IndexAudioAsync(fileNodeId, "song.mp3", "audio/mpeg", 1024, ownerId);

        var count = _db.Tracks.Count(t => t.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexAudioAsync_MultipleUniqueFiles_CreatesAll()
    {
        var ownerId = Guid.NewGuid();

        await _callback.IndexAudioAsync(Guid.NewGuid(), "song1.mp3", "audio/mpeg", 1024, ownerId);
        await _callback.IndexAudioAsync(Guid.NewGuid(), "song2.flac", "audio/flac", 2048, ownerId);
        await _callback.IndexAudioAsync(Guid.NewGuid(), "song3.ogg", "audio/ogg", 512, ownerId);

        Assert.AreEqual(3, _db.Tracks.Count());
    }
}

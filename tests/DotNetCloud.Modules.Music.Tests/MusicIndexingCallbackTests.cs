using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Storage;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        var contentStorage = new ContentAddressedStorage(Path.GetTempPath());
        var albumArtService = new AlbumArtService(metadataService, contentStorage, Mock.Of<ILogger<AlbumArtService>>());
        _libraryScanService = new LibraryScanService(
            _db, metadataService, albumArtService,
            Mock.Of<IEventBus>(), new ConfigurationBuilder().Build(), Mock.Of<ILogger<LibraryScanService>>(),
            Mock.Of<ITableNamingStrategy>());

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
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

        await _callback.IndexAudioAsync(fileNodeId, "song.mp3", "audio/mpeg", 5_000_000, ownerId);

        // LibraryScanService should create a track (metadata extraction may fail but track
        // record should still be created from the filename)
        var count = _db.UserTracks.Count(ut => ut.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexAudioAsync_SetsCorrectOwner()
    {
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

        await _callback.IndexAudioAsync(fileNodeId, "track.flac", "audio/flac", 30_000_000, ownerId);

        var track = _db.UserTracks.FirstOrDefault(ut => ut.FileNodeId == fileNodeId);
        Assert.IsNotNull(track);
        Assert.AreEqual(ownerId, track.OwnerId);
    }

    [TestMethod]
    public async Task IndexAudioAsync_DuplicateFileNode_DoesNotCreateSecond()
    {
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

        await _callback.IndexAudioAsync(fileNodeId, "song.mp3", "audio/mpeg", 1024, ownerId);
        await _callback.IndexAudioAsync(fileNodeId, "song.mp3", "audio/mpeg", 1024, ownerId);

        var count = _db.UserTracks.Count(ut => ut.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexAudioAsync_MultipleUniqueFiles_CreatesAll()
    {
        var ownerId = Guid.CreateVersion7();

        await _callback.IndexAudioAsync(Guid.CreateVersion7(), "song1.mp3", "audio/mpeg", 1024, ownerId);
        await _callback.IndexAudioAsync(Guid.CreateVersion7(), "song2.flac", "audio/flac", 2048, ownerId);
        await _callback.IndexAudioAsync(Guid.CreateVersion7(), "song3.ogg", "audio/ogg", 512, ownerId);

        Assert.AreEqual(3, _db.UserTracks.Count());
    }

    // ── Cross-owner copy tests ──

    [TestMethod]
    public async Task IndexFileAsync_CrossOwner_SameFileNodeId_ClonesTrack()
    {
        // Arrange: User A has already indexed a file
        var fileNodeId = Guid.CreateVersion7();
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();

        var sourceArtist = await TestHelpers.SeedArtistAsync(_db, "Infected Mushroom", null, userA);
        var sourceAlbum = await TestHelpers.SeedAlbumAsync(_db, sourceArtist.Id, "IM The Supervisor", 2004, userA);
        var sourceTrack = await TestHelpers.SeedTrackAsync(_db, sourceAlbum.Id, "Muduzz", ownerId: userA);
        sourceTrack.FileNodeId = fileNodeId;
        await _db.SaveChangesAsync();

        // Act: User B scans the same FileNodeId
        await _callback.IndexAudioAsync(fileNodeId, "muduzz.flac", "audio/flac", 30_000_000, userB);

        // Assert: User B now has a track for this FileNodeId with cloned metadata
        var userBTrack = _db.UserTracks
            .Include(ut => ut.CanonicalAlbum)
            .FirstOrDefault(ut => ut.FileNodeId == fileNodeId && ut.OwnerId == userB);
        Assert.IsNotNull(userBTrack, "User B should have a cloned track");
        Assert.AreEqual("Muduzz", userBTrack.CanonicalTrack!.Title);
        Assert.IsNotNull(userBTrack.CanonicalAlbum, "Album should be cloned along with track metadata");
        Assert.AreEqual("IM The Supervisor", userBTrack.CanonicalAlbum!.Title);
    }

    [TestMethod]
    public async Task IndexFileAsync_CrossOwner_SourceTrackNotModified()
    {
        // Arrange: User A has already indexed a file
        var fileNodeId = Guid.CreateVersion7();
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();

        var sourceArtist = await TestHelpers.SeedArtistAsync(_db, "Infected Mushroom", null, userA);
        var sourceAlbum = await TestHelpers.SeedAlbumAsync(_db, sourceArtist.Id, "IM The Supervisor", 2004, userA);
        var sourceTrack = await TestHelpers.SeedTrackAsync(_db, sourceAlbum.Id, "Muduzz", ownerId: userA);
        sourceTrack.FileNodeId = fileNodeId;
        await _db.SaveChangesAsync();
        var sourceTrackId = sourceTrack.Id;

        // Act: User B scans the same FileNodeId
        await _callback.IndexAudioAsync(fileNodeId, "muduzz.flac", "audio/flac", 30_000_000, userB);

        // Assert: User A's track still exists and is unchanged
        var verifySource = _db.UserTracks.IgnoreQueryFilters().FirstOrDefault(ut => ut.Id == sourceTrackId);
        Assert.IsNotNull(verifySource, "Source track should still exist");
        Assert.IsFalse(verifySource.IsDeleted, "Source track should NOT be deleted");
        Assert.AreEqual(userA, verifySource.OwnerId, "Source track OwnerId should be unchanged");
        Assert.AreEqual(sourceTrack.CanonicalTrack!.Title, verifySource.CanonicalTrack!.Title, "Source track Title should be unchanged");
    }

    [TestMethod]
    public async Task IndexFileAsync_CrossOwner_BothUsersHaveIndependentTracks()
    {
        // Arrange
        var fileNodeId = Guid.CreateVersion7();
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();

        var sourceArtist = await TestHelpers.SeedArtistAsync(_db, "Test Artist", null, userA);
        var sourceAlbum = await TestHelpers.SeedAlbumAsync(_db, sourceArtist.Id, "Test Album", null, userA);
        var sourceTrack = await TestHelpers.SeedTrackAsync(_db, sourceAlbum.Id, "Test Song", ownerId: userA);
        sourceTrack.FileNodeId = fileNodeId;
        await _db.SaveChangesAsync();

        // Act: User B scans same file
        await _callback.IndexAudioAsync(fileNodeId, "test.flac", "audio/flac", 10_000, userB);

        // Assert: Both users have exactly one track each for this FileNodeId
        var userATracks = _db.UserTracks.Count(ut => ut.FileNodeId == fileNodeId && ut.OwnerId == userA);
        var userBTracks = _db.UserTracks.Count(ut => ut.FileNodeId == fileNodeId && ut.OwnerId == userB);
        Assert.AreEqual(1, userATracks, "User A should have 1 track");
        Assert.AreEqual(1, userBTracks, "User B should have 1 track");
    }

    [TestMethod]
    public async Task IndexFileAsync_SameOwner_DuplicateNotCreated()
    {
        // Arrange
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

        await _callback.IndexAudioAsync(fileNodeId, "song.mp3", "audio/mpeg", 1024, ownerId);

        // Act: Same user scans same file again
        await _callback.IndexAudioAsync(fileNodeId, "song.mp3", "audio/mpeg", 1024, ownerId);

        // Assert: Only one track exists
        var count = _db.UserTracks.Count(ut => ut.FileNodeId == fileNodeId && ut.OwnerId == ownerId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexFileAsync_CrossOwner_SourceWithoutAlbum_StillClonesTrack()
    {
        // Arrange: User A has a track with no album
        var fileNodeId = Guid.CreateVersion7();
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();

        var sourceArtist = await TestHelpers.SeedArtistAsync(_db, "Solo Artist", null, userA);
        var sourceTrack = await TestHelpers.SeedTrackAsync(_db, null, "Standalone Track", ownerId: userA);
        sourceTrack.FileNodeId = fileNodeId;
        await _db.SaveChangesAsync();

        // Act: User B scans the same FileNodeId
        await _callback.IndexAudioAsync(fileNodeId, "standalone.flac", "audio/flac", 10_000, userB);

        // Assert: User B gets a track with no album (graceful handling)
        var userBTrack = _db.UserTracks.FirstOrDefault(ut => ut.FileNodeId == fileNodeId && ut.OwnerId == userB);
        Assert.IsNotNull(userBTrack, "User B should still get a track even if source has no album");
        Assert.AreEqual("Standalone Track", userBTrack.CanonicalTrack!.Title);
        Assert.IsNull(userBTrack.CanonicalAlbumId, "CanonicalAlbumId should be null if source had no album");
    }

    [TestMethod]
    public async Task IndexFileAsync_CrossOwner_SoftDeletedSourceIgnored()
    {
        // Arrange: User A has a soft-deleted track
        var fileNodeId = Guid.CreateVersion7();
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();

        var sourceTrack = await TestHelpers.SeedTrackAsync(_db, null, "Deleted Song", ownerId: userA);
        sourceTrack.FileNodeId = fileNodeId;
        sourceTrack.IsDeleted = true;
        sourceTrack.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Act: User B scans the same FileNodeId
        await _callback.IndexAudioAsync(fileNodeId, "song.mp3", "audio/mpeg", 5000, userB);

        // Assert: User B gets a track (from metadata extraction, not cross-owner copy since source is deleted)
        var userBTrack = _db.UserTracks.FirstOrDefault(ut => ut.FileNodeId == fileNodeId && ut.OwnerId == userB);
        Assert.IsNotNull(userBTrack, "User B should get a track via fresh extraction");
        Assert.IsFalse(userBTrack.IsDeleted, "User B's track should not be deleted");
    }

    // ── Reset collection owner-scoping tests ──

    [TestMethod]
    public async Task ResetCollectionAsync_OnlyDeletesTargetOwner()
    {
        // Arrange: Two users, each with tracks
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();

        var artistA = await TestHelpers.SeedArtistAsync(_db, "Artist A", null, userA);
        var albumA = await TestHelpers.SeedAlbumAsync(_db, artistA.Id, "Album A", null, userA);
        await TestHelpers.SeedTrackAsync(_db, albumA.Id, "Track A1", ownerId: userA);
        await TestHelpers.SeedTrackAsync(_db, albumA.Id, "Track A2", ownerId: userA);

        var artistB = await TestHelpers.SeedArtistAsync(_db, "Artist B", null, userB);
        var albumB = await TestHelpers.SeedAlbumAsync(_db, artistB.Id, "Album B", null, userB);
        await TestHelpers.SeedTrackAsync(_db, albumB.Id, "Track B1", ownerId: userB);

        // Act: Reset only User A's library
        await _libraryScanService.ResetCollectionAsync(userA);

        // Assert: User A's tracks are gone, User B's tracks survive
        var aTracks = _db.UserTracks.IgnoreQueryFilters().Count(ut => ut.OwnerId == userA);
        var bTracks = _db.UserTracks.IgnoreQueryFilters().Count(ut => ut.OwnerId == userB);
        Assert.AreEqual(0, aTracks, "User A's tracks should be deleted");
        Assert.AreEqual(1, bTracks, "User B's tracks should survive");
    }

    [TestMethod]
    public async Task ResetCollectionAsync_OtherOwnerAlbumsAndArtistsSurvive()
    {
        // Arrange
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();

        var artistB = await TestHelpers.SeedArtistAsync(_db, "Surviving Artist", null, userB);
        var albumB = await TestHelpers.SeedAlbumAsync(_db, artistB.Id, "Surviving Album", null, userB);
        await TestHelpers.SeedTrackAsync(_db, albumB.Id, "Surviving Track", ownerId: userB);

        // Act: Reset User A (who has nothing)
        await _libraryScanService.ResetCollectionAsync(userA);

        // Assert: User B still has everything
        var bArtist = _db.UserArtists.IgnoreQueryFilters().FirstOrDefault(ua => ua.OwnerId == userB);
        var bAlbum = _db.UserAlbums.IgnoreQueryFilters().FirstOrDefault(ua => ua.OwnerId == userB);
        var bTrack = _db.UserTracks.IgnoreQueryFilters().FirstOrDefault(ut => ut.OwnerId == userB);
        Assert.IsNotNull(bArtist, "User B's artist should survive");
        Assert.IsNotNull(bAlbum, "User B's album should survive");
        Assert.IsNotNull(bTrack, "User B's track should survive");
    }

    [TestMethod]
    public async Task ResetCollectionAsync_TracksWithPlayHistory_CleanedUp()
    {
        // Arrange: User has tracks with playback history
        var userA = Guid.CreateVersion7();
        var artist = await TestHelpers.SeedArtistAsync(_db, "Artist", null, userA);
        var album = await TestHelpers.SeedAlbumAsync(_db, artist.Id, "Album", null, userA);
        var track = await TestHelpers.SeedTrackAsync(_db, album.Id, "Track", ownerId: userA);

        _db.PlaybackHistories.Add(new DotNetCloud.Modules.Music.Models.PlaybackHistory
        {
            UserId = userA,
            UserTrackId = track.Id,
            PlayedAt = DateTime.UtcNow,
            DurationPlayedSeconds = 120
        });
        await _db.SaveChangesAsync();

        // Act: Reset
        await _libraryScanService.ResetCollectionAsync(userA);

        // Assert: Track and playback history are gone
        var trackCount = _db.UserTracks.IgnoreQueryFilters().Count(ut => ut.OwnerId == userA);
        var historyCount = _db.PlaybackHistories.IgnoreQueryFilters().Count();
        Assert.AreEqual(0, trackCount);
        Assert.AreEqual(0, historyCount);
    }
}

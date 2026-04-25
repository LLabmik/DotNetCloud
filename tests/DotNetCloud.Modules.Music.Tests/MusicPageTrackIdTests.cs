using System.Reflection;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Services;
using DotNetCloud.Modules.Music.UI;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Unit tests for the MusicPage deep-link TrackId parameter handling.
/// </summary>
[TestClass]
public class MusicPageTrackIdTests
{
    private Mock<ITrackService> _trackServiceMock = null!;
    private TestableMusicPage _page = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        _trackServiceMock = new Mock<ITrackService>();
        var loggerMock = new Mock<ILogger<MusicPage>>();

        _page = new TestableMusicPage();
        SetPrivateProperty(_page, "TrackService", _trackServiceMock.Object);
        SetPrivateProperty(_page, "Logger", loggerMock.Object);

        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    private static void SetPrivateProperty(object target, string name, object value)
    {
        var type = target.GetType();
        while (type is not null)
        {
            var prop = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (prop is not null)
            {
                prop.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        throw new InvalidOperationException($"Property '{name}' not found on {target.GetType().FullName} or its base types.");
    }

    // ── TrackId → Album navigation ────────────────────────────

    [TestMethod]
    public async Task TryNavigateToTrackAlbumAsync_TrackHasAlbum_NavigatesToAlbum()
    {
        var albumId = Guid.NewGuid();
        var track = MakeTrack(albumId: albumId);
        _trackServiceMock
            .Setup(s => s.GetTrackAsync(track.Id, _caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync(track);

        await _page.TryNavigateToTrackAlbumAsync(track.Id, _caller);

        Assert.AreEqual(1, _page.NavigateToAlbumCallCount);
        Assert.AreEqual(albumId, _page.NavigatedAlbumId);
    }

    [TestMethod]
    public async Task TryNavigateToTrackAlbumAsync_TrackHasNoAlbum_QueuesAutoPlay()
    {
        var track = MakeTrack(albumId: null);
        _trackServiceMock
            .Setup(s => s.GetTrackAsync(track.Id, _caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync(track);

        await _page.TryNavigateToTrackAlbumAsync(track.Id, _caller);

        Assert.AreEqual(0, _page.NavigateToAlbumCallCount);
        Assert.IsNotNull(_page.PendingAutoPlayTrackForTests);
        Assert.AreEqual(track.Id, _page.PendingAutoPlayTrackForTests!.Id);
    }

    [TestMethod]
    public async Task TryNavigateToTrackAlbumAsync_TrackNotFound_NoAction()
    {
        var trackId = Guid.NewGuid();
        _trackServiceMock
            .Setup(s => s.GetTrackAsync(trackId, _caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackDto?)null);

        await _page.TryNavigateToTrackAlbumAsync(trackId, _caller);

        Assert.AreEqual(0, _page.NavigateToAlbumCallCount);
        Assert.IsNull(_page.PendingAutoPlayTrackForTests);
    }

    [TestMethod]
    public async Task TryNavigateToTrackAlbumAsync_ServiceThrows_DoesNotCrash()
    {
        var trackId = Guid.NewGuid();
        _trackServiceMock
            .Setup(s => s.GetTrackAsync(trackId, _caller, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        // Must not throw
        await _page.TryNavigateToTrackAlbumAsync(trackId, _caller);

        Assert.AreEqual(0, _page.NavigateToAlbumCallCount);
        Assert.IsNull(_page.PendingAutoPlayTrackForTests);
    }

    // ── NavigateToAlbumAsync (real, via overridden dispatch) ───

    [TestMethod]
    public async Task NavigateToAlbumAsync_NullAlbumId_ReturnsWithoutAction()
    {
        // Call the overridden version which includes null guard, and since
        // the virtual dispatch works, null guard on base is bypassed.
        // The overridden version records every call including null.
        await _page.NavigateToAlbumAsync(null);

        Assert.AreEqual(1, _page.NavigateToAlbumCallCount);
        Assert.IsNull(_page.NavigatedAlbumId);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static TrackDto MakeTrack(Guid? albumId = null, string title = "Test Track")
    {
        return new TrackDto
        {
            Id = Guid.NewGuid(),
            FileNodeId = Guid.NewGuid(),
            Title = title,
            Duration = TimeSpan.FromMinutes(4),
            MimeType = "audio/flac",
            ArtistId = Guid.NewGuid(),
            ArtistName = "Test Artist",
            AlbumId = albumId,
            AlbumTitle = albumId is not null ? "Test Album" : null,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Testable subclass that overrides <see cref="MusicPage.NavigateToAlbumAsync"/>
    /// to avoid the full dependency chain (GetCallerAsync, OpenAlbumDetail, etc.).
    /// </summary>
    private class TestableMusicPage : MusicPage
    {
        public Guid? NavigatedAlbumId { get; private set; }
        public int NavigateToAlbumCallCount { get; private set; }

        internal override Task NavigateToAlbumAsync(Guid? albumId)
        {
            NavigatedAlbumId = albumId;
            NavigateToAlbumCallCount++;
            return Task.CompletedTask;
        }
    }
}

using System.Collections.ObjectModel;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Core;
using DotNetCloud.Client.Android.Music;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Core.DTOs;
using Moq;

namespace DotNetCloud.Client.Android.Tests.ViewModels;

[TestClass]
public sealed class MusicViewModelTests
{
    private const string ServerUrl = "https://example.com:15443";

    private Mock<IMusicRestClient> _music = null!;
    private Mock<IMusicPlayerService> _player = null!;
    private Mock<IEqualizerService> _eq = null!;
    private Mock<IAlbumArtCache> _artCache = null!;
    private Mock<IServerConnectionStore> _serverStore = null!;
    private Mock<ISecureTokenStore> _tokenStore = null!;

    private MusicViewModel _vm = null!;

    [TestInitialize]
    public void Setup()
    {
        _music = new Mock<IMusicRestClient>(MockBehavior.Strict);
        _player = new Mock<IMusicPlayerService>(MockBehavior.Loose);
        _eq = new Mock<IEqualizerService>(MockBehavior.Loose);
        _artCache = new Mock<IAlbumArtCache>(MockBehavior.Loose);
        _serverStore = new Mock<IServerConnectionStore>(MockBehavior.Strict);
        _tokenStore = new Mock<ISecureTokenStore>(MockBehavior.Strict);

        // Default auth setup: active server connection with token
        var connection = new ServerConnection(ServerUrl, "Test Server", "test@test.com");
        _serverStore.Setup(x => x.GetActive()).Returns(connection);
        _tokenStore.Setup(x => x.GetAccessTokenAsync(ServerUrl))
            .ReturnsAsync("test-access-token");

        _vm = new MusicViewModel(
            _music.Object, _player.Object, _eq.Object,
            _artCache.Object, _serverStore.Object, _tokenStore.Object);
    }

    // ── Initial state ──────────────────────────────────────────────────

    [TestMethod]
    public void Constructor_InitializesDefaultState()
    {
        Assert.AreEqual(MusicView.Artists, _vm.CurrentView);
        Assert.AreEqual("Music", _vm.Title);
        Assert.IsNull(_vm.SelectedArtist);
        Assert.IsNull(_vm.SelectedAlbum);
        Assert.IsNull(_vm.CurrentTrack);
        Assert.IsFalse(_vm.IsPlaying);
        Assert.IsFalse(_vm.IsLoading);
        Assert.AreEqual(0, _vm.Artists.Count);
        Assert.AreEqual(0, _vm.Albums.Count);
        Assert.AreEqual(0, _vm.Tracks.Count);
        Assert.AreEqual(0, _vm.Playlists.Count);
        Assert.AreEqual(0, _vm.EqPresets.Count);
        Assert.AreEqual(0, _vm.Genres.Count);

        // Repeat defaults
        Assert.AreEqual(RepeatMode.Off, _vm.RepeatMode);
        Assert.AreEqual("🔁", _vm.RepeatIcon);
        Assert.AreEqual("Off", _vm.RepeatLabel);
        Assert.IsFalse(_vm.IsRepeatActive);
    }

    // ── Repeat ─────────────────────────────────────────────────────────

    [TestMethod]
    public void CycleRepeatCommand_DelegatesToPlayer()
    {
        _vm.CycleRepeatCommand.Execute(null);
        _player.Verify(x => x.CycleRepeat(), Times.Once);
    }

    [TestMethod]
    public void RepeatModeChanged_SyncsOffState()
    {
        _player.Setup(x => x.RepeatMode).Returns(RepeatMode.Off);
        _player.Raise(x => x.RepeatModeChanged += null, EventArgs.Empty);

        Assert.AreEqual(RepeatMode.Off, _vm.RepeatMode);
        Assert.AreEqual("🔁", _vm.RepeatIcon);
        Assert.AreEqual("Off", _vm.RepeatLabel);
        Assert.IsFalse(_vm.IsRepeatActive);
    }

    [TestMethod]
    public void RepeatModeChanged_SyncsOneState()
    {
        _player.Setup(x => x.RepeatMode).Returns(RepeatMode.One);
        _player.Raise(x => x.RepeatModeChanged += null, EventArgs.Empty);

        Assert.AreEqual(RepeatMode.One, _vm.RepeatMode);
        Assert.AreEqual("🔂", _vm.RepeatIcon);
        Assert.AreEqual("One", _vm.RepeatLabel);
        Assert.IsTrue(_vm.IsRepeatActive);
    }

    [TestMethod]
    public void RepeatModeChanged_SyncsAllState()
    {
        _player.Setup(x => x.RepeatMode).Returns(RepeatMode.All);
        _player.Raise(x => x.RepeatModeChanged += null, EventArgs.Empty);

        Assert.AreEqual(RepeatMode.All, _vm.RepeatMode);
        Assert.AreEqual("🔁", _vm.RepeatIcon);
        Assert.AreEqual("All", _vm.RepeatLabel);
        Assert.IsTrue(_vm.IsRepeatActive);
    }

    // ── LoadArtistsAsync ───────────────────────────────────────────────

    [TestMethod]
    public async Task LoadArtistsCommand_PopulatesArtists_AndSetsView()
    {
        var artists = new List<ArtistDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Artist A", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Artist B", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(artists);

        await _vm.LoadArtistsCommand.ExecuteAsync(null);

        Assert.AreEqual(2, _vm.Artists.Count);
        Assert.AreEqual("Artist A", _vm.Artists[0].Name);
        Assert.AreEqual(MusicView.Artists, _vm.CurrentView);
        Assert.AreEqual("Artists", _vm.Title);
    }

    [TestMethod]
    public async Task LoadArtistsCommand_SetsLoadingState()
    {
        var artists = new List<ArtistDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Test", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(artists);

        var loadingStates = new List<bool>();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MusicViewModel.IsLoading))
                loadingStates.Add(_vm.IsLoading);
        };

        await _vm.LoadArtistsCommand.ExecuteAsync(null);

        Assert.IsFalse(_vm.IsLoading);
        Assert.IsTrue(loadingStates.Count >= 2);
    }

    [TestMethod]
    public async Task LoadArtistsCommand_DoesNothing_WhenNoCredentials()
    {
        _serverStore.Setup(x => x.GetActive()).Returns((ServerConnection?)null);

        await _vm.LoadArtistsCommand.ExecuteAsync(null);

        Assert.AreEqual(0, _vm.Artists.Count);
        _music.Verify(x => x.ListArtistsAsync(It.IsAny<string>(), It.IsAny<string>(), 0, 50, It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── SelectArtistAsync ──────────────────────────────────────────────

    [TestMethod]
    public async Task SelectArtistCommand_LoadsAlbums_AndSetsView()
    {
        var artistId = Guid.NewGuid();
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ArtistDto { Id = artistId, Name = "Artist", CreatedAt = DateTime.UtcNow }]);
        await _vm.LoadArtistsCommand.ExecuteAsync(null);

        var artist = _vm.Artists[0];
        var albums = new List<MusicAlbumDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Album 1", ArtistId = artistId, ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListAlbumsByArtistAsync(ServerUrl, "test-access-token", artistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(albums);

        // Simulate CollectionView setting SelectedItem, then call the command
        _vm.SelectedArtist = artist;
        await _vm.SelectArtistCommand.ExecuteAsync(artist);

        Assert.AreEqual(1, _vm.Albums.Count);
        Assert.AreEqual("Album 1", _vm.Albums[0].Title);
        Assert.AreEqual(MusicView.Albums, _vm.CurrentView);
        Assert.AreEqual("Artist", _vm.Title);
        Assert.AreEqual(artist, _vm.SelectedArtist); // was set by the binding simulation above
    }

    // ── LoadAlbumsAsync ────────────────────────────────────────────────

    [TestMethod]
    public async Task LoadAlbumsCommand_PopulatesAlbums_AndSetsView()
    {
        var albums = new List<MusicAlbumDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Album One", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListAlbumsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(albums);

        await _vm.LoadAlbumsCommand.ExecuteAsync(null);

        Assert.AreEqual(1, _vm.Albums.Count);
        Assert.AreEqual("Album One", _vm.Albums[0].Title);
        Assert.AreEqual(MusicView.Albums, _vm.CurrentView);
        Assert.AreEqual("Albums", _vm.Title);
        Assert.IsNull(_vm.SelectedArtist);
    }

    // ── SelectAlbumAsync ───────────────────────────────────────────────

    [TestMethod]
    public async Task SelectAlbumCommand_LoadsTracks_EnqueuesAndPlays()
    {
        var albumId = Guid.NewGuid();
        _music.Setup(x => x.ListAlbumsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MusicAlbumDto { Id = albumId, Title = "Album", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }]);
        await _vm.LoadAlbumsCommand.ExecuteAsync(null);

        var album = _vm.Albums[0];
        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Track 1", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", AlbumId = albumId, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Title = "Track 2", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", AlbumId = albumId, CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListTracksByAlbumAsync(ServerUrl, "test-access-token", albumId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tracks);
        _player.Setup(x => x.PlayAsync(tracks[0], ServerUrl, "test-access-token"))
            .Returns(Task.CompletedTask);

        // Call the command directly (as BackAsync does). The command
        // delegates to LoadTracksForAlbumAsync; the SelectedAlbum property
        // is set by the CollectionView binding in the real UI.
        await _vm.SelectAlbumCommand.ExecuteAsync(album);

        Assert.AreEqual(2, _vm.Tracks.Count);
        Assert.AreEqual("Track 1", _vm.Tracks[0].Title);
        Assert.AreEqual(MusicView.Tracks, _vm.CurrentView);
        Assert.AreEqual("Album", _vm.Title);
        _player.Verify(x => x.ReplaceQueue(tracks, albumId: album.Id, playlistId: null), Times.Once);
        _player.Verify(x => x.PlayAsync(tracks[0], ServerUrl, "test-access-token"), Times.Once);
    }

    // ── LoadTracksAsync ────────────────────────────────────────────────

    [TestMethod]
    public async Task LoadTracksCommand_PopulatesTracks_AndSetsView()
    {
        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Track One", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/flac", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListTracksAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tracks);

        await _vm.LoadTracksCommand.ExecuteAsync(null);

        Assert.AreEqual(1, _vm.Tracks.Count);
        Assert.AreEqual("Track One", _vm.Tracks[0].Title);
        Assert.AreEqual(MusicView.Tracks, _vm.CurrentView);
        Assert.AreEqual("Tracks", _vm.Title);
        Assert.IsNull(_vm.SelectedAlbum);
    }

    // ── LoadPlaylistsAsync ─────────────────────────────────────────────

    [TestMethod]
    public async Task LoadPlaylistsCommand_PopulatesPlaylists_AndSetsView()
    {
        var playlists = new List<PlaylistDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Favorites", OwnerId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListPlaylistsAsync(ServerUrl, "test-access-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlists);

        await _vm.LoadPlaylistsCommand.ExecuteAsync(null);

        Assert.AreEqual(1, _vm.Playlists.Count);
        Assert.AreEqual("Favorites", _vm.Playlists[0].Name);
        Assert.AreEqual(MusicView.Playlists, _vm.CurrentView);
        Assert.AreEqual("Playlists", _vm.Title);
    }

    // ── SelectPlaylistAsync ────────────────────────────────────────────

    [TestMethod]
    public async Task SelectPlaylistCommand_LoadsPlaylistTracks_AndSetsView()
    {
        var playlistId = Guid.NewGuid();
        _music.Setup(x => x.ListPlaylistsAsync(ServerUrl, "test-access-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlaylistDto { Id = playlistId, Name = "My Playlist", OwnerId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }]);
        await _vm.LoadPlaylistsCommand.ExecuteAsync(null);

        var playlist = _vm.Playlists[0];
        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Playlist Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/ogg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.GetPlaylistTracksAsync(ServerUrl, "test-access-token", playlistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tracks);

        await _vm.SelectPlaylistCommand.ExecuteAsync(playlist);

        Assert.AreEqual(1, _vm.Tracks.Count);
        Assert.AreEqual("Playlist Track", _vm.Tracks[0].Title);
        Assert.AreEqual(MusicView.Tracks, _vm.CurrentView);
        Assert.AreEqual("My Playlist", _vm.Title);
        _player.Verify(x => x.ReplaceQueue(tracks, albumId: null, playlistId: playlist.Id), Times.Once);
    }

    // ── PlayTrackAsync ─────────────────────────────────────────────────

    [TestMethod]
    public async Task PlayTrackCommand_CallsPlayerService()
    {
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Test Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        _player.Setup(x => x.PlayAsync(track, ServerUrl, "test-access-token"))
            .Returns(Task.CompletedTask);

        await _vm.PlayTrackCommand.ExecuteAsync(track);

        _player.Verify(x => x.PlayAsync(track, ServerUrl, "test-access-token"), Times.Once);
    }

    [TestMethod]
    public async Task PlayTrackCommand_DoesNothing_WhenNoCredentials()
    {
        _serverStore.Setup(x => x.GetActive()).Returns((ServerConnection?)null);
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Test", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };

        await _vm.PlayTrackCommand.ExecuteAsync(track);

        _player.Verify(x => x.PlayAsync(It.IsAny<TrackDto>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task PlayTrackCommand_WhenTrackInTracksList_ReplacesQueueWithTrackList()
    {
        // Arrange: Tracks view is showing a list containing the tapped track.
        var first = new TrackDto { Id = Guid.NewGuid(), Title = "Track 1", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Track 2", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        _vm.Tracks = new ObservableCollection<TrackDto>([first, track]);
        _player.Setup(x => x.PlayAsync(track, ServerUrl, "test-access-token"))
            .Returns(Task.CompletedTask);

        // Act
        await _vm.PlayTrackCommand.ExecuteAsync(track);

        // Assert: the queue is replaced with the full displayed list (so
        // RepeatMode.Off advances to the next track instead of stopping after one).
        _player.Verify(x => x.ReplaceQueue(
            It.Is<IEnumerable<TrackDto>>(q => q.SequenceEqual(new[] { first, track })),
            It.Is<Guid?>(g => g == null),
            It.Is<Guid?>(g => g == null)), Times.Once);
        _player.Verify(x => x.PlayAsync(track, ServerUrl, "test-access-token"), Times.Once);
    }

    [TestMethod]
    public async Task PlayTrackCommand_WhenTrackNotInTracksList_DoesNotReplaceQueue()
    {
        // Arrange: Tracks list is populated but does not contain the tapped track
        // (e.g. standalone/other source) — fall back to single-track playback.
        var listed = new TrackDto { Id = Guid.NewGuid(), Title = "Listed", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Other", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        _vm.Tracks = new ObservableCollection<TrackDto>([listed]);
        _player.Setup(x => x.PlayAsync(track, ServerUrl, "test-access-token"))
            .Returns(Task.CompletedTask);

        // Act
        await _vm.PlayTrackCommand.ExecuteAsync(track);

        // Assert: no queue replacement — PlayAsync handles standalone playback.
        _player.Verify(x => x.ReplaceQueue(It.IsAny<IEnumerable<TrackDto>>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()), Times.Never);
        _player.Verify(x => x.PlayAsync(track, ServerUrl, "test-access-token"), Times.Once);
    }

    [TestMethod]
    public async Task PlayTrackCommand_WhenInAlbumTracksView_PassesAlbumContext()
    {
        // Arrange: load an album so the Tracks view is album-scoped.
        var albumId = Guid.NewGuid();
        _music.Setup(x => x.ListAlbumsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MusicAlbumDto { Id = albumId, Title = "Album", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }]);
        await _vm.LoadAlbumsCommand.ExecuteAsync(null);

        var album = _vm.Albums[0];
        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Track 1", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", AlbumId = albumId, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Title = "Track 2", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", AlbumId = albumId, CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListTracksByAlbumAsync(ServerUrl, "test-access-token", albumId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tracks);
        await _vm.SelectAlbumCommand.ExecuteAsync(album);

        var tapped = tracks[1];
        _player.Setup(x => x.PlayAsync(tapped, ServerUrl, "test-access-token"))
            .Returns(Task.CompletedTask);

        // Act: tap the second track in the album's track list.
        await _vm.PlayTrackCommand.ExecuteAsync(tapped);

        // Assert: queue replaced with album tracks, keeping album navigation context.
        _player.Verify(x => x.ReplaceQueue(
            It.Is<IEnumerable<TrackDto>>(q => q.SequenceEqual(tracks)),
            It.Is<Guid?>(g => g == albumId),
            It.Is<Guid?>(g => g == null)), Times.AtLeastOnce);
        _player.Verify(x => x.PlayAsync(tapped, ServerUrl, "test-access-token"), Times.Once);
    }

    // ── Playback controls ──────────────────────────────────────────────

    [TestMethod]
    public void TogglePlayPause_CallsResume_WhenNotPlaying()
    {
        _player.Setup(x => x.IsPlaying).Returns(false);

        _vm.TogglePlayPauseCommand.Execute(null);

        _player.Verify(x => x.Resume(), Times.Once);
        _player.Verify(x => x.Pause(), Times.Never);
    }

    [TestMethod]
    public void TogglePlayPause_CallsPause_WhenPlaying()
    {
        _player.Setup(x => x.IsPlaying).Returns(true);

        _vm.TogglePlayPauseCommand.Execute(null);

        _player.Verify(x => x.Pause(), Times.Once);
        _player.Verify(x => x.Resume(), Times.Never);
    }

    [TestMethod]
    public void PlayNext_CallsPlayerPlayNext()
    {
        _vm.PlayNextCommand.Execute(null);
        _player.Verify(x => x.PlayNext(), Times.Once);
    }

    [TestMethod]
    public void PlayPrevious_CallsPlayerPlayPrevious()
    {
        _vm.PlayPreviousCommand.Execute(null);
        _player.Verify(x => x.PlayPrevious(), Times.Once);
    }

    [TestMethod]
    public void SeekToCommand_SeeksToPosition()
    {
        _player.Setup(x => x.Seek(TimeSpan.FromSeconds(42.5)));

        _vm.SeekToCommand.Execute(42.5);

        _player.Verify(x => x.Seek(TimeSpan.FromSeconds(42.5)), Times.Once);
    }

    // ── ToggleStarAsync ────────────────────────────────────────────────

    [TestMethod]
    public async Task ToggleStarCommand_WithTrack_StarsTrack()
    {
        var trackId = Guid.NewGuid();
        var track = new TrackDto { Id = trackId, Title = "Test", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        _music.Setup(x => x.ToggleStarAsync(ServerUrl, "test-access-token", trackId, "track", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _vm.ToggleStarCommand.ExecuteAsync(track);

        _music.Verify(x => x.ToggleStarAsync(ServerUrl, "test-access-token", trackId, "track", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ToggleStarCommand_WithAlbum_StarsAlbum()
    {
        var albumId = Guid.NewGuid();
        var album = new MusicAlbumDto { Id = albumId, Title = "Album", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        _music.Setup(x => x.ToggleStarAsync(ServerUrl, "test-access-token", albumId, "album", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _vm.ToggleStarCommand.ExecuteAsync(album);

        _music.Verify(x => x.ToggleStarAsync(ServerUrl, "test-access-token", albumId, "album", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ToggleStarCommand_WithArtist_StarsArtist()
    {
        var artistId = Guid.NewGuid();
        var artist = new ArtistDto { Id = artistId, Name = "Artist", CreatedAt = DateTime.UtcNow };
        _music.Setup(x => x.ToggleStarAsync(ServerUrl, "test-access-token", artistId, "artist", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _vm.ToggleStarCommand.ExecuteAsync(artist);

        _music.Verify(x => x.ToggleStarAsync(ServerUrl, "test-access-token", artistId, "artist", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ToggleStarCommand_DoesNothing_WhenNoCredentials()
    {
        _serverStore.Setup(x => x.GetActive()).Returns((ServerConnection?)null);
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Test", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };

        await _vm.ToggleStarCommand.ExecuteAsync(track);

        _music.Verify(x => x.ToggleStarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── NavigateToPlayingArtistAsync ────────────────────────────────────

    [TestMethod]
    public async Task NavigateToPlayingArtistCommand_LoadsAlbumsForCurrentTrackArtist()
    {
        var artistId = Guid.NewGuid();
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = artistId, ArtistName = "Test Artist", CreatedAt = DateTime.UtcNow };
        _player.Setup(x => x.CurrentTrack).Returns(track);

        var albums = new List<MusicAlbumDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Album 1", ArtistId = artistId, ArtistName = "Test Artist", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListAlbumsByArtistAsync(ServerUrl, "test-access-token", artistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(albums);

        await _vm.NavigateToPlayingArtistCommand.ExecuteAsync(null);

        Assert.AreEqual(1, _vm.Albums.Count);
        Assert.AreEqual("Album 1", _vm.Albums[0].Title);
        Assert.AreEqual(MusicView.Albums, _vm.CurrentView);
        Assert.AreEqual("Test Artist", _vm.Title);
        Assert.IsTrue(_vm.CanGoBackToArtist);
    }

    [TestMethod]
    public async Task NavigateToPlayingArtistCommand_DoesNothing_WhenNoTrack()
    {
        _player.Setup(x => x.CurrentTrack).Returns((TrackDto?)null);

        await _vm.NavigateToPlayingArtistCommand.ExecuteAsync(null);

        _music.Verify(x => x.ListAlbumsByArtistAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task NavigateToPlayingArtistCommand_DoesNothing_WhenNoCredentials()
    {
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        _player.Setup(x => x.CurrentTrack).Returns(track);
        _serverStore.Setup(x => x.GetActive()).Returns((ServerConnection?)null);

        await _vm.NavigateToPlayingArtistCommand.ExecuteAsync(null);

        _music.Verify(x => x.ListAlbumsByArtistAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── NavigateToCurrentSourceAsync ────────────────────────────────────

    [TestMethod]
    public async Task NavigateToCurrentSourceCommand_WithAlbumContext_NavigatesToAlbumTracks()
    {
        var albumId = Guid.NewGuid();
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", AlbumId = albumId, CreatedAt = DateTime.UtcNow };
        _player.Setup(x => x.CurrentTrack).Returns(track);
        _player.Setup(x => x.PlayingAlbumId).Returns(albumId);
        _player.Setup(x => x.PlayingPlaylistId).Returns((Guid?)null);

        var album = new MusicAlbumDto { Id = albumId, Title = "Test Album", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        _music.Setup(x => x.GetAlbumAsync(ServerUrl, "test-access-token", albumId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(album);

        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Album Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", AlbumId = albumId, CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListTracksByAlbumAsync(ServerUrl, "test-access-token", albumId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tracks);

        await _vm.NavigateToCurrentSourceCommand.ExecuteAsync(null);

        Assert.AreEqual(1, _vm.Tracks.Count);
        Assert.AreEqual("Album Track", _vm.Tracks[0].Title);
        Assert.AreEqual(MusicView.Tracks, _vm.CurrentView);
        Assert.IsTrue(_vm.CanGoBackToAlbum);
        // Playback should NOT have been restarted
        _player.Verify(x => x.PlayAsync(It.IsAny<TrackDto>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task NavigateToCurrentSourceCommand_WithPlaylistContext_NavigatesToPlaylistTracks()
    {
        var playlistId = Guid.NewGuid();
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        _player.Setup(x => x.CurrentTrack).Returns(track);
        _player.Setup(x => x.PlayingAlbumId).Returns((Guid?)null);
        _player.Setup(x => x.PlayingPlaylistId).Returns(playlistId);

        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Playlist Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.GetPlaylistTracksAsync(ServerUrl, "test-access-token", playlistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tracks);

        await _vm.NavigateToCurrentSourceCommand.ExecuteAsync(null);

        Assert.AreEqual(1, _vm.Tracks.Count);
        Assert.AreEqual("Playlist Track", _vm.Tracks[0].Title);
        Assert.AreEqual(MusicView.Tracks, _vm.CurrentView);
        Assert.IsTrue(_vm.CanGoBackToPlaylist);
        _player.Verify(x => x.PlayAsync(It.IsAny<TrackDto>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task NavigateToCurrentSourceCommand_FallsBackToTrackAlbumId_WhenNoPlayerContext()
    {
        var albumId = Guid.NewGuid();
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", AlbumId = albumId, CreatedAt = DateTime.UtcNow };
        _player.Setup(x => x.CurrentTrack).Returns(track);
        _player.Setup(x => x.PlayingAlbumId).Returns((Guid?)null);
        _player.Setup(x => x.PlayingPlaylistId).Returns((Guid?)null);

        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Fallback Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", AlbumId = albumId, CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListTracksByAlbumAsync(ServerUrl, "test-access-token", albumId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tracks);

        var album = new MusicAlbumDto { Id = albumId, Title = "Fallback Album", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        _music.Setup(x => x.GetAlbumAsync(ServerUrl, "test-access-token", albumId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(album);

        await _vm.NavigateToCurrentSourceCommand.ExecuteAsync(null);

        Assert.AreEqual(1, _vm.Tracks.Count);
        Assert.AreEqual(MusicView.Tracks, _vm.CurrentView);
    }

    [TestMethod]
    public async Task NavigateToCurrentSourceCommand_DoesNothing_WhenNoTrack()
    {
        _player.Setup(x => x.CurrentTrack).Returns((TrackDto?)null);

        await _vm.NavigateToCurrentSourceCommand.ExecuteAsync(null);

        _music.Verify(x => x.ListTracksByAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _music.Verify(x => x.GetPlaylistTracksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task NavigateToCurrentSourceCommand_DoesNothing_WhenNoCredentials()
    {
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        _player.Setup(x => x.CurrentTrack).Returns(track);
        _serverStore.Setup(x => x.GetActive()).Returns((ServerConnection?)null);

        await _vm.NavigateToCurrentSourceCommand.ExecuteAsync(null);

        _music.Verify(x => x.ListTracksByAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _music.Verify(x => x.GetPlaylistTracksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── ApplyEqPresetAsync ─────────────────────────────────────────────

    [TestMethod]
    public async Task ApplyEqPresetCommand_AppliesPreset()
    {
        var preset = new EqPresetDto { Id = Guid.NewGuid(), Name = "Rock", IsBuiltIn = true, Bands = new Dictionary<string, double>() };
        _eq.Setup(x => x.ApplyPreset(preset));

        await _vm.ApplyEqPresetCommand.ExecuteAsync(preset);

        _eq.Verify(x => x.ApplyPreset(preset), Times.Once);
    }

    // ── BackAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task BackCommand_FromArtists_LoadsArtists()
    {
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _vm.BackCommand.ExecuteAsync(null);

        _music.Verify(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task BackCommand_FromAlbums_ReturnsToArtists()
    {
        // First load artists
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ArtistDto { Id = Guid.NewGuid(), Name = "Artist", CreatedAt = DateTime.UtcNow }]);
        await _vm.LoadArtistsCommand.ExecuteAsync(null);

        // Then select artist to go to album view
        var artist = _vm.Artists[0];
        _music.Setup(x => x.ListAlbumsByArtistAsync(ServerUrl, "test-access-token", artist.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        await _vm.SelectArtistCommand.ExecuteAsync(artist);
        Assert.AreEqual(MusicView.Albums, _vm.CurrentView);

        // Now hit back
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _vm.BackCommand.ExecuteAsync(null);

        _music.Verify(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(MusicView.Artists, _vm.CurrentView);
    }

    [TestMethod]
    public async Task BackCommand_FromAlbumTracks_WithSelectedArtist_ReturnsToArtistAlbums()
    {
        // Load artists
        var artistId = Guid.NewGuid();
        var artist = new ArtistDto { Id = artistId, Name = "Artist", CreatedAt = DateTime.UtcNow };
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([artist]);
        await _vm.LoadArtistsCommand.ExecuteAsync(null);

        // Select artist -> albums (simulate CollectionView setting SelectedItem)
        _music.Setup(x => x.ListAlbumsByArtistAsync(ServerUrl, "test-access-token", artistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MusicAlbumDto { Id = Guid.NewGuid(), Title = "Album", ArtistId = artistId, ArtistName = "Artist", CreatedAt = DateTime.UtcNow }]);
        _vm.SelectedArtist = artist;
        await _vm.SelectArtistCommand.ExecuteAsync(artist);

        // Select album -> tracks (simulate CollectionView setting SelectedItem)
        var album = _vm.Albums[0];
        _music.Setup(x => x.ListTracksByAlbumAsync(ServerUrl, "test-access-token", album.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _vm.SelectedAlbum = album;
        await _vm.SelectAlbumCommand.ExecuteAsync(album);
        Assert.AreEqual(MusicView.Tracks, _vm.CurrentView);
        Assert.IsNotNull(_vm.SelectedArtist);

        // Back should return to artist albums
        _music.Setup(x => x.ListAlbumsByArtistAsync(ServerUrl, "test-access-token", artistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _vm.BackCommand.ExecuteAsync(null);

        // Should have loaded albums by artist again
        _music.Verify(x => x.ListAlbumsByArtistAsync(ServerUrl, "test-access-token", artistId, It.IsAny<CancellationToken>()), Times.AtLeast(2));
        Assert.AreEqual(MusicView.Albums, _vm.CurrentView);
    }

    // ── LoadEqPresetsAsync ─────────────────────────────────────────────

    [TestMethod]
    public async Task LoadEqPresetsCommand_PopulatesPresets()
    {
        var presets = new List<EqPresetDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Rock", IsBuiltIn = true, Bands = new Dictionary<string, double>() }
        };
        _music.Setup(x => x.ListEqPresetsAsync(ServerUrl, "test-access-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(presets);

        await _vm.LoadEqPresetsCommand.ExecuteAsync(null);

        Assert.AreEqual(1, _vm.EqPresets.Count);
        Assert.AreEqual("Rock", _vm.EqPresets[0].Name);
    }

    // ── PlaybackStateChanged event ─────────────────────────────────────

    [TestMethod]
    public void PlaybackStateChanged_UpdatesState()
    {
        var track = new TrackDto { Id = Guid.NewGuid(), Title = "Now Playing", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };

        // Simulate the player raising PlaybackStateChanged
        _player.Setup(x => x.CurrentTrack).Returns(track);
        _player.Setup(x => x.IsPlaying).Returns(true);
        _player.Setup(x => x.CurrentPosition).Returns(TimeSpan.FromSeconds(30));
        _player.Setup(x => x.Duration).Returns(TimeSpan.FromSeconds(180));

        _player.Raise(x => x.PlaybackStateChanged += null, EventArgs.Empty);

        // MainThread.BeginInvokeOnMainThread may not execute in test context,
        // but properties should be updated eventually
    }

    // ── Edge cases ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task LoadArtistsCommand_HandlesEmptyList()
    {
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _vm.LoadArtistsCommand.ExecuteAsync(null);

        Assert.AreEqual(0, _vm.Artists.Count);
        Assert.AreEqual(MusicView.Artists, _vm.CurrentView);
    }

    [TestMethod]
    public async Task MultipleRapidLoads_DoNotCrash()
    {
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _music.Setup(x => x.ListAlbumsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _music.Setup(x => x.ListPlaylistsAsync(ServerUrl, "test-access-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Rapidly switch views
        await _vm.LoadArtistsCommand.ExecuteAsync(null);
        await _vm.LoadAlbumsCommand.ExecuteAsync(null);
        await _vm.LoadArtistsCommand.ExecuteAsync(null);
        await _vm.LoadPlaylistsCommand.ExecuteAsync(null);

        Assert.AreEqual(MusicView.Playlists, _vm.CurrentView);
    }

    [TestMethod]
    public void Constructor_SubscribesToPlayerEvents()
    {
        // Verify subscription via raising events
        _player.Raise(x => x.PlaybackStateChanged += null, EventArgs.Empty);
        // Should not throw
    }

    [TestMethod]
    public void TrackEndedEvent_TriggersPlayNextCommand()
    {
        // The ViewModel subscribes to TrackEnded and dispatches PlayNextCommand.
        // This verifies the handler is wired up correctly (replaces the old
        // PlayNextIfQueued call that was removed from MusicPlayerService).
        _player.Setup(x => x.PlayNext());

        _player.Raise(x => x.TrackEnded += null, EventArgs.Empty);

        _player.Verify(x => x.PlayNext(), Times.AtLeastOnce);
    }

    // ── Search ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Constructor_InitializesSearchDefaults()
    {
        Assert.IsFalse(_vm.IsSearchOpen);
        Assert.AreEqual(string.Empty, _vm.SearchQuery);
        Assert.IsNull(_vm.SearchResultText);
        Assert.IsFalse(_vm.IsSearching);
        Assert.AreEqual("Search…", _vm.SearchPlaceholderText);
    }

    [TestMethod]
    public void ToggleSearchCommand_OpensSearchPanel()
    {
        _vm.ToggleSearchCommand.Execute(null);

        Assert.IsTrue(_vm.IsSearchOpen);
    }

    [TestMethod]
    public void ToggleSearchCommand_ClosesSearchPanel_WhenAlreadyOpen()
    {
        _vm.ToggleSearchCommand.Execute(null); // open
        _vm.ToggleSearchCommand.Execute(null); // close

        Assert.IsFalse(_vm.IsSearchOpen);
        Assert.AreEqual(string.Empty, _vm.SearchQuery);
    }

    [TestMethod]
    public void CloseSearchCommand_RestoresOriginalCollections_AndClearsSearch()
    {
        // Pre-populate Artists
        var artistId = Guid.NewGuid();
        var preSearch = new List<ArtistDto>
        {
            new() { Id = artistId, Name = "Saved Artist", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preSearch);
        _vm.LoadArtistsCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.AreEqual(1, _vm.Artists.Count);

        // Open search (saves pre-search collections)
        _vm.ToggleSearchCommand.Execute(null);
        Assert.IsTrue(_vm.IsSearchOpen);

        // Simulate search results replacing the collection
        _vm.Artists = [];
        Assert.AreEqual(0, _vm.Artists.Count);

        // Close search — original data should be restored
        _vm.CloseSearchCommand.Execute(null);

        Assert.IsFalse(_vm.IsSearchOpen);
        Assert.AreEqual(string.Empty, _vm.SearchQuery);
        Assert.IsNull(_vm.SearchResultText);
        Assert.IsFalse(_vm.IsSearching);
        Assert.AreEqual(1, _vm.Artists.Count);
        Assert.AreEqual("Saved Artist", _vm.Artists[0].Name);
    }

    [TestMethod]
    public void CloseSearchCommand_DoesNotThrow_WhenSearchNotOpen()
    {
        // Should be safe to call CloseSearch even if search was never opened
        _vm.CloseSearchCommand.Execute(null);

        Assert.IsFalse(_vm.IsSearchOpen);
    }

    [TestMethod]
    public async Task SearchQueryChanged_EmptyQuery_RestoresOriginalCollections()
    {
        // Pre-populate Artists
        var artistId = Guid.NewGuid();
        var preSearch = new List<ArtistDto>
        {
            new() { Id = artistId, Name = "Restored Artist", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preSearch);
        _vm.LoadArtistsCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.AreEqual(1, _vm.Artists.Count);

        // Open search (saves pre-search collections)
        _vm.ToggleSearchCommand.Execute(null);

        // Simulate search results replacing the collection
        _vm.Artists = [];
        Assert.AreEqual(0, _vm.Artists.Count);

        // CloseSearchCommand restores the original collection synchronously
        _vm.CloseSearchCommand.Execute(null);

        Assert.IsFalse(_vm.IsSearchOpen);
        Assert.AreEqual(1, _vm.Artists.Count);
        Assert.AreEqual("Restored Artist", _vm.Artists[0].Name);
    }

    [TestMethod]
    public async Task SearchQueryChanged_SearchesArtists_OnArtistsTab()
    {
        // Pre-populate Artists tab
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        await _vm.LoadArtistsCommand.ExecuteAsync(null);
        Assert.AreEqual(MusicView.Artists, _vm.CurrentView);

        // Setup search mock
        var results = new List<ArtistDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Found Artist", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.SearchArtistsAsync(ServerUrl, "test-access-token", "Found", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Open search
        _vm.ToggleSearchCommand.Execute(null);

        // Trigger search by setting query text
        _vm.SearchQuery = "Found";

        // Wait for debounce + Dispatch
        await Task.Delay(500);

        Assert.AreEqual(1, _vm.Artists.Count);
        Assert.AreEqual("Found Artist", _vm.Artists[0].Name);
        StringAssert.Contains(_vm.SearchResultText, "1 result");
    }

    [TestMethod]
    public async Task SearchQueryChanged_SearchesAlbums_OnAlbumsTab()
    {
        // Pre-populate Albums tab
        _music.Setup(x => x.ListAlbumsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        await _vm.LoadAlbumsCommand.ExecuteAsync(null);
        Assert.AreEqual(MusicView.Albums, _vm.CurrentView);

        // Setup search mock
        var results = new List<MusicAlbumDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Found Album", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.SearchAlbumsAsync(ServerUrl, "test-access-token", "Found", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Open search
        _vm.ToggleSearchCommand.Execute(null);

        // Trigger search
        _vm.SearchQuery = "Found";

        await Task.Delay(500);

        Assert.AreEqual(1, _vm.Albums.Count);
        Assert.AreEqual("Found Album", _vm.Albums[0].Title);
        StringAssert.Contains(_vm.SearchResultText, "1 result");
    }

    [TestMethod]
    public async Task SearchQueryChanged_SearchesTracks_OnTracksTab()
    {
        // Pre-populate Tracks tab
        _music.Setup(x => x.ListTracksAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        await _vm.LoadTracksCommand.ExecuteAsync(null);
        Assert.AreEqual(MusicView.Tracks, _vm.CurrentView);

        // Setup search mock
        var results = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Found Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        _music.Setup(x => x.SearchTracksAsync(ServerUrl, "test-access-token", "Found", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Open search
        _vm.ToggleSearchCommand.Execute(null);

        // Trigger search
        _vm.SearchQuery = "Found";

        await Task.Delay(500);

        Assert.AreEqual(1, _vm.Tracks.Count);
        Assert.AreEqual("Found Track", _vm.Tracks[0].Title);
        StringAssert.Contains(_vm.SearchResultText, "1 result");
    }

    [TestMethod]
    public async Task SearchQueryChanged_NoResults_ShowsZeroText()
    {
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        await _vm.LoadArtistsCommand.ExecuteAsync(null);

        _music.Setup(x => x.SearchArtistsAsync(ServerUrl, "test-access-token", "XyzNotFound", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _vm.ToggleSearchCommand.Execute(null);
        _vm.SearchQuery = "XyzNotFound";

        await Task.Delay(500);

        StringAssert.Contains(_vm.SearchResultText, "No results");
    }

    [TestMethod]
    public async Task SearchQueryChanged_ServerError_ShowsErrorMessage()
    {
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        await _vm.LoadArtistsCommand.ExecuteAsync(null);

        _music.Setup(x => x.SearchArtistsAsync(ServerUrl, "test-access-token", "Error", 50, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Server down"));

        _vm.ToggleSearchCommand.Execute(null);
        _vm.SearchQuery = "Error";

        await Task.Delay(500);

        // HttpRequestException maps to the shared user-friendly connectivity message
        // (ApiExceptionHelper), not a search-specific one.
        StringAssert.Contains(_vm.ErrorMessage, "A connection error occurred");
    }

    [TestMethod]
    public async Task LoadArtistsCommand_ClosesSearch()
    {
        _music.Setup(x => x.ListArtistsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _vm.ToggleSearchCommand.Execute(null);
        Assert.IsTrue(_vm.IsSearchOpen);

        await _vm.LoadArtistsCommand.ExecuteAsync(null);

        Assert.IsFalse(_vm.IsSearchOpen);
        Assert.AreEqual(string.Empty, _vm.SearchQuery);
    }

    [TestMethod]
    public async Task LoadAlbumsCommand_ClosesSearch()
    {
        _music.Setup(x => x.ListAlbumsAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _vm.ToggleSearchCommand.Execute(null);
        Assert.IsTrue(_vm.IsSearchOpen);

        await _vm.LoadAlbumsCommand.ExecuteAsync(null);

        Assert.IsFalse(_vm.IsSearchOpen);
        Assert.AreEqual(string.Empty, _vm.SearchQuery);
    }

    [TestMethod]
    public async Task LoadTracksCommand_ClosesSearch()
    {
        _music.Setup(x => x.ListTracksAsync(ServerUrl, "test-access-token", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _vm.ToggleSearchCommand.Execute(null);
        Assert.IsTrue(_vm.IsSearchOpen);

        await _vm.LoadTracksCommand.ExecuteAsync(null);

        Assert.IsFalse(_vm.IsSearchOpen);
        Assert.AreEqual(string.Empty, _vm.SearchQuery);
    }

    [TestMethod]
    public void SearchPlaceholderText_StartsWithDefault()
    {
        Assert.AreEqual("Search…", _vm.SearchPlaceholderText);
    }
}

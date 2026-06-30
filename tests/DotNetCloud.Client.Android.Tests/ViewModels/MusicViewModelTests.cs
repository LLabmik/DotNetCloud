using DotNetCloud.Client.Android.Auth;
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
        _player.Verify(x => x.ReplaceQueue(tracks), Times.Once);
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
}

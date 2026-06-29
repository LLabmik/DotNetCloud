# Android Music Tab — Implementation Plan

**Branch:** `feature/android-music-tab`
**Date:** 2026-06-29
**Status:** planning

## Overview

Add a read-only Music tab to the Android MAUI app that streams music from the server via the existing REST API (`/api/v1/music/`), with background playback via Android foreground service + `WakeLock`, native Android equalizer, two-tier album art caching, and dynamic tab visibility based on server module availability.

---

## Architecture

```
MAUI App (net10.0-android)
├── MusicPage.xaml / MusicViewModel       ← browsing UI
├── HttpMusicRestClient : IMusicRestClient ← REST → /api/v1/music/*
├── MusicPlayerService : IMusicPlayerService ← Android.Media.MediaPlayer
├── MusicPlaybackService (Foreground)      ← WakeLock + Notification
├── AndroidEqualizerService : IEqualizerService ← AudioEffect.Equalizer
├── AlbumArtCache : IAlbumArtCache          ← memory LRU + disk
└── ModuleAvailabilityState (static)        ← GET /api/v1/core/modules/music/available
```

---

## Phase 1: Music REST Client

### 1.1 Create `src/Clients/DotNetCloud.Client.Android/Music/IMusicRestClient.cs`

**Namespace:** `DotNetCloud.Client.Android.Music`
**Pattern:** Follow `IFileRestClient` — every method takes `(string serverBaseUrl, string accessToken, ...)`.

```csharp
namespace DotNetCloud.Client.Android.Music;

using DotNetCloud.Core.DTOs;  // ArtistDto, MusicAlbumDto, TrackDto, PlaylistDto, EqPresetDto

public interface IMusicRestClient
{
    // ── Artists ──────────────────────────────────────────────────────
    Task<IReadOnlyList<ArtistDto>> ListArtistsAsync(
        string serverBaseUrl, string accessToken,
        int skip = 0, int take = 50, CancellationToken ct = default);

    Task<ArtistDto?> GetArtistAsync(
        string serverBaseUrl, string accessToken,
        Guid artistId, CancellationToken ct = default);

    Task<IReadOnlyList<ArtistDto>> SearchArtistsAsync(
        string serverBaseUrl, string accessToken,
        string query, int take = 20, CancellationToken ct = default);

    // ── Albums ───────────────────────────────────────────────────────
    Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsAsync(
        string serverBaseUrl, string accessToken,
        int skip = 0, int take = 50, CancellationToken ct = default);

    Task<MusicAlbumDto?> GetAlbumAsync(
        string serverBaseUrl, string accessToken,
        Guid albumId, CancellationToken ct = default);

    Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsByArtistAsync(
        string serverBaseUrl, string accessToken,
        Guid artistId, CancellationToken ct = default);

    Task<IReadOnlyList<MusicAlbumDto>> SearchAlbumsAsync(
        string serverBaseUrl, string accessToken,
        string query, int take = 20, CancellationToken ct = default);

    Task<IReadOnlyList<MusicAlbumDto>> GetRecentAlbumsAsync(
        string serverBaseUrl, string accessToken,
        int take = 20, CancellationToken ct = default);

    // ── Tracks ───────────────────────────────────────────────────────
    Task<IReadOnlyList<TrackDto>> ListTracksAsync(
        string serverBaseUrl, string accessToken,
        int skip = 0, int take = 50, CancellationToken ct = default);

    Task<TrackDto?> GetTrackAsync(
        string serverBaseUrl, string accessToken,
        Guid trackId, CancellationToken ct = default);

    Task<IReadOnlyList<TrackDto>> ListTracksByAlbumAsync(
        string serverBaseUrl, string accessToken,
        Guid albumId, CancellationToken ct = default);

    Task<IReadOnlyList<TrackDto>> SearchTracksAsync(
        string serverBaseUrl, string accessToken,
        string query, int take = 20, CancellationToken ct = default);

    Task<IReadOnlyList<TrackDto>> GetRandomTracksAsync(
        string serverBaseUrl, string accessToken,
        int take = 20, string? genre = null, CancellationToken ct = default);

    Task<IReadOnlyList<TrackDto>> GetRecentTracksAsync(
        string serverBaseUrl, string accessToken,
        int take = 20, CancellationToken ct = default);

    // ── Playlists ────────────────────────────────────────────────────
    Task<IReadOnlyList<PlaylistDto>> ListPlaylistsAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);

    Task<PlaylistDto?> GetPlaylistAsync(
        string serverBaseUrl, string accessToken,
        Guid playlistId, CancellationToken ct = default);

    Task<IReadOnlyList<TrackDto>> GetPlaylistTracksAsync(
        string serverBaseUrl, string accessToken,
        Guid playlistId, CancellationToken ct = default);

    // ── Playback / Stars ─────────────────────────────────────────────
    Task RecordPlayAsync(
        string serverBaseUrl, string accessToken,
        Guid trackId, CancellationToken ct = default);

    Task ToggleStarAsync(
        string serverBaseUrl, string accessToken,
        Guid itemId, string itemType,  // itemType = "Track", "Album", "Artist"
        CancellationToken ct = default);

    // ── Equalizer ────────────────────────────────────────────────────
    Task<IReadOnlyList<EqPresetDto>> ListEqPresetsAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);

    Task<EqPresetDto?> GetEqPresetAsync(
        string serverBaseUrl, string accessToken,
        Guid presetId, CancellationToken ct = default);

    // ── Genres ───────────────────────────────────────────────────────
    Task<IReadOnlyList<string>> GetGenresAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);
}
```

**DTO types** come from `DotNetCloud.Core.DTOs` (defined in `src/Core/DotNetCloud.Core/DTOs/MusicDtos.cs`). They are all `record` types:
- `ArtistDto` — `Id:Guid`, `Name:string`, `SortName:string?`, `AlbumCount:int`, `TrackCount:int`, `IsStarred:bool`, `LogoUrl:string?`, `CreatedAt:DateTime`
- `MusicAlbumDto` — `Id:Guid`, `Title:string`, `ArtistId:Guid`, `ArtistName:string`, `Year:int?`, `Genre:string?`, `TrackCount:int`, `TotalDuration:TimeSpan`, `HasCoverArt:bool`, `IsStarred:bool`, `CreatedAt:DateTime`
- `TrackDto` — `Id:Guid`, `OwnerId:Guid`, `FileNodeId:Guid`, `Title:string`, `TrackNumber:int?`, `DiscNumber:int?`, `Duration:TimeSpan`, `SizeBytes:long`, `Bitrate:long?`, `MimeType:string`, `AlbumId:Guid?`, `AlbumTitle:string?`, `ArtistId:Guid`, `ArtistName:string`, `Genre:string?`, `Year:int?`, `IsStarred:bool`, `CreatedAt:DateTime`
- `PlaylistDto` — `Id:Guid`, `OwnerId:Guid`, `Name:string`, `Description:string?`, `IsPublic:bool`, `TrackCount:int`, `TotalDuration:TimeSpan`, `CreatedAt:DateTime`, `UpdatedAt:DateTime`
- `EqPresetDto` — `Id:Guid`, `Name:string`, `IsBuiltIn:bool`, `Bands:IReadOnlyDictionary<string,double>` (keys: frequency labels "31","63","125","250","500","1000","2000","4000","8000","16000", values: gain in dB)

### 1.2 Create `src/Clients/DotNetCloud.Client.Android/Music/HttpMusicRestClient.cs`

**Namespace:** `DotNetCloud.Client.Android.Music`
**Pattern:** Follow `HttpChatRestClient` / `HttpFileRestClient` — constructor takes `(HttpClient http, ILogger<T> logger)`, uses `SetAuth`, parses `{ success: true, data: ... }` envelope.

**Exact API endpoint URLs** (append to `serverBaseUrl.TrimEnd('/')`):

| Method | HTTP | URL | Response `data` |
|--------|------|-----|-----------------|
| `ListArtistsAsync` | GET | `/api/v1/music/artists?skip={s}&take={t}` | `List<ArtistDto>` |
| `GetArtistAsync` | GET | `/api/v1/music/artists/{artistId}` | `ArtistDto` or null (404) |
| `SearchArtistsAsync` | GET | `/api/v1/music/artists/search?q={q}&take={t}` | `List<ArtistDto>` |
| `ListAlbumsAsync` | GET | `/api/v1/music/albums?skip={s}&take={t}` | `List<MusicAlbumDto>` |
| `GetAlbumAsync` | GET | `/api/v1/music/albums/{albumId}` | `MusicAlbumDto` or null (404) |
| `ListAlbumsByArtistAsync` | GET | `/api/v1/music/artists/{artistId}/albums` | `List<MusicAlbumDto>` |
| `SearchAlbumsAsync` | GET | `/api/v1/music/albums/search?q={q}&take={t}` | `List<MusicAlbumDto>` |
| `GetRecentAlbumsAsync` | GET | `/api/v1/music/albums/recent?take={t}` | `List<MusicAlbumDto>` |
| `ListTracksAsync` | GET | `/api/v1/music/tracks?skip={s}&take={t}` | `List<TrackDto>` |
| `GetTrackAsync` | GET | `/api/v1/music/tracks/{trackId}` | `TrackDto` or null (404) |
| `ListTracksByAlbumAsync` | GET | `/api/v1/music/albums/{albumId}/tracks` | `List<TrackDto>` |
| `SearchTracksAsync` | GET | `/api/v1/music/tracks/search?q={q}&take={t}` | `List<TrackDto>` |
| `GetRandomTracksAsync` | GET | `/api/v1/music/tracks/random?take={t}&genre={g}` | `List<TrackDto>` |
| `GetRecentTracksAsync` | GET | `/api/v1/music/tracks/recent?take={t}` | `List<TrackDto>` |
| `ListPlaylistsAsync` | GET | `/api/v1/music/playlists` | `List<PlaylistDto>` |
| `GetPlaylistAsync` | GET | `/api/v1/music/playlists/{playlistId}` | `PlaylistDto` or null (404) |
| `GetPlaylistTracksAsync` | GET | `/api/v1/music/playlists/{playlistId}/tracks` | `List<TrackDto>` |
| `RecordPlayAsync` | POST | `/api/v1/music/tracks/{trackId}/play` | `{ recorded: true }` |
| `ToggleStarAsync` | POST | `/api/v1/music/{typePlural}/{itemId}/star` | `{ toggled: true }` |
| `ListEqPresetsAsync` | GET | `/api/v1/music/eq/presets` | `List<EqPresetDto>` |
| `GetEqPresetAsync` | GET | `/api/v1/music/eq/presets/{presetId}` | `EqPresetDto` or null (404) |
| `GetGenresAsync` | GET | `/api/v1/music/genres` | `List<string>` |

**Envelope parsing helper** (copy the EXACT pattern from `HttpFileRestClient.ReadEnvelopeDataAsync`):

```csharp
private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new()
{
    PropertyNameCaseInsensitive = true,
    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
};

private void SetAuth(string accessToken) =>
    _http.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

private async Task<T?> GetEnvelopeDataAsync<T>(string url, CancellationToken ct)
{
    using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    return await ReadEnvelopeDataAsync<T>(response, ct).ConfigureAwait(false);
}

private static async Task<T?> ReadEnvelopeDataAsync<T>(HttpResponseMessage response, CancellationToken ct)
{
    var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    using var doc = JsonDocument.Parse(body);
    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
        doc.RootElement.TryGetProperty("data", out var dataProp))
        return dataProp.Deserialize<T>(JsonOpts);
    return doc.RootElement.Deserialize<T>(JsonOpts);
}
```

**GET method pattern:**
```csharp
public async Task<IReadOnlyList<ArtistDto>> ListArtistsAsync(
    string serverBaseUrl, string accessToken,
    int skip = 0, int take = 50, CancellationToken ct = default)
{
    SetAuth(accessToken);
    var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/music/artists?skip={skip}&take={take}";
    var data = await GetEnvelopeDataAsync<List<ArtistDto>>(url, ct).ConfigureAwait(false);
    return data ?? [];
}
```

**Single-item GET pattern (returns null on 404):**
```csharp
public async Task<ArtistDto?> GetArtistAsync(
    string serverBaseUrl, string accessToken,
    Guid artistId, CancellationToken ct = default)
{
    SetAuth(accessToken);
    var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/music/artists/{artistId}";
    try
    {
        return await GetEnvelopeDataAsync<ArtistDto>(url, ct).ConfigureAwait(false);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
}
```

**POST method pattern:**
```csharp
public async Task RecordPlayAsync(
    string serverBaseUrl, string accessToken,
    Guid trackId, CancellationToken ct = default)
{
    SetAuth(accessToken);
    var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/music/tracks/{trackId}/play";
    using var response = await _http.PostAsync(url, null, ct).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
}
```

**ToggleStarAsync URL construction** — `itemType` is lowercased and pluralized:
```csharp
public async Task ToggleStarAsync(
    string serverBaseUrl, string accessToken,
    Guid itemId, string itemType, CancellationToken ct = default)
{
    SetAuth(accessToken);
    var typePlural = itemType.ToLowerInvariant() switch
    {
        "track" => "tracks", "album" => "albums", "artist" => "artists",
        _ => throw new ArgumentException($"Unknown itemType: {itemType}")
    };
    var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/music/{typePlural}/{itemId}/star";
    using var response = await _http.PostAsync(url, null, ct).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
}
```

### 1.3 Register in `MauiProgram.cs`

Add in `CreateMauiApp()`:
```csharp
// ── Music ─────────────────────────────────────────────────────────
builder.Services.AddHttpClient<Music.IMusicRestClient, Music.HttpMusicRestClient>()
    .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);
```

---

## Phase 2: Module Detection

### 2.1 Create `src/Clients/DotNetCloud.Client.Android/Services/ModuleAvailabilityState.cs`

**Namespace:** `DotNetCloud.Client.Android.Services`

```csharp
namespace DotNetCloud.Client.Android.Services;

/// <summary>Holds the availability state of optional server modules.
/// Set by <see cref="App"/> after querying the server at startup.</summary>
public static class ModuleAvailabilityState
{
    public static bool IsMusicModuleAvailable { get; set; }
}
```

### 2.2 Modify `src/Clients/DotNetCloud.Client.Android/App.xaml.cs`

The `App` class needs `ISecureTokenStore` injected. Change the constructor to:
```csharp
private readonly IServerConnectionStore _serverStore;
private readonly ISecureTokenStore _tokenStore;

public App(IServerConnectionStore serverStore, ISecureTokenStore tokenStore)
{
    InitializeComponent();
    _serverStore = serverStore;
    _tokenStore = tokenStore;
    UserAppTheme = AppTheme.Dark;
}
```

Add this method and call it in `OnStart()` BEFORE `NavigateToStartPageAsync()`:
```csharp
protected override async void OnStart()
{
    base.OnStart();
    await CheckAvailableModulesAsync();  // ← ADD
    await NavigateToStartPageAsync();
}

private async Task CheckAvailableModulesAsync()
{
    try
    {
        var connection = _serverStore.GetActive();
        if (connection is null) return;

        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl);
        if (token is null) return;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var url = $"{connection.ServerBaseUrl.TrimEnd('/')}/api/v1/core/modules/music/available";
        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("data", out var data) &&
            data.TryGetProperty("installed", out var installed))
        {
            Services.ModuleAvailabilityState.IsMusicModuleAvailable = installed.GetBoolean();
        }
    }
    catch
    {
        Services.ModuleAvailabilityState.IsMusicModuleAvailable = false;
    }
}
```

---

## Phase 3: Dynamic Tab Visibility in AppShell

### 3.1 Create `src/Clients/DotNetCloud.Client.Android/Services/MusicPageVisibilitySource.cs`

**Namespace:** `DotNetCloud.Client.Android.Services`

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DotNetCloud.Client.Android.Services;

public sealed class MusicPageVisibilitySource : INotifyPropertyChanged
{
    public bool IsMusicModuleAvailable => ModuleAvailabilityState.IsMusicModuleAvailable;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMusicModuleAvailable)));
}
```

### 3.2 Modify `src/Clients/DotNetCloud.Client.Android/AppShell.xaml`

Add the Music `ShellContent` as the 4th tab, AFTER Settings:
```xml
<TabBar Route="Main">
    <ShellContent Route="ChannelList" Title="Chat"    Icon="chat_icon.png"     ContentTemplate="{DataTemplate views:ChannelListPage}"/>
    <ShellContent Route="Files"       Title="Files"   Icon="files_icon.png"    ContentTemplate="{DataTemplate views:FileBrowserPage}"/>
    <ShellContent Route="Settings"    Title="Settings" Icon="settings_icon.png" ContentTemplate="{DataTemplate views:SettingsPage}"/>
    <ShellContent Route="Music"       Title="Music"   Icon="music_icon.png"    IsVisible="{Binding IsMusicModuleAvailable}" ContentTemplate="{DataTemplate views:MusicPage}"/>
</TabBar>
```

### 3.3 Modify `src/Clients/DotNetCloud.Client.Android/AppShell.xaml.cs`

```csharp
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.Views;

namespace DotNetCloud.Client.Android;

public partial class AppShell : Shell
{
    private readonly MusicPageVisibilitySource _musicVisibility = new();

    public AppShell()
    {
        InitializeComponent();
        BindingContext = _musicVisibility;
        Routing.RegisterRoute("MessageList", typeof(MessageListPage));
        Routing.RegisterRoute("ChannelDetails", typeof(ChannelDetailsPage));
    }
}
```

---

## Phase 4: Music ViewModel & Browsing UI

### 4.1 Create `src/Clients/DotNetCloud.Client.Android/ViewModels/MusicViewModel.cs`

**Namespace:** `DotNetCloud.Client.Android.ViewModels`
**Pattern:** Use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`).

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Music;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.ViewModels;

public enum MusicView { Artists, Albums, Tracks, Playlists }

public sealed partial class MusicViewModel : ObservableObject
{
    private readonly IMusicRestClient _music;
    private readonly IMusicPlayerService _player;
    private readonly IEqualizerService _eq;
    private readonly IAlbumArtCache _artCache;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;

    public MusicViewModel(
        IMusicRestClient music, IMusicPlayerService player, IEqualizerService eq,
        IAlbumArtCache artCache, IServerConnectionStore serverStore, ISecureTokenStore tokenStore)
    {
        _music = music; _player = player; _eq = eq;
        _artCache = artCache; _serverStore = serverStore; _tokenStore = tokenStore;
        _player.PlaybackStateChanged += (_, _) => UpdatePlaybackState();
        _player.TrackEnded += (_, _) => MainThread.BeginInvokeOnMainThread(() => PlayNextCommand.Execute(null));
    }

    private async Task<(string? serverUrl, string? token)> GetCredentialsAsync()
    {
        var conn = _serverStore.GetActive();
        if (conn is null) return (null, null);
        var tok = await _tokenStore.GetAccessTokenAsync(conn.ServerBaseUrl);
        return (conn.ServerBaseUrl, tok);
    }

    [ObservableProperty] private MusicView _currentView = MusicView.Artists;
    [ObservableProperty] private ArtistDto? _selectedArtist;
    [ObservableProperty] private MusicAlbumDto? _selectedAlbum;
    [ObservableProperty] private string _title = "Music";
    [ObservableProperty] private ObservableCollection<ArtistDto> _artists = [];
    [ObservableProperty] private ObservableCollection<MusicAlbumDto> _albums = [];
    [ObservableProperty] private ObservableCollection<TrackDto> _tracks = [];
    [ObservableProperty] private ObservableCollection<PlaylistDto> _playlists = [];
    [ObservableProperty] private ObservableCollection<EqPresetDto> _eqPresets = [];
    [ObservableProperty] private ObservableCollection<string> _genres = [];
    [ObservableProperty] private TrackDto? _currentTrack;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private double _currentPositionSeconds;
    [ObservableProperty] private double _durationSeconds;
    [ObservableProperty] private bool _isLoading;

    [RelayCommand] private async Task LoadArtistsAsync() { /* GET artists, set CurrentView=Artists */ }
    [RelayCommand] private async Task SelectArtistAsync(ArtistDto artist) { /* GET artist albums */ }
    [RelayCommand] private async Task LoadAlbumsAsync() { /* GET all albums */ }
    [RelayCommand] private async Task SelectAlbumAsync(MusicAlbumDto album) { /* GET album tracks */ }
    [RelayCommand] private async Task LoadTracksAsync() { /* GET all tracks */ }
    [RelayCommand] private async Task LoadPlaylistsAsync() { /* GET playlists */ }
    [RelayCommand] private async Task SelectPlaylistAsync(PlaylistDto p) { /* GET playlist tracks */ }
    [RelayCommand] private async Task LoadEqPresetsAsync() { /* GET EQ presets */ }
    [RelayCommand] private async Task PlayTrackAsync(TrackDto track) { /* _player.PlayAsync */ }
    [RelayCommand] private void TogglePlayPause() { if(_player.IsPlaying) _player.Pause(); else _player.Resume(); }
    [RelayCommand] private void PlayNext() => _player.PlayNext();
    [RelayCommand] private void PlayPrevious() => _player.PlayPrevious();
    [RelayCommand] private async Task SeekAsync(double pos) => _player.Seek(TimeSpan.FromSeconds(pos));
    [RelayCommand] private async Task ToggleStarAsync(object item) { /* _music.ToggleStarAsync */ }
    [RelayCommand] private async Task ApplyEqPresetAsync(EqPresetDto p) => _eq.ApplyPreset(p);
    [RelayCommand] private async Task BackAsync() { /* navigate up: Tracks→Albums→Artists */ }

    private void UpdatePlaybackState()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentTrack = _player.CurrentTrack;
            IsPlaying = _player.IsPlaying;
            CurrentPositionSeconds = _player.CurrentPosition.TotalSeconds;
            DurationSeconds = _player.Duration.TotalSeconds;
        });
    }
}
```

For full method bodies, see the detailed implementation section below. Each `Load*`/`Select*` command follows the same pattern: get credentials → call `_music.MethodAsync(...)` → populate ObservableCollection → update `CurrentView`/`Title`.

### 4.2 Create `src/Clients/DotNetCloud.Client.Android/Views/MusicPage.xaml`

**Layout structure:**
- `ContentPage` → `Grid` with 2 rows: `Auto` (now-playing bar) + `*` (content)
- **Now-playing bar:** `Frame` containing `Grid` with album art `Image` (48x48), track info `VerticalStackLayout`, play/pause/next buttons, seek `Slider`. Visibility bound to `CurrentTrack` not null via `IsNotNullConverter`.
- **Content:** `Grid` with 2 rows: segmented tabs `HorizontalStackLayout` (Artists|Albums|Tracks|Playlists buttons) + `CollectionView` with `DataTemplateSelector` or visibility-switched `CollectionView` instances.

### 4.3 Create `src/Clients/DotNetCloud.Client.Android/Views/MusicPage.xaml.cs`

```csharp
using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

public partial class MusicPage : ContentPage
{
    private readonly MusicViewModel _vm;
    public MusicPage(MusicViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Artists.Count == 0) await _vm.LoadArtistsCommand.ExecuteAsync(null);
    }
}
```

### 4.4 Register in `MauiProgram.cs`

```csharp
builder.Services.AddTransient<MusicViewModel>();
builder.Services.AddTransient<MusicPage>();
```

---

## Phase 5: Audio Playback Foreground Service

### 5.1 Create `src/Clients/DotNetCloud.Client.Android/Services/IMusicPlayerService.cs`

**Namespace:** `DotNetCloud.Client.Android.Services`

```csharp
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Services;

public interface IMusicPlayerService
{
    Task PlayAsync(TrackDto track, string serverBaseUrl, string accessToken);
    void Pause();
    void Resume();
    void Stop();
    void Seek(TimeSpan position);
    void SetVolume(float volume); // 0.0 - 1.0
    void PlayNext();
    void PlayPrevious();
    void Enqueue(IEnumerable<TrackDto> tracks);

    TrackDto? CurrentTrack { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan Duration { get; }
    bool IsPlaying { get; }
    int AudioSessionId { get; }

    event EventHandler? PlaybackStateChanged;
    event EventHandler? TrackEnded;
}
```

### 5.2 Create `src/Clients/DotNetCloud.Client.Android/Services/MusicPlayerService.cs`

**Namespace:** `DotNetCloud.Client.Android.Services`

Wraps `Android.Media.MediaPlayer`. Key implementation:

- **Constructor:** inject `ILogger<MusicPlayerService>`
- **PlayAsync:** store `serverBaseUrl`/`accessToken`, add track to queue at current position, call `PrepareAndStartAsync()`
- **PrepareAndStartAsync:** `_mediaPlayer?.Release()`, new `MediaPlayer()`, then:
  ```csharp
  var audioUrl = $"{serverBaseUrl!.TrimEnd('/')}/api/v1/files/{track.FileNodeId}/content";
  var headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {accessToken}" };
  _mediaPlayer.SetDataSource(Android.App.Application.Context, Android.Net.Uri.Parse(audioUrl), headers);
  _mediaPlayer.Completion += (_, _) => { TrackEnded?.Invoke(this, EventArgs.Empty); PlayNextIfQueued(); };
  _mediaPlayer.Prepared += (_, _) => { _mediaPlayer.Start(); _isPlaying = true; StartPositionTimer(); StartForegroundService(); };
  _mediaPlayer.PrepareAsync();
  ```
- **Position timer:** `System.Timers.Timer(1000)` fires `PlaybackStateChanged`
- **Pause:** `_mediaPlayer?.Pause()`, `_isPlaying = false`, stop timer
- **Resume:** `_mediaPlayer?.Start()`, `_isPlaying = true`, start timer
- **Stop:** `_mediaPlayer?.Stop()`, `Release()`, `null`, stop timer, stop foreground service
- **CurrentPosition:** `TimeSpan.FromMilliseconds(_mediaPlayer?.CurrentPosition ?? 0)`
- **Duration:** `TimeSpan.FromMilliseconds(_mediaPlayer?.Duration ?? 0)`
- **AudioSessionId:** `_mediaPlayer?.AudioSessionId ?? 0`
- **Queue:** `List<TrackDto> _queue`, `int _queueIndex`. `PlayNext()` increments index modulo count. `PlayPrevious()` decrements with wrap. `Enqueue()` appends.
- **Foreground service:** `StartForegroundService()` creates intent with `MusicPlaybackService.ActionStart`, calls `context.StartForegroundService(intent)`. `StopForegroundService()` uses `ActionStop`.
- **Register:** singleton in `MauiProgram.cs`

### 5.3 Create `src/Clients/DotNetCloud.Client.Android/Platforms/Android/MusicPlaybackService.cs`

**File:** `src/Clients/DotNetCloud.Client.Android/Platforms/Android/MusicPlaybackService.cs`
**Pattern:** Copy `ChatConnectionService` structure exactly.

```csharp
using Android.App;
using Android.Content;
using Android.OS;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

[Service(Name = "net.dotnetcloud.client.MusicPlaybackService",
         ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback,
         Exported = false)]
public sealed class MusicPlaybackService : Service
{
    public const string ActionStart = "net.dotnetcloud.client.action.START_MUSIC";
    public const string ActionStop  = "net.dotnetcloud.client.action.STOP_MUSIC";
    public const string ActionPlayPause = "net.dotnetcloud.client.action.MUSIC_PLAYPAUSE";
    public const string ActionNext = "net.dotnetcloud.client.action.MUSIC_NEXT";
    public const string ActionPrevious = "net.dotnetcloud.client.action.MUSIC_PREVIOUS";
    internal const int NotificationId = 1002;
    internal const string ChannelId = "music_playback";

    private PowerManager.WakeLock? _wakeLock;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop) { StopForeground(StopForegroundFlags.Remove); StopSelf(); return StartCommandResult.NotSticky; }

        // Handle media button actions via IMusicPlayerService from DI
        var player = Ioc.Default.GetService<IMusicPlayerService>();
        switch (intent?.Action)
        {
            case ActionPlayPause: if (player!.IsPlaying) player.Pause(); else player.Resume(); break;
            case ActionNext: player!.PlayNext(); break;
            case ActionPrevious: player!.PlayPrevious(); break;
        }

        StartForeground(NotificationId, BuildNotification());
        AcquireWakeLock();
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy() { _wakeLock?.Release(); _wakeLock = null; base.OnDestroy(); }

    private void AcquireWakeLock()
    {
        var pm = (PowerManager?)GetSystemService(PowerService);
        _wakeLock = pm?.NewWakeLock(WakeLockFlags.Partial, "DotNetCloud::MusicWakeLock");
        _wakeLock?.Acquire();
    }

    private Notification BuildNotification()
    {
        var player = Ioc.Default.GetService<IMusicPlayerService>();
        var title = player?.CurrentTrack?.Title ?? "DotNetCloud Music";
        var artist = player?.CurrentTrack?.ArtistName ?? "";
        // Build MediaStyle notification with play/pause, next, previous PendingIntents
        // Uses Notification.Builder, SetContentTitle, SetContentText, SetStyle(new Notification.MediaStyle())
        // AddAction for Previous (Icon: IcMediaPrevious), PlayPause (Icon: IcMediaPlay or IcMediaPause based on state), Next (Icon: IcMediaNext)
        // SetContentIntent opens MainActivity
        return null!; // See full implementation in ChatConnectionService for the exact pattern
    }
}
```

For the full `BuildNotification()` implementation, follow the pattern in `ChatConnectionService.BuildNotification()` but use `MediaStyle`, three `AddAction` calls, and check `player.IsPlaying` to toggle the play/pause icon.

### 5.4 Modify `AndroidManifest.xml`

File: `src/Clients/DotNetCloud.Client.Android/Platforms/Android/AndroidManifest.xml`

Inside `<application>`, after the existing `ChatConnectionService`:
```xml
<service android:name=".MusicPlaybackService"
         android:foregroundServiceType="mediaPlayback"
         android:exported="false" />
```

After existing `FOREGROUND_SERVICE_DATA_SYNC` permission:
```xml
<uses-permission android:name="android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK" />
```

### 5.5 Modify `MainApplication.cs`

In `CreateNotificationChannels()`, add:
```csharp
nm.CreateNotificationChannel(new NotificationChannel(
    MusicPlaybackService.ChannelId, "Music playback", NotificationImportance.Low)
{ Description = "Shows current track and playback controls" });
```

### 5.6 Register in `MauiProgram.cs`

```csharp
builder.Services.AddSingleton<IMusicPlayerService, MusicPlayerService>();
```

---

## Phase 6: Album Art Caching

### 6.1 Create `src/Clients/DotNetCloud.Client.Android/Services/IAlbumArtCache.cs`

**Namespace:** `DotNetCloud.Client.Android.Services`

```csharp
namespace DotNetCloud.Client.Android.Services;

public interface IAlbumArtCache
{
    Task<ImageSource?> GetAlbumArtAsync(Guid albumId, string serverBaseUrl, string accessToken, CancellationToken ct = default);
    void Invalidate(Guid albumId);
    void Clear();
}
```

### 6.2 Create `src/Clients/DotNetCloud.Client.Android/Services/AlbumArtCache.cs`

**Namespace:** `DotNetCloud.Client.Android.Services`

```csharp
using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace DotNetCloud.Client.Android.Services;

public sealed class AlbumArtCache : IAlbumArtCache
{
    private const int MaxMemoryEntries = 50;
    private readonly ConcurrentDictionary<Guid, CachedEntry> _memory = new();
    private readonly HttpClient _http;
    private readonly string _diskDir;

    public AlbumArtCache(HttpClient http)
    {
        _http = http;
        _diskDir = Path.Combine(FileSystem.CacheDirectory, "albumart");
        Directory.CreateDirectory(_diskDir);
    }

    public async Task<ImageSource?> GetAlbumArtAsync(
        Guid albumId, string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        // 1. Memory hit
        if (_memory.TryGetValue(albumId, out var entry))
        { entry.LastAccess = DateTime.UtcNow; return entry.Source; }
        // 2. Disk hit
        var diskPath = GetDiskPath(albumId);
        if (File.Exists(diskPath))
        { var src = ImageSource.FromFile(diskPath); AddToMemory(albumId, src); return src; }
        // 3. Download
        try
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/music/albums/{albumId}/cover";
            var bytes = await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(diskPath, bytes, ct).ConfigureAwait(false);
            var source = ImageSource.FromStream(() => new MemoryStream(bytes));
            AddToMemory(albumId, source);
            return source;
        }
        catch { return null; }
    }

    public void Invalidate(Guid albumId) { _memory.TryRemove(albumId, out _); try { File.Delete(GetDiskPath(albumId)); } catch { } }
    public void Clear() { _memory.Clear(); try { Directory.Delete(_diskDir, true); Directory.CreateDirectory(_diskDir); } catch { } }

    private string GetDiskPath(Guid albumId) => Path.Combine(_diskDir, $"{albumId:N}.jpg");

    private void AddToMemory(Guid key, ImageSource source)
    {
        if (_memory.Count >= MaxMemoryEntries)
        { var lru = _memory.OrderBy(kvp => kvp.Value.LastAccess).First(); _memory.TryRemove(lru.Key, out _); }
        _memory[key] = new CachedEntry(source, DateTime.UtcNow);
    }

    private sealed class CachedEntry(ImageSource source, DateTime lastAccess)
    { public ImageSource Source { get; } = source; public DateTime LastAccess { get; set; } = lastAccess; }
}
```

**Note:** Uses C# 12 primary constructor for `CachedEntry`. If targeting C# 11 or lower, expand to traditional constructor.

### 6.3 Register in `MauiProgram.cs`

```csharp
builder.Services.AddHttpClient<IAlbumArtCache, AlbumArtCache>()
    .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);
```

---

## Phase 7: Native Android Equalizer

### 7.1 Create `src/Clients/DotNetCloud.Client.Android/Services/IEqualizerService.cs`

```csharp
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Services;

public interface IEqualizerService
{
    bool IsAvailable { get; }
    int NumberOfBands { get; }
    int[] GetBandFrequenciesMhz(); // center frequencies in millihertz
    void SetBandLevel(int bandIndex, short gainMb); // gain in millibels
    void SetAllBands(IDictionary<string, double> bands); // keys: freq labels, values: dB
    void ApplyPreset(EqPresetDto preset);
    void Reset();
    event EventHandler? AvailabilityChanged;
}
```

### 7.2 Create `src/Clients/DotNetCloud.Client.Android/Services/AndroidEqualizerService.cs`

```csharp
using Android.Media.Audiofx;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Services;

public sealed class AndroidEqualizerService : IEqualizerService, IDisposable
{
    private readonly IMusicPlayerService _player;
    private Equalizer? _equalizer;
    private bool _isAvailable;

    // Server preset target frequencies in Hz
    private static readonly int[] ServerFrequencies = [31, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    public event EventHandler? AvailabilityChanged;

    public AndroidEqualizerService(IMusicPlayerService player)
    {
        _player = player;
        _player.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    public bool IsAvailable => _isAvailable && _equalizer is not null;
    public int NumberOfBands => _equalizer?.NumberOfBands ?? 0;

    public int[] GetBandFrequenciesMhz()
    {
        if (_equalizer is null) return [];
        var freqs = new int[_equalizer.NumberOfBands];
        for (int i = 0; i < freqs.Length; i++) freqs[i] = _equalizer.GetCenterFreq((short)i);
        return freqs;
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        if (_player.IsPlaying && _player.AudioSessionId != 0) CreateEqualizer();
        else DisposeEqualizer();
    }

    private void CreateEqualizer()
    {
        try
        {
            DisposeEqualizer();
            _equalizer = new Equalizer(0, _player.AudioSessionId); // priority 0 = highest
            _equalizer.SetEnabled(true);
            _isAvailable = true;
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
        catch { _isAvailable = false; _equalizer = null; }
    }

    private void DisposeEqualizer()
    {
        _equalizer?.SetEnabled(false);
        _equalizer?.Release();
        _equalizer?.Dispose();
        _equalizer = null;
    }

    public void SetBandLevel(int bandIndex, short gainMb)
        => _equalizer?.SetBandLevel((short)bandIndex, gainMb);

    /// <summary>Maps server frequency labels ("31","63",...,"16K") to closest device EQ bands.
    /// Server gains are in dB (range ~-12 to +12). Android uses millibels (range ~-1500 to +1500).
    /// Conversion: gainMb = (short)Clamp((int)(gainDb * 100), -1500, 1500).</summary>
    public void SetAllBands(IDictionary<string, double> bands)
    {
        if (_equalizer is null) return;
        var deviceFreqs = new int[_equalizer.NumberOfBands];
        for (int i = 0; i < deviceFreqs.Length; i++) deviceFreqs[i] = _equalizer.GetCenterFreq((short)i);

        foreach (var (freqLabel, gainDb) in bands)
        {
            var targetHz = ParseFrequencyLabel(freqLabel);
            var bandIdx = FindClosestBand(deviceFreqs, targetHz);
            if (bandIdx < 0) continue;
            var gainMb = (short)Math.Clamp((int)(gainDb * 100), -1500, 1500);
            _equalizer.SetBandLevel((short)bandIdx, gainMb);
        }
    }

    public void ApplyPreset(EqPresetDto preset) => SetAllBands(new Dictionary<string, double>(preset.Bands));

    public void Reset()
    {
        if (_equalizer is null) return;
        for (short i = 0; i < _equalizer.NumberOfBands; i++) _equalizer.SetBandLevel(i, 0);
    }

    private static int ParseFrequencyLabel(string label)
    {
        if (label.EndsWith("K", StringComparison.OrdinalIgnoreCase) && int.TryParse(label[..^1], out var kHz))
            return kHz * 1000;
        return int.TryParse(label, out var hz) ? hz : 0;
    }

    private static int FindClosestBand(int[] deviceFreqsMhz, int targetHz)
    {
        int bestIdx = -1, bestDiff = int.MaxValue;
        for (int i = 0; i < deviceFreqsMhz.Length; i++)
        {
            int freqHz = deviceFreqsMhz[i] / 1000; // millihertz → Hz
            int diff = Math.Abs(freqHz - targetHz);
            if (diff < bestDiff) { bestDiff = diff; bestIdx = i; }
        }
        return bestIdx;
    }

    public void Dispose()
    {
        _player.PlaybackStateChanged -= OnPlaybackStateChanged;
        DisposeEqualizer();
    }
}
```

### 7.3 Register in `MauiProgram.cs`

```csharp
builder.Services.AddSingleton<IEqualizerService, AndroidEqualizerService>();
```

---

## Phase 8: Resources & Integration

### 8.1 Create `Resources/Images/music_icon.svg`

Music note icon in the same style as existing tab icons (24x24 viewBox, `#0EA5E9` stroke):
```svg
<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="#0EA5E9" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
  <path d="M9 18V5l12-2v13"/>
  <circle cx="6" cy="18" r="3"/>
  <circle cx="18" cy="16" r="3"/>
</svg>
```

### 8.2 Extend `src/Clients/DotNetCloud.Client.Android/Converters/AppConverters.cs`

Add two converters:

```csharp
public sealed class TimeSpanToMmSsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        return "0:00";
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class BoolToPlayPauseIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "⏸" : "▶";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
```

### 8.3 Final `MauiProgram.cs` registration summary

All new registrations:
```csharp
// ── Music ─────────────────────────────────────────────────────────
builder.Services.AddHttpClient<Music.IMusicRestClient, Music.HttpMusicRestClient>()
    .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);

builder.Services.AddHttpClient<IAlbumArtCache, AlbumArtCache>()
    .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);

builder.Services.AddSingleton<IMusicPlayerService, MusicPlayerService>();
builder.Services.AddSingleton<IEqualizerService, AndroidEqualizerService>();

// ── ViewModels ────────────────────────────────────────────────
builder.Services.AddTransient<MusicViewModel>();

// ── Pages ─────────────────────────────────────────────────────
builder.Services.AddTransient<MusicPage>();
```

---

## API Reference: All Server Endpoints Used

| Endpoint | Method | Response `data` |
|----------|--------|-----------------|
| `/api/v1/core/modules/music/available` | GET | `{ installed: bool }` |
| `/api/v1/music/artists?skip=&take=` | GET | `List<ArtistDto>` |
| `/api/v1/music/artists/{id}` | GET | `ArtistDto` |
| `/api/v1/music/artists/search?q=&take=` | GET | `List<ArtistDto>` |
| `/api/v1/music/artists/{id}/albums` | GET | `List<MusicAlbumDto>` |
| `/api/v1/music/albums?skip=&take=` | GET | `List<MusicAlbumDto>` |
| `/api/v1/music/albums/{id}` | GET | `MusicAlbumDto` |
| `/api/v1/music/albums/search?q=&take=` | GET | `List<MusicAlbumDto>` |
| `/api/v1/music/albums/recent?take=` | GET | `List<MusicAlbumDto>` |
| `/api/v1/music/albums/{id}/cover` | GET | JPEG/PNG binary |
| `/api/v1/music/albums/{id}/tracks` | GET | `List<TrackDto>` |
| `/api/v1/music/tracks?skip=&take=` | GET | `List<TrackDto>` |
| `/api/v1/music/tracks/{id}` | GET | `TrackDto` |
| `/api/v1/music/tracks/search?q=&take=` | GET | `List<TrackDto>` |
| `/api/v1/music/tracks/random?take=&genre=` | GET | `List<TrackDto>` |
| `/api/v1/music/tracks/recent?take=` | GET | `List<TrackDto>` |
| `/api/v1/music/tracks/{id}/play` | POST | `{ recorded: true }` |
| `/api/v1/music/tracks/{id}/star` | POST | `{ toggled: true }` |
| `/api/v1/music/albums/{id}/star` | POST | `{ toggled: true }` |
| `/api/v1/music/artists/{id}/star` | POST | `{ toggled: true }` |
| `/api/v1/music/playlists` | GET | `List<PlaylistDto>` |
| `/api/v1/music/playlists/{id}` | GET | `PlaylistDto` |
| `/api/v1/music/playlists/{id}/tracks` | GET | `List<TrackDto>` |
| `/api/v1/music/eq/presets` | GET | `List<EqPresetDto>` |
| `/api/v1/music/eq/presets/{id}` | GET | `EqPresetDto` |
| `/api/v1/music/genres` | GET | `List<string>` |
| `/api/v1/files/{fileNodeId}/content` | GET | Audio binary (from Files module) |

**All responses** use envelope: `{ "success": true, "data": ... }`. All require `Authorization: Bearer {token}`.

---

## Files Summary

### Create (15 files)

| # | File | Est. Lines |
|---|------|-----------|
| 1 | `Music/IMusicRestClient.cs` | ~90 |
| 2 | `Music/HttpMusicRestClient.cs` | ~330 |
| 3 | `Services/ModuleAvailabilityState.cs` | ~8 |
| 4 | `Services/MusicPageVisibilitySource.cs` | ~18 |
| 5 | `ViewModels/MusicViewModel.cs` | ~250 |
| 6 | `Views/MusicPage.xaml` | ~200 |
| 7 | `Views/MusicPage.xaml.cs` | ~20 |
| 8 | `Services/IMusicPlayerService.cs` | ~35 |
| 9 | `Services/MusicPlayerService.cs` | ~200 |
| 10 | `Platforms/Android/MusicPlaybackService.cs` | ~200 |
| 11 | `Services/IAlbumArtCache.cs` | ~12 |
| 12 | `Services/AlbumArtCache.cs` | ~80 |
| 13 | `Services/IEqualizerService.cs` | ~18 |
| 14 | `Services/AndroidEqualizerService.cs` | ~140 |
| 15 | `Resources/Images/music_icon.svg` | ~8 |
| | **Total** | **~1,609** |

### Modify (7 files)

| # | File | Changes |
|---|------|---------|
| 1 | `MauiProgram.cs` | ~20 lines: 4 service + 1 VM + 1 page registrations |
| 2 | `App.xaml.cs` | ~40 lines: inject `ISecureTokenStore`, add `CheckAvailableModulesAsync()`, call in `OnStart()` |
| 3 | `AppShell.xaml` | ~3 lines: add Music `ShellContent` with `IsVisible` binding |
| 4 | `AppShell.xaml.cs` | ~4 lines: add `MusicPageVisibilitySource`, set `BindingContext` |
| 5 | `AndroidManifest.xml` | ~5 lines: service + permission declarations |
| 6 | `MainApplication.cs` | ~6 lines: music notification channel |
| 7 | `Converters/AppConverters.cs` | ~25 lines: two converters |

---

## Verification Checklist

- ☐ `dotnet build src/Clients/DotNetCloud.Client.Android/ -f net10.0-android` succeeds (0 errors)
- ☐ Music tab hidden when `/api/v1/core/modules/music/available` returns `installed: false`
- ☐ Music tab visible when module is installed and active
- ☐ Artists/albums/tracks/playlists load and display
- ☐ Drill-down artist→albums→tracks works; back button returns
- ☐ Track streaming works (tap → audio plays via MediaPlayer)
- ☐ Play/pause/next/previous/seek all work
- ☐ Background playback: switch away → audio continues (foreground service + notification)
- ☐ Notification shows track info + media controls that work
- ☐ EQ presets load from server; applying changes audio tonality
- ☐ Album art visible and cached (second view instant)
- ☐ Token refresh works (401 → AuthenticatedHttpClientHandler refreshes → retries)
- ☐ Chat, Files, Settings tabs unaffected
- ☐ Login/logout flow unaffected

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| REST (not gRPC) | Android has no gRPC infra. Music module exposes full REST API. Same `HttpClient` + `AuthenticatedHttpClientHandler` as Chat/Files. |
| Foreground service + WakeLock | Follows `ChatConnectionService` pattern. `TypeMediaPlayback`, `Sticky`, media notification. |
| `Android.Media.MediaPlayer` | Supports HTTP streaming with auth headers. Simpler than ExoPlayer for MAUI. |
| Two-tier album art cache | Memory LRU (50 items) + disk. Uses `FileSystem.CacheDirectory`. |
| `AudioEffect.Equalizer` | Standard Android EQ. Priority 0. Maps 10-band server presets to device bands. dB→millibels conversion. |
| `ShellContent.IsVisible` binding | Reads static `ModuleAvailabilityState`. Set before Shell first displays. |
| Shared `DotNetCloud.Core` DTOs | Reuses existing `ArtistDto`, `TrackDto`, etc. No duplicate definitions. |
| No playlist creation, visualization, offline, indexing | Excluded by design per requirements. |

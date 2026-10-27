using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Localization;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Music.Services;
using DotNetCloud.Modules.Music.UI;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Editors;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Layout;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Resources;
using DotNetCloud.UI.Shared.Services;
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Client.Shared;
using DotNetCloud.UI.Web.Components.Layout;
using DotNetCloud.UI.Web.Components.Pages;
using DotNetCloud.UI.Web.Components.Pages.Modules.Help;
using DotNetCloud.UI.Web.Components.Shared;
using DotNetCloud.UI.Web.Components.Shared.Search;
using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>Code-behind for the <see cref="GlobalMusicPlaybar"/> component.</summary>
public partial class GlobalMusicPlaybar : ComponentBase, IAsyncDisposable
{
    private bool _musicModuleEnabled;
    private bool _jsInitialized;
    private bool _disposed;

    /// <summary>Album whose cover failed to load; the fallback SVG is rendered instead.</summary>
    private Guid? _albumArtFailedForAlbumId;
    private DotNetObjectReference<GlobalMusicPlaybar>? _dotNetRef;
    private string _newPresetName = string.Empty;
    private Guid? _selectedPresetId;

    // ── Queue-to-playlist state ──
    private bool _showSaveQueueMenu;
    private bool _showSaveQueueDialog;
    private string _newQueuePlaylistName = string.Empty;
    private bool _newQueuePlaylistPublic;
    private int? _queueItemMenuIndex;
    private List<PlaylistDto> _playlists = [];

    // ── Settings persistence ──
    private const string SettingsModule = "dotnetcloud.music";
    private const string VolumeKey = "player.volume";
    private const string MutedKey = "player.muted";
    private const string ShuffleKey = "player.shuffle";
    private const string RepeatKey = "player.repeat";
    private const string EqBandsKey = "player.eq-bands";
    private const string LocalEqStateKey = "dotnetcloud.music:eq-state";
    private bool _settingsLoaded;
    private LocalEqState? _localEqState;
    private CancellationTokenSource? _saveCts;

    /// <summary>
    /// CSS class applying the repeat-mode colour: red for Off, yellow for One, green for All.
    /// </summary>
    private string RepeatModeCssClass => Playback.Repeat switch
    {
        RepeatMode.One => "gmp-repeat-mode-one",
        RepeatMode.All => "gmp-repeat-mode-all",
        _ => "gmp-repeat-mode-off",
    };

    /// <summary>Human-readable repeat mode label shown next to the repeat button.</summary>
    private string RepeatLabel => Playback.Repeat switch
    {
        RepeatMode.One => "One",
        RepeatMode.All => "All",
        _ => "Off",
    };

    /// <summary>Material icon name reflecting the current mute / volume level.</summary>
    private string VolumeIcon => Playback.Muted
        ? "volume_off"
        : Playback.Volume > 50 ? "volume_up" : "volume_down";

    protected override void OnInitialized()
    {
        _musicModuleEnabled = ModuleRegistry.NavItems.Any(n =>
            string.Equals(n.ModuleId, "dotnetcloud.music", StringComparison.OrdinalIgnoreCase));

        Playback.OnChange += OnPlaybackStateChanged;
        Playback.OnTrackChanged += OnTrackChanged;
        ModuleRegistry.OnChange += OnModuleRegistryChanged;

        // The playbar outlives page navigation, so follow it to keep the tab title in sync:
        // the song belongs in the title only while the user is on the Music module.
        Navigation.LocationChanged += OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (_disposed)
            return;

        _ = InvokeAsync(UpdateTabTitleAsync);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_musicModuleEnabled && Playback.NowPlaying is not null && !_jsInitialized)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.init", _dotNetRef);
                _jsInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to initialize global music player JS interop");
            }
        }

        if (firstRender && Playback.NowPlaying is not null)
        {
            // A track may already be playing (e.g. after a circuit reconnect) — reflect it in the tab title.
            await UpdateTabTitleAsync();
        }
    }

    private void OnPlaybackStateChanged()
    {
        InvokeAsync(StateHasChanged);
        _ = InvokeAsync(UpdateTabTitleAsync);
    }

    /// <summary>
    /// Handles a failed album-art image load by switching to the inline SVG fallback.
    /// </summary>
    /// <remarks>
    /// This used to be an inline <c>onerror=""</c> attribute in the markup, which the hardened
    /// Content-Security-Policy blocks (it allows no <c>'unsafe-inline'</c>/<c>'unsafe-hashes'</c>
    /// for scripts). Using a Blazor event handler keeps the fallback working under the strict policy.
    /// </remarks>
    private void OnAlbumArtError()
    {
        _albumArtFailedForAlbumId = Playback.NowPlaying?.AlbumId;
    }

    private void OnModuleRegistryChanged()
    {
        _musicModuleEnabled = ModuleRegistry.NavItems.Any(n =>
            string.Equals(n.ModuleId, "dotnetcloud.music", StringComparison.OrdinalIgnoreCase));
        InvokeAsync(StateHasChanged);
        if (!_musicModuleEnabled)
        {
            // Module was disabled — drop any now-playing tab title override.
            _ = InvokeAsync(UpdateTabTitleAsync);
        }
    }

    // ── Playback JS interop ──

    private async Task TogglePlayPauseAsync()
    {
        Playback.TogglePlayPause();
        if (_jsInitialized)
        {
            if (Playback.IsPlaying)
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.resume");
            else
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.pause");
        }
    }

    private void PlayNextAsync()
    {
        Playback.PlayNext();
        // JS audio is triggered by OnTrackChanged event
    }

    private void PlayPreviousAsync()
    {
        Playback.PlayPrevious();
        // JS audio is triggered by OnTrackChanged event
    }

    private async Task StopAsync()
    {
        Playback.Stop();
        if (_jsInitialized)
        {
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.stop");
        }
    }

    private async Task ToggleShuffleAsync()
    {
        Playback.ToggleShuffle();
        await SaveSettingImmediateAsync(ShuffleKey, Playback.Shuffle.ToString().ToLowerInvariant());
    }

    private async Task CycleRepeatAsync()
    {
        Playback.CycleRepeat();
        await SaveSettingImmediateAsync(RepeatKey, Playback.Repeat.ToString());
    }

    private async Task ToggleMuteAsync()
    {
        Playback.ToggleMute();
        if (_jsInitialized)
        {
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setMuted", Playback.Muted);
        }
        await SaveSettingImmediateAsync(MutedKey, Playback.Muted.ToString().ToLowerInvariant());
    }

    private async Task OnVolumeChangedAsync(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var vol))
        {
            Playback.SetVolume(vol);
            if (_jsInitialized)
            {
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setVolume", Playback.Muted ? 0 : Playback.Volume);
            }
            SaveSettingDebounced(VolumeKey, Playback.Volume.ToString());
        }
    }

    private async Task SeekAsync(MouseEventArgs e)
    {
        if (Playback.NowPlaying is null) return;
        // Delegate to JS which uses the element's actual rendered width
        // (via getBoundingClientRect) to compute the correct fraction.
        if (_jsInitialized)
        {
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.handleProgressClick", e.ClientX);
        }
    }

    // ── Equalizer JS interop ──

    private async Task ToggleEqualizerAsync()
    {
        await LoadPersistedSettingsAsync();

        if (!Playback.EqPresetsLoaded)
        {
            var caller = await GetCallerAsync();
            await Playback.LoadEqPresetsAsync(caller);
        }

        // Re-resolve the device-local preset now that the preset list is available
        // (the stored preset may have been renamed or re-seeded since it was saved).
        if (_localEqState is not null && Playback.EqPresetsLoaded)
        {
            var resolved = _localEqState.ResolvePresetId(Playback.EqPresets);
            if (resolved != Playback.ActivePresetId)
                Playback.ApplyEqSnapshot(_localEqState.Bands, resolved, isLocalOverride: true);
        }

        Playback.ToggleEqualizer();
    }

    private async Task ApplyPresetAsync(EqPresetDto preset)
    {
        Playback.ApplyPreset(preset);
        if (_jsInitialized)
        {
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setEqBands", Playback.EqBands);
        }
        await SaveLocalEqAsync();
        SaveSettingDebounced(EqBandsKey, JsonSerializer.Serialize(Playback.EqBands));
    }

    private async Task OnEqBandChangedAsync(int bandIndex, ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), out var gain))
        {
            Playback.SetEqBand(bandIndex, gain);
            if (_jsInitialized)
            {
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setEqBand", bandIndex, gain);
            }
            await SaveLocalEqAsync();
            SaveSettingDebounced(EqBandsKey, JsonSerializer.Serialize(Playback.EqBands));
        }
    }

    private async Task ResetEqAsync()
    {
        Playback.ResetEqBands();
        if (_jsInitialized)
        {
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setEqBands", Playback.EqBands);
        }
        await SaveLocalEqAsync();
        await SaveSettingImmediateAsync(EqBandsKey, JsonSerializer.Serialize(Playback.EqBands));
    }

    private async Task SavePresetAsync()
    {
        if (string.IsNullOrWhiteSpace(_newPresetName)) return;
        try
        {
            var caller = await GetCallerAsync();
            await Playback.SavePresetAsync(_newPresetName, caller, _selectedPresetId);
            _newPresetName = string.Empty;
            _selectedPresetId = null;
            await SaveLocalEqAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save EQ preset");
        }
    }

    private void SelectPreset(EqPresetDto preset)
    {
        _selectedPresetId = preset.Id;
        _newPresetName = preset.Name;
    }

    private void OnPresetNameChanged()
    {
        // Clear the preset selection if the user edits the name away from the selected preset's name
        if (_selectedPresetId.HasValue)
        {
            var selected = Playback.EqPresets.FirstOrDefault(p => p.Id == _selectedPresetId.Value);
            if (selected is null || _newPresetName != selected.Name)
            {
                _selectedPresetId = null;
            }
        }
    }

    private async Task SetDefaultPresetAsync(EqPresetDto preset)
    {
        try
        {
            var caller = await GetCallerAsync();
            await Playback.SetDefaultPresetAsync(preset.Id, caller);
            if (_jsInitialized)
            {
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setEqBands", Playback.EqBands);
            }
            // Persist the per-device default immediately; the server value seeds new machines.
            await SaveLocalEqAsync();
            SaveSettingDebounced(EqBandsKey, JsonSerializer.Serialize(Playback.EqBands));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to set default EQ preset");
        }
    }

    private async Task DeletePresetAsync(EqPresetDto preset)
    {
        try
        {
            var caller = await GetCallerAsync();
            await Playback.DeletePresetAsync(preset.Id, caller);
            if (_jsInitialized)
            {
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setEqBands", Playback.EqBands);
            }
            await SaveLocalEqAsync();
            SaveSettingDebounced(EqBandsKey, JsonSerializer.Serialize(Playback.EqBands));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete EQ preset");
        }
    }

    // ── Star toggle ──

    private async Task ToggleStarAsync()
    {
        try
        {
            var caller = await GetCallerAsync();
            await Playback.ToggleStarNowPlayingAsync(caller);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to toggle star");
        }
    }

    // ── Navigation ──

    private void NavigateToPlayingAlbum()
    {
        // Navigate to the music page; the ScrollToPlaying handler on MusicPage
        // finds the now-playing track's album and navigates to it.
        Navigation.NavigateTo("/apps/music?playing=true");
    }

    private async Task NavigateToCurrentContext()
    {
        // If a playlist is active, navigate to it; otherwise navigate to the album
        if (ActivePlaylist.PlaylistId.HasValue)
        {
            await NavigateToActivePlaylistAsync();
        }
        else if (Playback.NowPlaying?.AlbumId.HasValue == true)
        {
            NavigateToPlayingAlbum();
        }
        else
        {
            Navigation.NavigateTo("/apps/music?playing=true");
        }
    }

    private void NavigateToArtistPage(Guid artistId)
    {
        Navigation.NavigateTo($"/apps/music?artistId={artistId}");
    }

    private async Task NavigateToActivePlaylistAsync()
    {
        Navigation.NavigateTo("/apps/music");
        // Small delay to let MusicPage initialize, then trigger playlist navigation
        await Task.Delay(100);
        ActivePlaylist.RequestNavigateToPlaylist();
    }

    // ── JS Interop callbacks (invoked from music-player.js) ──

    [JSInvokable]
    public void OnJsTimeUpdate(double currentTime, double duration)
    {
        Playback.UpdatePosition(TimeSpan.FromSeconds(currentTime));
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task OnJsTrackEnded()
    {
        InvokeAsync(() => { PlayNextAsync(); StateHasChanged(); });
        return Task.CompletedTask;
    }

    [JSInvokable]
    public void OnJsPlaybackError(string message)
    {
        Logger.LogWarning("Audio playback error: {Message}", message);
        Playback.SetPlaying(false);
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public void OnJsMetadataLoaded(double duration)
    {
        Playback.UpdateDurationFromMetadata(duration);
        InvokeAsync(StateHasChanged);
    }

    // ── Track change handler (triggered by MusicPlaybackState when a new track starts) ──

    private void OnTrackChanged(TrackDto track)
    {
        // Fire-and-forget, but observe failures: an unhandled exception from a faulted task here
        // would otherwise be lost (and a hung playback init leaves the UI looking frozen).
        _ = InvokeAsync(async () =>
        {
            try
            {
                await StartPlaybackJsAsync(track);
            }
            catch (JSDisconnectedException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to start JS playback for track {TrackId}", track.Id);
            }
        });
        _ = InvokeAsync(UpdateTabTitleAsync);
    }

    /// <summary>
    /// Initializes JS audio playback for a track. Called when MusicPlaybackState fires OnTrackChanged.
    /// </summary>
    private async Task StartPlaybackJsAsync(TrackDto track)
    {
        if (!_settingsLoaded)
        {
            await LoadPersistedSettingsAsync();
        }

        if (!_jsInitialized)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.init", _dotNetRef);
                _jsInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to initialize JS audio for playback");
                return;
            }
        }

        if (track.FileNodeId != Guid.Empty)
        {
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.play", MusicPlaybackState.GetAudioUrl(track));
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setVolume", Playback.Muted ? 0 : Playback.Volume);
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setEqBands", Playback.EqBands);
        }

        await RecordPlaySafeAsync();
    }

    // ── Browser tab title (now-playing) ──

    /// <summary>
    /// Reflects the now-playing track (artist + song) in the browser tab title, but only while the
    /// user is on the Music module and audio is actually playing. Navigating elsewhere — or the
    /// queue finishing — restores the page's own title instead of leaving the last song stuck there.
    /// </summary>
    private async Task UpdateTabTitleAsync()
    {
        try
        {
            // Show the song only in the Music module, and only while it is really playing.
            var track = _musicModuleEnabled
                && Playback.IsPlaying
                && Playback.NowPlaying is not null
                && MusicRoutes.IsMusicUrl(Navigation.Uri)
                    ? Playback.NowPlaying
                    : null;

            if (track is null)
            {
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.clearNowPlayingTitle");
                return;
            }

            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setNowPlayingTitle", BuildNowPlayingLabel(track));
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to update browser tab title for now-playing track");
        }
    }

    private static string BuildNowPlayingLabel(TrackDto track)
    {
        var title = string.IsNullOrWhiteSpace(track.Title) ? "Unknown Track" : track.Title.Trim();
        var artist = string.IsNullOrWhiteSpace(track.ArtistName) ? null : track.ArtistName.Trim();
        return artist is null ? $"♪ {title}" : $"♪ {title} — {artist}";
    }

    // ── Helpers ──

    private async Task<CallerContext> GetCallerAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? throw new UnauthorizedAccessException("Not authenticated.");
        var roles = authState.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(Guid.Parse(userId), roles, CallerType.User);
    }

    private async Task RecordPlaySafeAsync()
    {
        try
        {
            var caller = await GetCallerAsync();
            await Playback.RecordPlayAsync(caller);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error recording play");
        }
    }

    // ── Queue-to-playlist ──

    private async Task LoadPlaylistsAsync()
    {
        try
        {
            var caller = await GetCallerAsync();
            _playlists = (await PlaylistService.ListPlaylistsAsync(caller)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load playlists for queue save");
        }
    }

    private void ToggleSaveQueueMenu()
    {
        _showSaveQueueMenu = !_showSaveQueueMenu;
        if (_showSaveQueueMenu)
        {
            _ = LoadPlaylistsAsync();
        }
    }

    private void ShowSaveQueueAsNewDialog()
    {
        _showSaveQueueMenu = false;
        _newQueuePlaylistName = string.Empty;
        _newQueuePlaylistPublic = false;
        _showSaveQueueDialog = true;
    }

    private void CloseSaveQueueDialog()
    {
        _showSaveQueueDialog = false;
    }

    private async Task SaveQueueAsNewPlaylistAsync()
    {
        if (string.IsNullOrWhiteSpace(_newQueuePlaylistName) || Playback.Queue.Count == 0)
            return;

        try
        {
            var caller = await GetCallerAsync();
            var playlist = await PlaylistService.CreatePlaylistAsync(
                new CreatePlaylistDto { Name = _newQueuePlaylistName, IsPublic = _newQueuePlaylistPublic }, caller);

            var trackIds = Playback.Queue.Select(t => t.Id).Distinct().ToList();
            await PlaylistService.AddTrackRangeAsync(playlist.Id, trackIds, caller);

            _showSaveQueueDialog = false;
            _newQueuePlaylistName = string.Empty;
            _newQueuePlaylistPublic = false;
            _playlists = (await PlaylistService.ListPlaylistsAsync(caller)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save queue as new playlist");
        }
    }

    private async Task AddQueueToExistingPlaylistAsync(Guid playlistId)
    {
        _showSaveQueueMenu = false;
        try
        {
            var caller = await GetCallerAsync();
            var trackIds = Playback.Queue.Select(t => t.Id).Distinct().ToList();
            await PlaylistService.AddTrackRangeAsync(playlistId, trackIds, caller);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add queue to playlist {PlaylistId}", playlistId);
        }
    }

    private void ToggleQueueItemMenu(int index)
    {
        _queueItemMenuIndex = _queueItemMenuIndex == index ? null : index;
        if (_queueItemMenuIndex.HasValue)
        {
            _ = LoadPlaylistsAsync();
        }
    }

    private async Task AddTrackToPlaylistFromQueueAsync(Guid playlistId, Guid trackId)
    {
        _queueItemMenuIndex = null;
        try
        {
            var caller = await GetCallerAsync();
            await PlaylistService.AddTrackAsync(playlistId, trackId, caller);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add track {TrackId} to playlist {PlaylistId}", trackId, playlistId);
        }
    }

    // ── Settings persistence ──

    private async Task LoadPersistedSettingsAsync()
    {
        if (_settingsLoaded) return;
        _settingsLoaded = true;
        try
        {
            var userId = await GetUserIdAsync();
            if (userId is null) return;

            var volSetting = await UserSettingsService.GetSettingAsync(userId.Value, SettingsModule, VolumeKey);
            if (volSetting is not null && int.TryParse(volSetting.Value, out var vol))
                Playback.SetVolume(vol);

            var muteSetting = await UserSettingsService.GetSettingAsync(userId.Value, SettingsModule, MutedKey);
            if (muteSetting is not null && bool.TryParse(muteSetting.Value, out var muted) && muted != Playback.Muted)
                Playback.ToggleMute();

            var shuffleSetting = await UserSettingsService.GetSettingAsync(userId.Value, SettingsModule, ShuffleKey);
            if (shuffleSetting is not null && bool.TryParse(shuffleSetting.Value, out var shuffle) && shuffle != Playback.Shuffle)
                Playback.ToggleShuffle();

            var repeatSetting = await UserSettingsService.GetSettingAsync(userId.Value, SettingsModule, RepeatKey);
            if (repeatSetting is not null && Enum.TryParse<RepeatMode>(repeatSetting.Value, out var repeat))
                while (Playback.Repeat != repeat) Playback.CycleRepeat();

            await LoadEqStateAsync(userId.Value);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load persisted music player settings");
        }
    }

    /// <summary>
    /// Restores the equalizer for this circuit. A device-local selection (localStorage) always
    /// wins; the server-side user settings are only used as the fallback for machines that have
    /// no local state yet.
    /// </summary>
    private async Task LoadEqStateAsync(Guid userId)
    {
        _localEqState = await LoadLocalEqAsync();
        if (_localEqState is not null)
        {
            Playback.ApplyEqSnapshot(_localEqState.Bands, _localEqState.PresetId, isLocalOverride: true);
            return;
        }

        var eqSetting = await UserSettingsService.GetSettingAsync(userId, SettingsModule, EqBandsKey);
        if (eqSetting is not null)
        {
            var bands = JsonSerializer.Deserialize<double[]>(eqSetting.Value);
            if (bands is { Length: LocalEqState.BandCount })
            {
                Playback.ApplyEqSnapshot(bands, presetId: null, isLocalOverride: false);
                return;
            }
        }

        // No local state and no server band array: fall back to the user's server default preset.
        var caller = await GetCallerAsync();
        await Playback.LoadEqPresetsAsync(caller);
        if (Playback.DefaultPresetId is { } defaultId)
        {
            var preset = Playback.EqPresets.FirstOrDefault(p => p.Id == defaultId);
            if (preset is not null)
                Playback.ApplyPreset(preset);
        }
    }

    private async Task<LocalEqState?> LoadLocalEqAsync()
    {
        try
        {
            var json = await Js.InvokeAsync<string?>("localStorage.getItem", LocalEqStateKey);
            return LocalEqState.TryParse(json);
        }
        catch (Exception)
        {
            // localStorage is unavailable during prerendering or when blocked by the browser.
            return null;
        }
    }

    private async Task SaveLocalEqAsync()
    {
        var presetName = Playback.ActivePresetId.HasValue
            ? Playback.EqPresets.FirstOrDefault(p => p.Id == Playback.ActivePresetId)?.Name
              ?? _localEqState?.PresetName
            : null;

        _localEqState = new LocalEqState
        {
            PresetId = Playback.ActivePresetId,
            PresetName = presetName,
            Bands = Playback.EqBands.ToArray()
        };

        try
        {
            await Js.InvokeVoidAsync("localStorage.setItem", LocalEqStateKey, _localEqState.ToJson());
        }
        catch (Exception)
        {
            // localStorage unavailable — the server-side copy remains the fallback for new machines.
        }
    }

    private async Task SaveSettingImmediateAsync(string key, string value)
    {
        try
        {
            var userId = await GetUserIdAsync();
            if (userId is null) return;
            await UserSettingsService.UpsertSettingAsync(userId.Value, SettingsModule, key,
                new UpsertUserSettingDto { Value = value });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to save music setting {Key}", key);
        }
    }

    private void SaveSettingDebounced(string key, string value)
    {
        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        var token = _saveCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000, token);
                if (token.IsCancellationRequested) return;
                await InvokeAsync(async () => await SaveSettingImmediateAsync(key, value));
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private async Task<Guid?> GetUserIdAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var claim = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        Playback.OnChange -= OnPlaybackStateChanged;
        Playback.OnTrackChanged -= OnTrackChanged;
        ModuleRegistry.OnChange -= OnModuleRegistryChanged;
        Navigation.LocationChanged -= OnLocationChanged;
        _saveCts?.Cancel();
        _saveCts?.Dispose();

        // JS interop is not available while the component is statically rendered (e.g. the
        // static prerender/disposal pass of a page request), and calling it there used to throw
        // InvalidOperationException out of DisposeAsync — an unhandled exception on every page
        // request. Only talk to JS when the component is actually interactive.
        if (RendererInfo.IsInteractive)
        {
            try
            {
                // Restore the page's own tab title when the global playbar goes away (e.g. logout).
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.clearNowPlayingTitle");
            }
            catch (JSDisconnectedException) { }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }

            if (_jsInitialized)
            {
                try
                {
                    // Detach rather than dispose — keep audio playing if circuit is still alive
                    await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.detach");
                }
                catch (JSDisconnectedException) { }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }
        }

        _dotNetRef?.Dispose();
    }
}

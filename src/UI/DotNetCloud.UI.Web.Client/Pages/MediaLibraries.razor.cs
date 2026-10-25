using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Localization;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Client.Pages;

/// <summary>Code-behind for the <see cref="MediaLibraries"/> component.</summary>
public partial class MediaLibraries : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private bool _loading = true;
    private bool _scanning;
    private string? _scanningType;
    private string? _error;

    private IReadOnlyList<MediaLibrarySource> _photoSources = [];
    private IReadOnlyList<MediaLibrarySource> _musicSources = [];
    private IReadOnlyList<MediaLibrarySource> _videoSources = [];

    private MediaScanResultResponse? _lastPhotoResult;
    private MediaScanResultResponse? _lastMusicResult;
    private MediaScanResultResponse? _lastVideoResult;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var paths = await Api.GetMediaLibraryPathsAsync();
            if (paths is not null)
            {
                _photoSources = paths.PhotosSources;
                _musicSources = paths.MusicSources;
                _videoSources = paths.VideoSources;
            }
        }
        catch
        {
            Toast.ShowError("Failed to load media library settings.");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ScanAsync(string mediaType)
    {
        _scanning = true;
        _scanningType = mediaType;
        _error = null;
        StateHasChanged();

        try
        {
            var result = await Api.ScanMediaLibraryAsync(mediaType);

            switch (mediaType)
            {
                case "Photos": _lastPhotoResult = result; break;
                case "Music": _lastMusicResult = result; break;
                case "Video": _lastVideoResult = result; break;
            }

            if (result is not null && result.Failed > 0)
            {
                Toast.ShowWarning($"Scan complete with {result.Failed} errors. {result.Imported} files imported.");
            }
            else if (result is not null)
            {
                Toast.ShowSuccess($"Scan complete: {result.Imported} imported, {result.Skipped} already up to date.");
            }
        }
        catch (Exception ex)
        {
            _error = $"Scan failed: {ex.Message}";
            Toast.ShowError("Media library scan failed.");
        }
        finally
        {
            _scanning = false;
            _scanningType = null;
        }
    }

    private static string FormatResult(MediaScanResultResponse r) =>
        $"Found {r.TotalFound} · Imported {r.Imported} · Skipped {r.Skipped}" +
        (r.Removed > 0 ? $" · Removed {r.Removed}" : string.Empty) +
        (r.Failed > 0 ? $" · Failed {r.Failed}" : string.Empty);

    private static string GetSourceKindLabel(MediaLibrarySource source)
        => source.SourceKind == MediaLibrarySourceKind.SharedMount ? "Shared" : "Owned";
}

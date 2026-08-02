using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.UI;

/// <summary>
/// Code-behind for the <see cref="VideoMetadataEditDialog"/> component.
/// Lets the user manually correct a video's displayed metadata when automatic
/// TMDB enrichment matched the wrong movie (parity with the Music module's
/// manual metadata correction flow).
/// </summary>
public partial class VideoMetadataEditDialog
{
    /// <summary>The video whose metadata is being edited.</summary>
    [Parameter, EditorRequired] public required VideoDto Video { get; set; }

    /// <summary>Raised after a successful save with the updated video.</summary>
    [Parameter] public EventCallback<VideoDto?> OnSaved { get; set; }

    /// <summary>Raised when the dialog is dismissed (cancel or close).</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    [Inject] private IVideoService VideoService { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;
    [Inject] private ILogger<VideoMetadataEditDialog> Logger { get; set; } = null!;

    private string _title = string.Empty;
    private string _overview = string.Empty;
    private string _genres = string.Empty;
    private DateTime? _releaseDate;
    private string _tagline = string.Empty;
    private bool _saving;
    private string? _error;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        _title = Video.Title;
        _overview = Video.Overview ?? string.Empty;
        _genres = Video.Genres ?? string.Empty;
        _releaseDate = Video.ReleaseDate;
        _tagline = Video.TmdbTagline ?? string.Empty;
        _error = null;
    }

    private async Task SaveAsync()
    {
        if (_saving)
            return;

        _saving = true;
        _error = null;
        StateHasChanged();

        try
        {
            var caller = await GetCallerAsync();
            var dto = new UpdateVideoDetailsDto
            {
                Title = _title.Trim(),
                Overview = _overview.Trim(),
                Genres = _genres.Trim(),
                ReleaseDate = _releaseDate,
                Tagline = _tagline.Trim(),
            };

            var updated = await VideoService.UpdateVideoDetailsAsync(Video.Id, dto, caller);
            if (updated is not null)
            {
                await OnSaved.InvokeAsync(updated);
            }
            else
            {
                _error = "Video not found. It may have been deleted.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving video metadata for {VideoId}", Video.Id);
            _error = "Failed to save metadata. Please try again.";
        }
        finally
        {
            _saving = false;
            StateHasChanged();
        }
    }

    private async Task HandleClose()
    {
        if (_saving)
            return;
        await OnClose.InvokeAsync();
    }

    private async Task<CallerContext> GetCallerAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? throw new UnauthorizedAccessException("Not authenticated.");
        var roles = authState.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(Guid.Parse(userId), roles, CallerType.User);
    }
}

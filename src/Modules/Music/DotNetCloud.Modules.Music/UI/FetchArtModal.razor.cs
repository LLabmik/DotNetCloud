using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Music.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.UI;

/// <summary>
/// Code-behind for the FetchArtModal component.
/// Manages the multi-source album cover art search and selection flow.
/// </summary>
public partial class FetchArtModal
{
    [Parameter, EditorRequired] public required Guid AlbumId { get; set; }
    [Parameter, EditorRequired] public required string AlbumTitle { get; set; }
    [Parameter, EditorRequired] public required string ArtistName { get; set; }
    [Parameter] public int? Year { get; set; }
    [Parameter] public string? ArtistMbid { get; set; }
    [Parameter] public string? SanitizedAlbumTitle { get; set; }
    [Parameter] public string? SanitizedArtistName { get; set; }
    [Parameter, EditorRequired] public required CallerContext Caller { get; set; }
    [Parameter] public EventCallback<FetchArtModalResult> OnClose { get; set; }

    [Inject] private IMetadataEnrichmentService EnrichmentService { get; set; } = null!;
    [Inject] private ILogger<FetchArtModal> Logger { get; set; } = null!;

    private enum ModalState { Editing, Searching, NoResults, Results, Applying }

    private ModalState _state = ModalState.Editing;
    private string _editAlbumTitle = string.Empty;
    private string _editArtistName = string.Empty;
    private string? _editYear;
    private string? _sanitizedTitle;
    private string? _sanitizedArtist;
    private List<FetchArtSearchResult> _results = [];
    private int _selectedResultIndex = -1;
    private bool _isSearching;
    private bool _isApplying;
    private string? _errorMessage;

    protected override void OnInitialized()
    {
        _editAlbumTitle = AlbumTitle;
        _editArtistName = ArtistName;
        _editYear = Year?.ToString();
        _sanitizedTitle = SanitizedAlbumTitle;
        _sanitizedArtist = SanitizedArtistName;
    }

    private string GetTitle() => _state switch
    {
        ModalState.Editing => "Fetch Cover Art",
        ModalState.Searching => "Searching…",
        ModalState.NoResults => "No Results",
        ModalState.Results => $"Results ({_results.Count})",
        ModalState.Applying => "Applying…",
        _ => "Fetch Cover Art"
    };

    private async Task SelectResultByIndex(int index)
    {
        if (index >= 0 && index < _results.Count)
        {
            _selectedResultIndex = index;
            StateHasChanged();
        }
        await Task.CompletedTask;
    }

    private void GoBackToEditing()
    {
        _state = ModalState.Editing;
        _errorMessage = null;
        _selectedResultIndex = -1;
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_editAlbumTitle) || string.IsNullOrWhiteSpace(_editArtistName))
        {
            _errorMessage = "Album title and artist name are required.";
            return;
        }

        _isSearching = true;
        _errorMessage = null;
        _state = ModalState.Searching;
        _selectedResultIndex = -1;

        try
        {
            var request = new FetchArtSearchRequest
            {
                AlbumTitle = _editAlbumTitle.Trim(),
                ArtistName = _editArtistName.Trim(),
                Year = int.TryParse(_editYear, out var y) ? y : null,
                ArtistMbid = ArtistMbid
            };

            var results = await EnrichmentService.SearchArtCandidatesAsync(request);

            _results = results.ToList();

            if (_results.Count == 0)
            {
                _state = ModalState.NoResults;
            }
            else
            {
                _state = ModalState.Results;
                if (_results.Count == 1)
                {
                    _selectedResultIndex = 0;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to search for album art");
            _errorMessage = "Search failed. Please try again.";
            _state = ModalState.Editing;
        }
        finally
        {
            _isSearching = false;
        }
    }

    private async Task ApplySelectedAsync()
    {
        if (_selectedResultIndex < 0 || _selectedResultIndex >= _results.Count)
        {
            _errorMessage = "No result selected. Click a result card first.";
            StateHasChanged();
            return;
        }

        _isApplying = true;
        _errorMessage = null;
        _state = ModalState.Applying;
        StateHasChanged();

        try
        {
            var selected = _results[_selectedResultIndex];
            var request = new FetchArtApplyRequest
            {
                Source = selected.Source,
                SourceId = selected.SourceId,
                ThumbnailUrl = selected.ThumbnailUrl
            };

            var result = await EnrichmentService.ApplyArtSelectionAsync(AlbumId, request, Caller);

            if (result.Success)
            {
                await OnClose.InvokeAsync(new FetchArtModalResult { Success = true });
            }
            else
            {
                _errorMessage = result.ErrorMessage ?? "Failed to apply artwork.";
                _state = ModalState.Results;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to apply album art");
            _errorMessage = "Failed to apply artwork. Please try again.";
            _state = ModalState.Results;
        }
        finally
        {
            _isApplying = false;
        }
    }

    private async Task CancelAsync()
    {
        await OnClose.InvokeAsync(new FetchArtModalResult { Success = false });
    }
}

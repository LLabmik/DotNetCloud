using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Typeahead dropdown for @mentions in comments and descriptions.
/// Shows user search results when the user types @ in a text input.
/// </summary>
public partial class MentionTypeahead : ComponentBase, IDisposable
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    /// <summary>Callback when a user is selected from the dropdown.</summary>
    [Parameter] public EventCallback<UserSearchResult> OnUserSelected { get; set; }

    /// <summary>Callback when the dropdown should be dismissed.</summary>
    [Parameter] public EventCallback OnDismiss { get; set; }

    private bool _isVisible;
    private bool _isLoading;
    private string _searchTerm = "";
    private string _topPx = "0";
    private string _leftPx = "0";
    private int _selectedIndex = -1;
    private readonly List<UserSearchResult> _results = [];
    private CancellationTokenSource? _debounceCts;

    /// <summary>
    /// Shows the typeahead at the given position and begins searching.
    /// Called from the parent component when @ is typed.
    /// </summary>
    public async Task ShowAsync(string searchTerm, double topPx, double leftPx)
    {
        _searchTerm = searchTerm;
        _topPx = $"{topPx}px";
        _leftPx = $"{leftPx}px";
        _selectedIndex = -1;
        _isVisible = true;
        await DebouncedSearchAsync(searchTerm);
    }

    /// <summary>Hides the typeahead dropdown.</summary>
    public void Hide()
    {
        _isVisible = false;
        _searchTerm = "";
        _results.Clear();
        _selectedIndex = -1;
        CancelDebounce();
    }

    /// <summary>Handles keyboard navigation within the typeahead.</summary>
    public async Task HandleKeyDownAsync(string key)
    {
        if (!_isVisible || _results.Count == 0) return;

        switch (key)
        {
            case "ArrowDown":
                _selectedIndex = Math.Min(_selectedIndex + 1, _results.Count - 1);
                break;
            case "ArrowUp":
                _selectedIndex = Math.Max(_selectedIndex - 1, 0);
                break;
            case "Enter":
                if (_selectedIndex >= 0 && _selectedIndex < _results.Count)
                    await SelectUserAsync(_results[_selectedIndex]);
                break;
            case "Escape":
                Hide();
                await OnDismiss.InvokeAsync();
                break;
        }
    }

    private async Task SelectUserAsync(UserSearchResult user)
    {
        Hide();
        await OnUserSelected.InvokeAsync(user);
    }

    private static string GetInitials(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "?";
        var parts = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
    }

    private async Task DebouncedSearchAsync(string term)
    {
        CancelDebounce();
        _debounceCts = new CancellationTokenSource();
        var ct = _debounceCts.Token;

        try
        {
            await Task.Delay(300, ct); // 300ms debounce
            if (string.IsNullOrWhiteSpace(term) || ct.IsCancellationRequested) return;

            _isLoading = true;
            StateHasChanged();

            var results = await ApiClient.SearchUsersAsync(term, 8, ct);
            if (!ct.IsCancellationRequested)
            {
                _results.Clear();
                _results.AddRange(results);
                _isLoading = false;
                StateHasChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // Debounce cancelled — expected
        }
        catch
        {
            _isLoading = false;
            _results.Clear();
            StateHasChanged();
        }
    }

    private void CancelDebounce()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CancelDebounce();
    }
}

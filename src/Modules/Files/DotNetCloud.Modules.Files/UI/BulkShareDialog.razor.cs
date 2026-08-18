using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Dialog for sharing multiple files/folders with a single recipient at once.
/// The parent is responsible for creating the share on every target node.
/// </summary>
public partial class BulkShareDialog : ComponentBase
{
    /// <summary>The nodes being shared.</summary>
    [Parameter] public IReadOnlyList<FileNodeViewModel> Nodes { get; set; } = [];

    /// <summary>Raised when the dialog should close.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Search callback for users/teams/groups. Returns matching results.</summary>
    [Parameter] public Func<string, Task<IReadOnlyList<ShareSearchResult>>>? OnSearch { get; set; }

    /// <summary>Raised when the user confirms the share. Parent creates the share on all nodes.</summary>
    [Parameter] public EventCallback<BulkShareCreatedEventArgs> OnShareCreated { get; set; }

    private List<ShareSearchResult> _searchResults = [];
    private ShareSearchResult? _selectedSearchResult;
    private string _searchQuery = string.Empty;
    private string _newSharePermission = "Read";
    private string _note = string.Empty;
    private bool _isSearching;
    private bool _isCreatingShare;
    private string _shareErrorMessage = string.Empty;
    private bool _overlayMouseDown;

    protected IReadOnlyList<ShareSearchResult> SearchResults => _searchResults;
    protected ShareSearchResult? SelectedSearchResult => _selectedSearchResult;

    protected string SearchQuery
    {
        get => _searchQuery;
        set => _searchQuery = value;
    }

    protected string NewSharePermission
    {
        get => _newSharePermission;
        set => _newSharePermission = value;
    }

    protected string Note
    {
        get => _note;
        set => _note = value;
    }

    protected bool IsSearching => _isSearching;
    protected bool IsCreatingShare => _isCreatingShare;
    protected string ShareErrorMessage => _shareErrorMessage;

    protected async Task OnSearchInputAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery) || _searchQuery.Length < 2)
        {
            _searchResults = [];
            return;
        }

        if (OnSearch is null)
            return;

        _isSearching = true;
        StateHasChanged();

        var results = await OnSearch(_searchQuery);
        _searchResults = [.. results];
        _isSearching = false;
    }

    protected void SelectSearchResult(ShareSearchResult result)
    {
        _selectedSearchResult = result;
        _searchResults = [];
        _searchQuery = result.DisplayName;
    }

    protected async Task CreateShareAsync()
    {
        if (_selectedSearchResult is null)
            return;

        _shareErrorMessage = string.Empty;
        _isCreatingShare = true;
        StateHasChanged();

        await OnShareCreated.InvokeAsync(new BulkShareCreatedEventArgs
        {
            ShareType = _selectedSearchResult.ResultType,
            TargetId = _selectedSearchResult.Id,
            TargetName = _selectedSearchResult.DisplayName,
            Permission = _newSharePermission,
            Note = string.IsNullOrWhiteSpace(_note) ? null : _note
        });

        _isCreatingShare = false;
    }

    protected void HandleOverlayMouseDown() => _overlayMouseDown = true;

    protected void HandleOverlayClick()
    {
        if (_overlayMouseDown)
            Close();

        _overlayMouseDown = false;
    }

    protected async void Close() => await OnClose.InvokeAsync();
}

/// <summary>Event args raised when a bulk share is confirmed.</summary>
public sealed class BulkShareCreatedEventArgs
{
    /// <summary>Share type: "User", "Team", or "Group".</summary>
    public string ShareType { get; init; } = string.Empty;

    /// <summary>Target entity ID (user, team, or group).</summary>
    public Guid TargetId { get; init; }

    /// <summary>Display name of the target.</summary>
    public string TargetName { get; init; } = string.Empty;

    /// <summary>Permission level: "Read", "ReadWrite", or "Full".</summary>
    public string Permission { get; init; } = "Read";

    /// <summary>Expiration in days (0 = never).</summary>
    public int ExpirationDays { get; init; }

    /// <summary>Optional note.</summary>
    public string? Note { get; init; }
}

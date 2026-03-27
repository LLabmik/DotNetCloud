using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the comprehensive share dialog component.
/// Manages user/team/group search, existing share listing with inline edits,
/// and public link creation and configuration.
/// </summary>
public partial class ShareDialog : ComponentBase
{
    /// <summary>The file or folder node being shared.</summary>
    [Parameter] public FileNodeViewModel? Node { get; set; }

    /// <summary>Raised when the dialog should close.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>
    /// Raised when a new share is created. The caller receives the share type, target ID,
    /// permission, expiration days, and note.
    /// </summary>
    [Parameter] public EventCallback<ShareCreatedEventArgs> OnShareCreated { get; set; }

    /// <summary>Raised when an existing share's permission is changed.</summary>
    [Parameter] public EventCallback<ShareUpdatedEventArgs> OnShareUpdated { get; set; }

    /// <summary>Raised when a share is removed.</summary>
    [Parameter] public EventCallback<Guid> OnShareRemoved { get; set; }

    /// <summary>Raised when the public link is toggled on or off.</summary>
    [Parameter] public EventCallback<bool> OnPublicLinkToggled { get; set; }

    /// <summary>Existing shares for the node, supplied by the parent.</summary>
    [Parameter] public IReadOnlyList<ShareViewModel> InitialShares { get; set; } = [];

    /// <summary>Whether existing shares are still being loaded.</summary>
    [Parameter] public bool IsLoadingShares { get; set; }

    /// <summary>
    /// Callback to search for users, teams, and groups. Returns matching results.
    /// The parent component should wire this to an API call.
    /// </summary>
    [Parameter] public Func<string, Task<IReadOnlyList<ShareSearchResult>>>? OnSearch { get; set; }

    // ── Internal state ─────────────────────────────────────────────────────

    private List<ShareViewModel> _existingShares = [];
    private List<ShareSearchResult> _searchResults = [];
    private ShareSearchResult? _selectedSearchResult;
    private string _searchQuery = string.Empty;
    private string _newSharePermission = "Read";
    private int _expirationDays;
    private string _note = string.Empty;
    private bool _isSearching;
    private bool _isCreatingShare;
    private string _shareErrorMessage = string.Empty;

    // Public link state
    private bool _isPublicLinkEnabled;
    private ShareViewModel? _publicLinkShare;
    private string _publicLinkPermission = "Read";
    private string _linkPassword = string.Empty;
    private int _linkMaxDownloads;
    private int _linkExpirationDays = 30;
    private bool _isLinkCopied;
    private bool _overlayMouseDown;

    // ── Protected accessors for the template ───────────────────────────────

    protected IReadOnlyList<ShareViewModel> ExistingShares => _existingShares;
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

    protected int ExpirationDays
    {
        get => _expirationDays;
        set => _expirationDays = value;
    }

    protected string Note
    {
        get => _note;
        set => _note = value;
    }

    protected bool IsSearching => _isSearching;
    protected bool IsCreatingShare => _isCreatingShare;
    protected string ShareErrorMessage => _shareErrorMessage;

    protected bool IsPublicLinkEnabled
    {
        get => _isPublicLinkEnabled;
        set => _isPublicLinkEnabled = value;
    }

    protected ShareViewModel? PublicLinkShare => _publicLinkShare;

    protected string PublicLinkPermission
    {
        get => _publicLinkPermission;
        set => _publicLinkPermission = value;
    }

    protected string LinkPassword
    {
        get => _linkPassword;
        set => _linkPassword = value;
    }

    protected int LinkMaxDownloads
    {
        get => _linkMaxDownloads;
        set => _linkMaxDownloads = value;
    }

    protected int LinkExpirationDays
    {
        get => _linkExpirationDays;
        set => _linkExpirationDays = value;
    }

    protected bool IsLinkCopied => _isLinkCopied;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _existingShares = [.. InitialShares];
        _publicLinkShare = _existingShares.FirstOrDefault(s => s.ShareType == "PublicLink");
        _isPublicLinkEnabled = _publicLinkShare is not null;

        if (_publicLinkShare is not null)
        {
            _publicLinkPermission = _publicLinkShare.Permission;
            _linkMaxDownloads = _publicLinkShare.MaxDownloads ?? 0;
        }
    }

    // ── Search ─────────────────────────────────────────────────────────────

    /// <summary>Triggered when the search input value changes.</summary>
    protected async Task OnSearchInputAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery) || _searchQuery.Length < 2)
        {
            _searchResults = [];
            return;
        }

        if (OnSearch is null)
        {
            return;
        }

        _isSearching = true;
        StateHasChanged();

        var results = await OnSearch(_searchQuery);
        _searchResults = [.. results];
        _isSearching = false;
    }

    /// <summary>Handles keyboard navigation in the search results.</summary>
    protected void HandleSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            _searchResults = [];
            _searchQuery = string.Empty;
        }
    }

    /// <summary>Selects a search result as the share recipient.</summary>
    protected void SelectSearchResult(ShareSearchResult result)
    {
        _selectedSearchResult = result;
        _searchResults = [];
        _searchQuery = result.DisplayName;
    }

    /// <summary>Clears the currently selected search result.</summary>
    protected void ClearSelectedResult()
    {
        _selectedSearchResult = null;
        _searchQuery = string.Empty;
    }

    // ── Create share ───────────────────────────────────────────────────────

    /// <summary>Creates a share for the selected recipient.</summary>
    protected async Task CreateShareAsync()
    {
        if (_selectedSearchResult is null) return;

        _shareErrorMessage = string.Empty;
        _isCreatingShare = true;
        StateHasChanged();

        var args = new ShareCreatedEventArgs
        {
            ShareType = _selectedSearchResult.ResultType,
            TargetId = _selectedSearchResult.Id,
            TargetName = _selectedSearchResult.DisplayName,
            Permission = _newSharePermission,
            ExpirationDays = _expirationDays,
            Note = string.IsNullOrWhiteSpace(_note) ? null : _note
        };

        await OnShareCreated.InvokeAsync(args);

        // Add to local list for immediate UI feedback
        _existingShares.Add(new ShareViewModel
        {
            Id = Guid.NewGuid(),
            ShareType = args.ShareType,
            RecipientName = args.TargetName,
            Permission = args.Permission,
            ExpiresAt = args.ExpirationDays > 0
                ? DateTime.UtcNow.AddDays(args.ExpirationDays)
                : null,
            CreatedAt = DateTime.UtcNow,
            Note = args.Note
        });

        // Reset form
        _selectedSearchResult = null;
        _searchQuery = string.Empty;
        _note = string.Empty;
        _expirationDays = 0;
        _isCreatingShare = false;
    }

    // ── Existing share management ──────────────────────────────────────────

    /// <summary>Updates the permission level for an existing share.</summary>
    protected async Task UpdateSharePermissionAsync(ShareViewModel share, string newPermission)
    {
        if (share.Permission == newPermission) return;

        share.Permission = newPermission;
        await OnShareUpdated.InvokeAsync(new ShareUpdatedEventArgs
        {
            ShareId = share.Id,
            NewPermission = newPermission
        });
    }

    /// <summary>Removes an existing share.</summary>
    protected async Task RemoveShareAsync(ShareViewModel share)
    {
        _existingShares.Remove(share);
        await OnShareRemoved.InvokeAsync(share.Id);
    }

    // ── Public link ────────────────────────────────────────────────────────

    /// <summary>Toggles public link sharing on or off.</summary>
    protected async Task OnPublicLinkToggleAsync()
    {
        if (_isPublicLinkEnabled && _publicLinkShare is null)
        {
            // Create a public link placeholder — the parent will create the actual share
            _publicLinkShare = new ShareViewModel
            {
                Id = Guid.NewGuid(),
                ShareType = "PublicLink",
                RecipientName = "Public Link",
                Permission = "Read",
                LinkUrl = $"{Navigation.BaseUri.TrimEnd('/')}/s/{Guid.NewGuid():N}",
                CreatedAt = DateTime.UtcNow
            };
            _existingShares.Add(_publicLinkShare);
        }
        else if (!_isPublicLinkEnabled && _publicLinkShare is not null)
        {
            _existingShares.Remove(_publicLinkShare);
            await OnShareRemoved.InvokeAsync(_publicLinkShare.Id);
            _publicLinkShare = null;
        }

        await OnPublicLinkToggled.InvokeAsync(_isPublicLinkEnabled);
    }

    /// <summary>Updates public link settings (permission, max downloads, expiry).</summary>
    protected async Task UpdatePublicLinkAsync()
    {
        if (_publicLinkShare is null) return;

        await OnShareUpdated.InvokeAsync(new ShareUpdatedEventArgs
        {
            ShareId = _publicLinkShare.Id,
            NewPermission = _publicLinkPermission,
            NewMaxDownloads = _linkMaxDownloads > 0 ? _linkMaxDownloads : null,
            NewExpirationDays = _linkExpirationDays
        });
    }

    /// <summary>Sets a password on the public link.</summary>
    protected async Task SetLinkPasswordAsync()
    {
        if (_publicLinkShare is null || string.IsNullOrEmpty(_linkPassword)) return;

        await OnShareUpdated.InvokeAsync(new ShareUpdatedEventArgs
        {
            ShareId = _publicLinkShare.Id,
            NewPassword = _linkPassword
        });
        _linkPassword = string.Empty;
    }

    /// <summary>Removes the password from the public link.</summary>
    protected async Task RemoveLinkPasswordAsync()
    {
        if (_publicLinkShare is null) return;

        await OnShareUpdated.InvokeAsync(new ShareUpdatedEventArgs
        {
            ShareId = _publicLinkShare.Id,
            RemovePassword = true
        });
    }

    /// <summary>Copies the public link URL to clipboard via JS interop.</summary>
    protected async Task CopyPublicLink()
    {
        if (_publicLinkShare?.LinkUrl is not null)
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", _publicLinkShare.LinkUrl);
        }

        _isLinkCopied = true;
    }

    // ── Dialog ─────────────────────────────────────────────────────────────

    /// <summary>Tracks that the mousedown originated on the overlay (not inside the dialog).</summary>
    protected void HandleOverlayMouseDown() => _overlayMouseDown = true;

    /// <summary>Closes the dialog only when both mousedown and click occurred on the overlay.</summary>
    protected void HandleOverlayClick()
    {
        if (_overlayMouseDown)
        {
            Close();
        }

        _overlayMouseDown = false;
    }

    /// <summary>Closes the share dialog.</summary>
    protected async void Close()
    {
        await OnClose.InvokeAsync();
    }
}

/// <summary>
/// Event args raised when a new share is created via the dialog.
/// </summary>
public sealed class ShareCreatedEventArgs
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

/// <summary>
/// Event args raised when an existing share is updated.
/// </summary>
public sealed class ShareUpdatedEventArgs
{
    /// <summary>ID of the share being updated.</summary>
    public Guid ShareId { get; init; }

    /// <summary>New permission level (null = keep current).</summary>
    public string? NewPermission { get; init; }

    /// <summary>New max downloads (null = keep current).</summary>
    public int? NewMaxDownloads { get; init; }

    /// <summary>New expiration in days (0 = never, null = keep current).</summary>
    public int? NewExpirationDays { get; init; }

    /// <summary>New password to set (null = keep current).</summary>
    public string? NewPassword { get; init; }

    /// <summary>Whether to remove the existing password.</summary>
    public bool RemovePassword { get; init; }
}

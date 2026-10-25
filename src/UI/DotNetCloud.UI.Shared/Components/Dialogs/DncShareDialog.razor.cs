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
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace DotNetCloud.UI.Shared.Components.Dialogs;

/// <summary>Code-behind for the <see cref="DncShareDialog"/> component.</summary>
public partial class DncShareDialog : ComponentBase
{
    // ── Parameters ────────────────────────────────────────────────────────────

    /// <summary>Name of the shared item (file name, note title, etc.).</summary>
    [Parameter] public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Number of items being shared. When greater than one the dialog runs in bulk
    /// mode: existing shares and the public link section are hidden and the share
    /// button reads "Share with N items".
    /// </summary>
    [Parameter] public int ItemCount { get; set; } = 1;

    /// <summary>Raised when the dialog should close.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>
    /// Raised when a new share is created. The caller persists the share and
    /// re-supplies <see cref="ExistingShares"/> so the dialog can reconcile.
    /// </summary>
    [Parameter] public EventCallback<DncShareCreatedEventArgs> OnShareCreated { get; set; }

    /// <summary>
    /// Raised when an existing share is removed.
    /// </summary>
    [Parameter] public EventCallback<Guid> OnShareRemoved { get; set; }

    /// <summary>
    /// Raised when an existing share's permission or public-link settings change.
    /// </summary>
    [Parameter] public EventCallback<DncShareUpdatedEventArgs> OnShareUpdated { get; set; }

    /// <summary>
    /// Whether inline permission editing of existing shares is allowed. When
    /// <c>false</c> the existing-shares list is revoke-only (permission shown as text)
    /// and the public link settings section is hidden.
    /// </summary>
    [Parameter] public bool AllowPermissionEditing { get; set; } = true;

    /// <summary>Raised when the public link is toggled on or off (Files only).</summary>
    [Parameter] public EventCallback<bool> OnPublicLinkToggled { get; set; }

    /// <summary>Existing shares for the item, supplied by the parent.</summary>
    [Parameter] public IReadOnlyList<DncShareEntry> ExistingShares { get; set; } = [];

    /// <summary>Whether existing shares are still being loaded.</summary>
    [Parameter] public bool IsLoadingShares { get; set; }

    /// <summary>
    /// Optional custom search callback (users/teams/groups). When <c>null</c> the
    /// dialog uses the shared <see cref="IShareRecipientSearchService"/>.
    /// </summary>
    [Parameter] public Func<string, Task<IReadOnlyList<DncShareRecipient>>>? OnSearch { get; set; }

    /// <summary>Permission choices for new shares and inline editing.</summary>
    [Parameter] public IReadOnlyList<DncSharePermissionOption> PermissionOptions { get; set; } =
        [new() { Value = "Read", Label = "View only" },
         new() { Value = "ReadWrite", Label = "Can edit" },
         new() { Value = "Full", Label = "Full access" }];

    /// <summary>Permission choices for the public link (Files only).</summary>
    [Parameter] public IReadOnlyList<DncSharePermissionOption> PublicLinkPermissionOptions { get; set; } =
        [new() { Value = "Read", Label = "View only" },
         new() { Value = "ReadWrite", Label = "Can edit" }];

    /// <summary>Whether to show the public link section (Files only).</summary>
    [Parameter] public bool EnablePublicLink { get; set; }

    /// <summary>Whether to show the expiry + note fields on the new-share form (Files).</summary>
    [Parameter] public bool ShowNewShareExtras { get; set; }

    // ── Internal state ─────────────────────────────────────────────────────

    private List<DncShareRecipient> _searchResults = [];
    private DncShareRecipient? _selectedSearchResult;
    private string _searchQuery = string.Empty;
    private string _newSharePermission = "Read";
    private int _expirationDays;
    private string _note = string.Empty;
    private bool _isSearching;
    private bool _isCreatingShare;
    private string _shareErrorMessage = string.Empty;

    // Public link state
    private bool _isPublicLinkEnabled;
    private DncShareEntry? _publicLinkShare;
    private string _publicLinkPermission = "Read";
    private string _linkPassword = string.Empty;
    private int _linkMaxDownloads;
    private int _linkExpirationDays = 30;
    private bool _isLinkCopied;
    private bool _overlayMouseDown;

    private bool IsBulkMode => ItemCount > 1;

    private string HeaderText => IsBulkMode
        ? $"Share {ItemCount} items"
        : $"Share \"{ItemName}\"";

    // ── Protected accessors for the template ───────────────────────────────

    protected IReadOnlyList<DncShareRecipient> SearchResults => _searchResults;
    protected DncShareRecipient? SelectedSearchResult => _selectedSearchResult;
    protected DncShareEntry? PublicLinkShare => _publicLinkShare;
    protected bool IsSearching => _isSearching;
    protected bool IsCreatingShare => _isCreatingShare;
    protected string ShareErrorMessage => _shareErrorMessage;
    protected bool IsLinkCopied => _isLinkCopied;

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

    protected bool IsPublicLinkEnabled
    {
        get => _isPublicLinkEnabled;
        set => _isPublicLinkEnabled = value;
    }

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

    // ── Lifecycle ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _publicLinkShare = ExistingShares.FirstOrDefault(s => s.RecipientType == "PublicLink");
        _isPublicLinkEnabled = _publicLinkShare is not null;

        if (_publicLinkShare is not null)
        {
            _publicLinkPermission = _publicLinkShare.Permission;
            _linkMaxDownloads = _publicLinkShare.MaxDownloads ?? 0;
            _linkExpirationDays = _publicLinkShare.ExpiresAt is { } expiresAt
                ? Math.Max(0, (int)Math.Ceiling((expiresAt - DateTime.UtcNow).TotalDays))
                : 0;
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

        _isSearching = true;
        StateHasChanged();

        try
        {
            if (OnSearch is not null)
            {
                var results = await OnSearch(_searchQuery);
                _searchResults = [.. results];
            }
            else
            {
                var results = await ShareSearchService.SearchAsync(_searchQuery);
                _searchResults = [.. results];
            }
        }
        catch (Exception)
        {
            _searchResults = [];
        }
        finally
        {
            _isSearching = false;
        }
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
    protected void SelectSearchResult(DncShareRecipient result)
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
        if (_selectedSearchResult is null)
            return;

        _shareErrorMessage = string.Empty;
        _isCreatingShare = true;
        StateHasChanged();

        try
        {
            var args = new DncShareCreatedEventArgs
            {
                ShareType = _selectedSearchResult.RecipientType,
                TargetId = _selectedSearchResult.Id,
                TargetName = _selectedSearchResult.DisplayName,
                Permission = _newSharePermission,
                ExpirationDays = _expirationDays,
                Note = string.IsNullOrWhiteSpace(_note) ? null : _note,
            };

            await OnShareCreated.InvokeAsync(args);

            // Reset the form. The parent re-supplies ExistingShares with the persisted
            // share so the optimistic entry below is replaced by the real one.
            _selectedSearchResult = null;
            _searchQuery = string.Empty;
            _note = string.Empty;
            _expirationDays = 0;
        }
        catch (Exception ex)
        {
            _shareErrorMessage = ex.Message;
        }
        finally
        {
            _isCreatingShare = false;
        }
    }

    // ── Existing share management ──────────────────────────────────────────

    /// <summary>Updates the permission level for an existing share.</summary>
    protected async Task UpdateSharePermissionAsync(DncShareEntry share, string newPermission)
    {
        if (share.Permission == newPermission || !AllowPermissionEditing)
            return;

        var oldPermission = share.Permission;
        share.Permission = newPermission;

        try
        {
            await OnShareUpdated.InvokeAsync(new DncShareUpdatedEventArgs
            {
                ShareId = share.ShareId,
                NewPermission = newPermission,
            });
        }
        catch (Exception)
        {
            share.Permission = oldPermission;
            throw;
        }
    }

    /// <summary>Removes an existing share.</summary>
    protected async Task RemoveShareAsync(DncShareEntry share)
    {
        try
        {
            await OnShareRemoved.InvokeAsync(share.ShareId);
        }
        catch (Exception ex)
        {
            _shareErrorMessage = ex.Message;
            StateHasChanged();
        }
    }

    // ── Public link ────────────────────────────────────────────────────────

    /// <summary>Toggles public link sharing on or off.</summary>
    protected async Task OnPublicLinkToggleAsync()
    {
        try
        {
            await OnPublicLinkToggled.InvokeAsync(_isPublicLinkEnabled);
        }
        catch (Exception ex)
        {
            _shareErrorMessage = ex.Message;
            StateHasChanged();
        }
    }

    /// <summary>Updates public link settings (permission, max downloads, expiry).</summary>
    protected async Task UpdatePublicLinkAsync()
    {
        if (_publicLinkShare is null || !AllowPermissionEditing)
            return;

        try
        {
            await OnShareUpdated.InvokeAsync(new DncShareUpdatedEventArgs
            {
                ShareId = _publicLinkShare.ShareId,
                NewPermission = _publicLinkPermission,
                NewMaxDownloads = _linkMaxDownloads > 0 ? _linkMaxDownloads : null,
                NewExpirationDays = _linkExpirationDays,
            });
        }
        catch (Exception ex)
        {
            _shareErrorMessage = ex.Message;
            StateHasChanged();
        }
    }

    /// <summary>Sets a password on the public link.</summary>
    protected async Task SetLinkPasswordAsync()
    {
        if (_publicLinkShare is null || !AllowPermissionEditing || string.IsNullOrEmpty(_linkPassword))
            return;

        try
        {
            await OnShareUpdated.InvokeAsync(new DncShareUpdatedEventArgs
            {
                ShareId = _publicLinkShare.ShareId,
                NewPassword = _linkPassword,
            });
            _linkPassword = string.Empty;
        }
        catch (Exception ex)
        {
            _shareErrorMessage = ex.Message;
            StateHasChanged();
        }
    }

    /// <summary>Removes the password from the public link.</summary>
    protected async Task RemoveLinkPasswordAsync()
    {
        if (_publicLinkShare is null || !AllowPermissionEditing)
            return;

        try
        {
            await OnShareUpdated.InvokeAsync(new DncShareUpdatedEventArgs
            {
                ShareId = _publicLinkShare.ShareId,
                RemovePassword = true,
            });
        }
        catch (Exception ex)
        {
            _shareErrorMessage = ex.Message;
            StateHasChanged();
        }
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

    // ── Helpers ────────────────────────────────────────────────────────────

    private string GetPermissionLabel(string permission)
        => PermissionOptions.FirstOrDefault(o => o.Value == permission)?.Label ?? permission;

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
    protected async void Close() => await OnClose.InvokeAsync();
}

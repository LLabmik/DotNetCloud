using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the "Shared by me" view component.
/// Displays all files and folders the current user has shared with others,
/// showing recipients, permissions, and inline share management.
/// </summary>
public partial class SharedByMeView : ComponentBase
{
    /// <summary>Items shared by the current user.</summary>
    [Parameter] public IReadOnlyList<SharedItemViewModel> Items { get; set; } = [];

    /// <summary>Whether items are currently being loaded.</summary>
    [Parameter] public bool IsLoading { get; set; }

    /// <summary>Raised when the user wants to manage a share (opens share dialog for the node).</summary>
    [Parameter] public EventCallback<SharedItemViewModel> OnManageShare { get; set; }

    /// <summary>Raised when the user revokes a share.</summary>
    [Parameter] public EventCallback<SharedItemViewModel> OnRevokeShare { get; set; }

    /// <summary>Raised when the user changes a share's permission inline.</summary>
    [Parameter] public EventCallback<SharePermissionChangedEventArgs> OnPermissionChanged { get; set; }

    /// <summary>Raised when the user copies a public link.</summary>
    [Parameter] public EventCallback<SharedItemViewModel> OnCopyLink { get; set; }

    private string _sortColumn = "Date";
    private bool _sortAscending;

    /// <summary>Items sorted by the current sort column and direction.</summary>
    protected IReadOnlyList<SharedItemViewModel> SortedItems
    {
        get
        {
            IEnumerable<SharedItemViewModel> ordered = (_sortColumn, _sortAscending) switch
            {
                ("Name", true) => Items.OrderBy(i => i.NodeName, StringComparer.OrdinalIgnoreCase),
                ("Name", false) => Items.OrderByDescending(i => i.NodeName, StringComparer.OrdinalIgnoreCase),
                ("Date", true) => Items.OrderBy(i => i.SharedAt),
                _ => Items.OrderByDescending(i => i.SharedAt),
            };
            return [.. ordered];
        }
    }

    /// <summary>Sets the active sort column; toggles direction when already active.</summary>
    protected void SetSort(string column)
    {
        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = column != "Date";
        }
    }

    /// <summary>Returns the CSS class for a sort header (active/inactive).</summary>
    protected string SortHeaderClass(string column) =>
        _sortColumn == column ? "sort-header--active" : string.Empty;

    /// <summary>Returns the sort direction indicator for a column header.</summary>
    protected string SortIndicator(string column) =>
        _sortColumn != column ? string.Empty : _sortAscending ? "▲" : "▼";

    /// <summary>Opens the share management dialog for an item.</summary>
    protected async Task ManageShare(SharedItemViewModel item)
    {
        await OnManageShare.InvokeAsync(item);
    }

    /// <summary>Revokes an existing share.</summary>
    protected async Task RevokeShare(SharedItemViewModel item)
    {
        await OnRevokeShare.InvokeAsync(item);
    }

    /// <summary>Updates the permission level for a share inline.</summary>
    protected async Task UpdatePermission(SharedItemViewModel item, string newPermission)
    {
        if (item.Permission == newPermission) return;

        await OnPermissionChanged.InvokeAsync(new SharePermissionChangedEventArgs
        {
            ShareId = item.ShareId,
            NewPermission = newPermission
        });
    }

    /// <summary>Copies a public link to clipboard.</summary>
    protected async Task CopyLink(SharedItemViewModel item)
    {
        await OnCopyLink.InvokeAsync(item);
    }

    /// <summary>Returns an icon placeholder based on node type and MIME type.</summary>
    protected static string GetNodeIcon(SharedItemViewModel item)
    {
        if (item.NodeType == "Folder") return "[Folder]";
        if (item.MimeType is null) return "[File]";
        if (item.MimeType.StartsWith("image/")) return "[Image]";
        if (item.MimeType.StartsWith("video/")) return "[Video]";
        if (item.MimeType.StartsWith("audio/")) return "[Audio]";
        if (item.MimeType == "application/pdf") return "[PDF]";
        if (item.MimeType.Contains("document") || item.MimeType.Contains("word")) return "[Doc]";
        if (item.MimeType.Contains("spreadsheet") || item.MimeType.Contains("excel")) return "[Sheet]";
        return "[File]";
    }

    /// <summary>Formats a file size in human-readable units.</summary>
    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    /// <summary>Formats a future date as a relative time string.</summary>
    protected static string FormatRelativeDate(DateTime dateUtc)
    {
        var diff = dateUtc - DateTime.UtcNow;
        if (diff.TotalDays < 0) return "expired";
        if (diff.TotalDays < 1) return "today";
        if (diff.TotalDays < 2) return "tomorrow";
        if (diff.TotalDays < 7) return $"in {(int)diff.TotalDays} days";
        if (diff.TotalDays < 30) return $"in {(int)(diff.TotalDays / 7)} weeks";
        return dateUtc.ToString("MMM d, yyyy");
    }
}

/// <summary>
/// Event args raised when a share's permission is changed inline.
/// </summary>
public sealed class SharePermissionChangedEventArgs
{
    /// <summary>ID of the share being updated.</summary>
    public Guid ShareId { get; init; }

    /// <summary>New permission level: "Read", "ReadWrite", or "Full".</summary>
    public string NewPermission { get; init; } = "Read";
}

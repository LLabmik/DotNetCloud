using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the "Shared with me" view component.
/// Displays all files and folders shared with the current user,
/// grouped by share source with permission levels and accept/decline actions.
/// </summary>
public partial class SharedWithMeView : ComponentBase
{
    /// <summary>Items shared with the current user.</summary>
    [Parameter] public IReadOnlyList<SharedItemViewModel> Items { get; set; } = [];

    /// <summary>Whether items are currently being loaded.</summary>
    [Parameter] public bool IsLoading { get; set; }

    /// <summary>Raised when the user opens a shared item (navigates to it).</summary>
    [Parameter] public EventCallback<SharedItemViewModel> OnOpenItem { get; set; }

    /// <summary>Raised when the user declines/removes a share.</summary>
    [Parameter] public EventCallback<SharedItemViewModel> OnDeclineShare { get; set; }

    private string _groupBy = "sharer";

    /// <summary>Current grouping mode: "sharer", "type", or "none".</summary>
    protected string GroupBy
    {
        get => _groupBy;
        set => _groupBy = value;
    }

    /// <summary>Items grouped by the person who shared them.</summary>
    protected IReadOnlyDictionary<string, List<SharedItemViewModel>> GroupedBySharer
    {
        get
        {
            var groups = new Dictionary<string, List<SharedItemViewModel>>();
            foreach (var item in Items.OrderByDescending(i => i.SharedAt))
            {
                var key = string.IsNullOrEmpty(item.SharedByName) ? "Unknown" : item.SharedByName;
                if (!groups.TryGetValue(key, out var list))
                {
                    list = [];
                    groups[key] = list;
                }
                list.Add(item);
            }
            return groups;
        }
    }

    /// <summary>Items sorted by share date (newest first) without grouping.</summary>
    protected IReadOnlyList<SharedItemViewModel> SortedItems =>
        [.. Items.OrderByDescending(i => i.SharedAt)];

    /// <summary>Opens a shared item.</summary>
    protected async Task OpenItem(SharedItemViewModel item)
    {
        await OnOpenItem.InvokeAsync(item);
    }

    /// <summary>Declines/removes a share.</summary>
    protected async Task DeclineShare(SharedItemViewModel item)
    {
        await OnDeclineShare.InvokeAsync(item);
    }

    /// <summary>Returns a human-readable permission label.</summary>
    protected static string GetPermissionLabel(string permission) => permission switch
    {
        "Read" => "View only",
        "ReadWrite" => "Can edit",
        "Full" => "Full access",
        _ => permission
    };

    /// <summary>Returns an icon name based on node type and MIME type.</summary>
    protected static string GetNodeIcon(SharedItemViewModel item)
    {
        if (item.NodeType == "Folder")
            return "folder";
        if (item.MimeType is null)
            return "description";
        if (item.MimeType.StartsWith("image/"))
            return "image";
        if (item.MimeType.StartsWith("video/"))
            return "movie";
        if (item.MimeType.StartsWith("audio/"))
            return "music_note";
        if (item.MimeType.StartsWith("text/"))
            return "text_snippet";
        if (item.MimeType == "application/pdf")
            return "picture_as_pdf";
        if (item.MimeType.Contains("spreadsheet") || item.MimeType.Contains("excel"))
            return "table_chart";
        if (item.MimeType.Contains("presentation") || item.MimeType.Contains("powerpoint"))
            return "slideshow";
        if (item.MimeType.Contains("document") || item.MimeType.Contains("word"))
            return "article";
        if (item.MimeType.Contains("zip") || item.MimeType.Contains("compressed"))
            return "folder_zip";
        return "description";
    }

    /// <summary>Formats a file size in human-readable units.</summary>
    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    /// <summary>Formats a future date as a relative time string.</summary>
    protected static string FormatRelativeDate(DateTime dateUtc)
    {
        var diff = dateUtc - DateTime.UtcNow;
        if (diff.TotalDays < 0)
            return "expired";
        if (diff.TotalDays < 1)
            return "today";
        if (diff.TotalDays < 2)
            return "tomorrow";
        if (diff.TotalDays < 7)
            return $"in {(int)diff.TotalDays} days";
        if (diff.TotalDays < 30)
            return $"in {(int)(diff.TotalDays / 7)} weeks";
        return dateUtc.ToString("MMM d, yyyy");
    }
}

using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the trash bin component.
/// </summary>
public partial class TrashBin : ComponentBase
{
    private List<TrashItemViewModel> _trashedItems = [];

    protected IReadOnlyList<TrashItemViewModel> TrashedItems => _trashedItems;

    protected void RestoreItem(Guid itemId)
    {
        _trashedItems.RemoveAll(i => i.Id == itemId);
    }

    protected void PurgeItem(Guid itemId)
    {
        _trashedItems.RemoveAll(i => i.Id == itemId);
    }

    protected void EmptyTrash()
    {
        _trashedItems.Clear();
    }

    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}

using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Displays soft-deleted work items (trash) for a product with restore and permanent delete actions.
/// </summary>
public partial class WorkItemTrashView : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public ProductDto Product { get; set; } = default!;
    [Parameter] public EventCallback OnChanged { get; set; }
    [Parameter] public bool IsAdmin { get; set; }

    private readonly List<WorkItemDto> _deletedItems = [];
    private bool _showDeleted;
    private bool _deletedLoaded;
    private Guid? _restoringId;
    private Guid? _deletingId;
    private bool _isEmptyingTrash;
    private bool _showEmptyConfirm;

    protected override async Task OnParametersSetAsync()
    {
        await LoadDeletedItemsAsync();
    }

    private async Task LoadDeletedItemsAsync()
    {
        if (_deletedLoaded)
            return;

        try
        {
            var items = await ApiClient.ListDeletedWorkItemsAsync(Product.Id);
            _deletedItems.Clear();
            _deletedItems.AddRange(items);
            _deletedLoaded = true;
        }
        catch
        {
            // Non-critical; silently skip
        }
    }

    private async Task RestoreItemAsync(WorkItemDto item)
    {
        _restoringId = item.Id;

        try
        {
            var restored = await ApiClient.RestoreWorkItemAsync(item.Id);
            if (restored is not null)
            {
                _deletedItems.Remove(item);
                await OnChanged.InvokeAsync();
            }
        }
        catch
        {
            // Can retry
        }
        finally
        {
            _restoringId = null;
        }
    }

    private async Task PermanentDeleteItemAsync(WorkItemDto item)
    {
        _deletingId = item.Id;

        try
        {
            await ApiClient.PermanentDeleteWorkItemAsync(item.Id);
            _deletedItems.Remove(item);
            await OnChanged.InvokeAsync();
        }
        catch
        {
            // Can retry
        }
        finally
        {
            _deletingId = null;
        }
    }

    private async Task EmptyTrashAsync()
    {
        _showEmptyConfirm = true;
    }

    private async Task ConfirmEmptyTrashAsync()
    {
        _isEmptyingTrash = true;

        try
        {
            await ApiClient.EmptyWorkItemTrashAsync(Product.Id);
            _deletedItems.Clear();
            _showEmptyConfirm = false;
            _showDeleted = false;
            _deletedLoaded = false;
            await OnChanged.InvokeAsync();
        }
        catch
        {
            // Can retry
        }
        finally
        {
            _isEmptyingTrash = false;
        }
    }

    private static string GetTypeIcon(WorkItemType type) => type switch
    {
        WorkItemType.Epic => "🌟",
        WorkItemType.Feature => "✨",
        WorkItemType.Item => "📋",
        WorkItemType.SubItem => "🔹",
        _ => "📌"
    };
}

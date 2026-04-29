using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Dialog shown when completing a sprint — shows summary, handles incomplete cards.
/// </summary>
public partial class SprintCompletionDialog : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public Guid EpicId { get; set; }
    [Parameter, EditorRequired] public SprintDto Sprint { get; set; } = default!;
    [Parameter, EditorRequired] public List<SprintDto> AvailableSprints { get; set; } = [];
    [Parameter] public EventCallback OnCompleted { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private SprintReportDto? _report;
    private readonly List<WorkItemDto> _incompleteItems = [];
    private bool _isLoading = true;
    private bool _isCompleting;
    private string _moveTarget = "backlog";

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;

        try
        {
            var reportTask = ApiClient.GetSprintReportAsync(Sprint.Id);
            var itemsTask = ApiClient.GetBacklogItemsAsync(EpicId);
            await Task.WhenAll(reportTask, itemsTask);

            _report = await reportTask;
            var allItems = await itemsTask;

            _incompleteItems.Clear();
            _incompleteItems.AddRange(allItems
                .Where(i => i.SprintId == Sprint.Id && !i.IsArchived));

            // Default to first available next sprint if any exist
            var nextSprint = AvailableSprints
                .FirstOrDefault(s => s.Id != Sprint.Id && s.Status != SprintStatus.Completed);
            if (nextSprint is not null)
            {
                _moveTarget = nextSprint.Id.ToString();
            }
        }
        catch
        {
            _report = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task CompleteAsync()
    {
        _isCompleting = true;

        try
        {
            // Move incomplete items first
            if (_incompleteItems.Count > 0)
            {
                if (_moveTarget == "backlog")
                {
                    // Remove each item from the sprint
                    foreach (var item in _incompleteItems)
                    {
                        await ApiClient.RemoveItemFromSprintAsync(Sprint.Id, item.Id);
                    }
                }
                else if (Guid.TryParse(_moveTarget, out var targetSprintId))
                {
                    // Move to another sprint: remove from current, add to target
                    foreach (var item in _incompleteItems)
                    {
                        await ApiClient.RemoveItemFromSprintAsync(Sprint.Id, item.Id);
                        await ApiClient.AddItemToSprintAsync(targetSprintId, item.Id);
                    }
                }
            }

            // Complete the sprint
            await ApiClient.CompleteSprintAsync(Sprint.Id);
            await OnCompleted.InvokeAsync();
        }
        catch
        {
            // If completion fails, stay on dialog
        }
        finally
        {
            _isCompleting = false;
        }
    }
}

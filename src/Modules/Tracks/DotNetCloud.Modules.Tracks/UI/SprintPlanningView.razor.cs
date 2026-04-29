using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static DotNetCloud.Core.DTOs.SprintStatus;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Side-by-side sprint planning view with product backlog (left) and sprint backlog (right).
/// </summary>
public partial class SprintPlanningView : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public Guid EpicId { get; set; }
    [Parameter, EditorRequired] public SprintDto Sprint { get; set; } = default!;
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<Guid> OnWorkItemSelected { get; set; }
    [Parameter] public EventCallback OnSprintChanged { get; set; }

    private readonly List<WorkItemDto> _sprintItems = [];
    private readonly List<WorkItemDto> _backlogItems = [];
    private bool _isLoading = true;
    private string _backlogSearch = string.Empty;

    // Target SP editing
    private bool _isEditingTarget;
    private string _editTargetSp = "0";
    private int? _targetStoryPoints;
    private int _loadVersion;

    protected override async Task OnParametersSetAsync()
    {
        if (!_isEditingTarget)
        {
            _targetStoryPoints = Sprint.TargetStoryPoints;
        }

        if (!_isEditingTarget)
        {
            _editTargetSp = (_targetStoryPoints ?? 0).ToString();
        }

        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        var loadVersion = Interlocked.Increment(ref _loadVersion);
        _isLoading = true;

        var sprintItems = new List<WorkItemDto>();
        var backlogItems = new List<WorkItemDto>();

        try
        {
            // Load all backlog items for the Epic, then filter to those assigned to this sprint.
            var allItems = await ApiClient.GetBacklogItemsAsync(EpicId);
            sprintItems.AddRange(allItems
                .Where(item => item.SprintId == Sprint.Id)
                .GroupBy(item => item.Id)
                .Select(g => g.First()));
        }
        catch
        {
            // Keep rendering available sections if one API call fails.
        }

        try
        {
            var loadedBacklogItems = await ApiClient.GetBacklogItemsAsync(EpicId);
            backlogItems.AddRange(loadedBacklogItems
                .Where(item => item.SprintId == null)
                .GroupBy(item => item.Id)
                .Select(g => g.First()));
        }
        catch
        {
            // Keep rendering available sections if one API call fails.
        }
        finally
        {
            if (loadVersion == _loadVersion)
            {
                _sprintItems.Clear();
                _sprintItems.AddRange(sprintItems);

                _backlogItems.Clear();
                _backlogItems.AddRange(backlogItems);

                _isLoading = false;
            }
        }
    }

    private List<WorkItemDto> GetFilteredBacklog()
    {
        var items = _backlogItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_backlogSearch))
        {
            var search = _backlogSearch.Trim();
            items = items.Where(item =>
                item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.ItemNumber.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return items.OrderByDescending(item => item.Priority).ThenBy(item => item.ItemNumber).ToList();
    }

    private async Task AddToSprintAsync(Guid itemId)
    {
        try
        {
            await ApiClient.AddItemToSprintAsync(Sprint.Id, itemId);

            var item = _backlogItems.FirstOrDefault(i => i.Id == itemId);
            if (item is not null)
            {
                _backlogItems.Remove(item);
                _sprintItems.Add(item);
            }

            await OnSprintChanged.InvokeAsync();
        }
        catch
        {
            // Silent fail — item stays in backlog
        }
    }

    private async Task RemoveFromSprintAsync(Guid itemId)
    {
        try
        {
            await ApiClient.RemoveItemFromSprintAsync(Sprint.Id, itemId);

            var item = _sprintItems.FirstOrDefault(i => i.Id == itemId);
            if (item is not null)
            {
                _sprintItems.Remove(item);
                _backlogItems.Add(item);
            }

            await OnSprintChanged.InvokeAsync();
        }
        catch
        {
            // Silent fail — item stays in sprint
        }
    }

    private async Task SaveTargetAsync()
    {
        _isEditingTarget = false;

        if (!int.TryParse(_editTargetSp, out var target) || target < 0) return;

        var normalizedTarget = target > 0 ? target : (int?)null;

        try
        {
            var updated = await ApiClient.UpdateSprintAsync(Sprint.Id, new UpdateSprintDto
            {
                Title = Sprint.Title,
                Goal = Sprint.Goal,
                StartDate = Sprint.StartDate,
                EndDate = Sprint.EndDate,
                TargetStoryPoints = normalizedTarget
            });

            _targetStoryPoints = updated?.TargetStoryPoints ?? normalizedTarget;
            _editTargetSp = (_targetStoryPoints ?? 0).ToString();

            await OnSprintChanged.InvokeAsync();
        }
        catch
        {
            // Silent fail
        }
    }

    private async Task HandleTargetKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SaveTargetAsync();
        }
        else if (e.Key == "Escape")
        {
            _isEditingTarget = false;
        }
    }

    private static string GetPriorityLabel(Priority priority) => priority switch
    {
        Priority.Urgent => "🔴 Urgent",
        Priority.High => "🟠 High",
        Priority.Medium => "🟡 Medium",
        Priority.Low => "🔵 Low",
        _ => "⚪ None"
    };

    private static string GetStatusBadgeClass(SprintStatus status) => status switch
    {
        SprintStatus.Active => "badge-success",
        SprintStatus.Planning => "badge-info",
        SprintStatus.Completed => "badge-secondary",
        _ => "badge-secondary"
    };
}

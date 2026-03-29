using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Sprint management panel — create, start, complete sprints.
/// </summary>
public partial class SprintPanel : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public Guid BoardId { get; set; }
    [Parameter, EditorRequired] public List<SprintDto> Sprints { get; set; } = [];
    [Parameter] public EventCallback OnSprintChanged { get; set; }

    private bool _showCreateDialog;
    private readonly SprintCreateModel _createModel = new();

    private void OpenCreateDialog()
    {
        _createModel.Title = "";
        _createModel.Goal = "";
        _createModel.StartDate = DateTime.Today;
        _createModel.EndDate = DateTime.Today.AddDays(14);
        _showCreateDialog = true;
    }

    private async Task CreateSprintAsync()
    {
        if (string.IsNullOrWhiteSpace(_createModel.Title)) return;

        await ApiClient.CreateSprintAsync(BoardId, new CreateSprintDto
        {
            Title = _createModel.Title.Trim(),
            Goal = string.IsNullOrWhiteSpace(_createModel.Goal) ? null : _createModel.Goal.Trim(),
            StartDate = _createModel.StartDate,
            EndDate = _createModel.EndDate
        });

        _showCreateDialog = false;
        await OnSprintChanged.InvokeAsync();
    }

    private async Task StartSprintAsync(Guid sprintId)
    {
        await ApiClient.StartSprintAsync(BoardId, sprintId);
        await OnSprintChanged.InvokeAsync();
    }

    private async Task CompleteSprintAsync(Guid sprintId)
    {
        await ApiClient.CompleteSprintAsync(BoardId, sprintId);
        await OnSprintChanged.InvokeAsync();
    }

    private async Task DeleteSprintAsync(Guid sprintId)
    {
        await ApiClient.DeleteSprintAsync(BoardId, sprintId);
        await OnSprintChanged.InvokeAsync();
    }

    private static string GetStatusBadgeClass(SprintStatus status) => status switch
    {
        SprintStatus.Active => "badge-success",
        SprintStatus.Completed => "badge-muted",
        _ => "badge-info"
    };

    private sealed class SprintCreateModel
    {
        public string Title { get; set; } = "";
        public string Goal { get; set; } = "";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}

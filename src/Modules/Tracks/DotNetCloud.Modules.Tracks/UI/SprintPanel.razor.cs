using System.ComponentModel.DataAnnotations;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Sprint management panel — create, start, complete sprints.
/// </summary>
public partial class SprintPanel : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private BrowserTimeProvider TimeProvider { get; set; } = default!;

    [Parameter, EditorRequired] public Guid BoardId { get; set; }
    [Parameter, EditorRequired] public List<SprintDto> Sprints { get; set; } = [];
    [Parameter] public EventCallback OnSprintChanged { get; set; }

    private bool _showCreateDialog;
    private bool _isCreating;
    private string? _createError;
    private readonly SprintCreateModel _createModel = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await TimeProvider.EnsureInitializedAsync();
            StateHasChanged();
        }
    }

    private void OpenCreateDialog()
    {
        _createModel.Title = "";
        _createModel.Goal = "";
        _createModel.StartDate = DateTime.Today;
        _createModel.EndDate = DateTime.Today.AddDays(14);
        _createError = null;
        _isCreating = false;
        _showCreateDialog = true;
    }

    private async Task CreateSprintAsync()
    {
        if (string.IsNullOrWhiteSpace(_createModel.Title)) return;

        _isCreating = true;
        _createError = null;

        try
        {
            await ApiClient.CreateSprintAsync(BoardId, new CreateSprintDto
            {
                Title = _createModel.Title.Trim(),
                Goal = string.IsNullOrWhiteSpace(_createModel.Goal) ? null : _createModel.Goal.Trim(),
                StartDate = _createModel.StartDate.HasValue ? _createModel.StartDate.Value.ToUniversalTime() : null,
                EndDate = _createModel.EndDate.HasValue ? _createModel.EndDate.Value.ToUniversalTime() : null
            });

            _showCreateDialog = false;
            await OnSprintChanged.InvokeAsync();
        }
        catch (Exception ex)
        {
            _createError = $"Failed to create sprint: {ex.Message}";
        }
        finally
        {
            _isCreating = false;
        }
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

    private sealed class SprintCreateModel : IValidatableObject
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title must be 200 characters or fewer.")]
        public string Title { get; set; } = "";

        public string Goal { get; set; } = "";

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
            {
                yield return new ValidationResult(
                    "End date must be on or after the start date.",
                    [nameof(EndDate)]);
            }
        }
    }
}

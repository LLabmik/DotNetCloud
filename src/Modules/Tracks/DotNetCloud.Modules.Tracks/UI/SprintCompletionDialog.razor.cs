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

    [Parameter, EditorRequired] public Guid BoardId { get; set; }
    [Parameter, EditorRequired] public SprintDto Sprint { get; set; } = default!;
    [Parameter, EditorRequired] public List<SprintDto> AvailableSprints { get; set; } = [];
    [Parameter] public EventCallback OnCompleted { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private SprintReportDto? _report;
    private readonly List<CardDto> _incompleteCards = [];
    private bool _isLoading = true;
    private bool _isCompleting;
    private string _moveTarget = "backlog";

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;

        try
        {
            var reportTask = ApiClient.GetSprintReportAsync(Sprint.Id);
            var cardsTask = ApiClient.GetSprintCardsAsync(BoardId, Sprint.Id);
            await Task.WhenAll(reportTask, cardsTask);

            _report = await reportTask;
            var allCards = await cardsTask;

            _incompleteCards.Clear();
            _incompleteCards.AddRange(allCards.Where(c => !c.IsArchived));

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
            // Move incomplete cards first
            if (_incompleteCards.Count > 0)
            {
                if (_moveTarget == "backlog")
                {
                    // Remove each card from the sprint
                    foreach (var card in _incompleteCards)
                    {
                        await ApiClient.RemoveCardFromSprintAsync(BoardId, Sprint.Id, card.Id);
                    }
                }
                else if (Guid.TryParse(_moveTarget, out var targetSprintId))
                {
                    // Batch move to another sprint: remove from current, add to target
                    var cardIds = _incompleteCards.Select(c => c.Id).ToList();
                    foreach (var card in _incompleteCards)
                    {
                        await ApiClient.RemoveCardFromSprintAsync(BoardId, Sprint.Id, card.Id);
                    }
                    await ApiClient.BatchAddCardsToSprintAsync(BoardId, targetSprintId, cardIds);
                }
            }

            // Complete the sprint
            await ApiClient.CompleteSprintAsync(BoardId, Sprint.Id);
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

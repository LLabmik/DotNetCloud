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

    [Parameter, EditorRequired] public Guid BoardId { get; set; }
    [Parameter, EditorRequired] public SprintDto Sprint { get; set; } = default!;
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<Guid> OnCardSelected { get; set; }
    [Parameter] public EventCallback OnSprintChanged { get; set; }

    private readonly List<CardDto> _sprintCards = [];
    private readonly List<CardDto> _backlogCards = [];
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

        var sprintCards = new List<CardDto>();
        var backlogCards = new List<CardDto>();

        try
        {
            var loadedSprintCards = await ApiClient.GetSprintCardsAsync(BoardId, Sprint.Id);
            sprintCards.AddRange(loadedSprintCards
                .GroupBy(c => c.Id)
                .Select(g => g.First()));
        }
        catch
        {
            // Keep rendering available sections if one API call fails.
        }

        try
        {
            var loadedBacklogCards = await ApiClient.GetBacklogCardsAsync(BoardId);
            backlogCards.AddRange(loadedBacklogCards
                .GroupBy(c => c.Id)
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
                _sprintCards.Clear();
                _sprintCards.AddRange(sprintCards);

                _backlogCards.Clear();
                _backlogCards.AddRange(backlogCards);

                _isLoading = false;
            }
        }
    }

    private List<CardDto> GetFilteredBacklog()
    {
        var cards = _backlogCards.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_backlogSearch))
        {
            var search = _backlogSearch.Trim();
            cards = cards.Where(c =>
                c.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.CardNumber.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return cards.OrderByDescending(c => c.Priority).ThenBy(c => c.CardNumber).ToList();
    }

    private async Task AddToSprintAsync(Guid cardId)
    {
        try
        {
            await ApiClient.AddCardToSprintAsync(BoardId, Sprint.Id, cardId);

            var card = _backlogCards.FirstOrDefault(c => c.Id == cardId);
            if (card is not null)
            {
                _backlogCards.Remove(card);
                _sprintCards.Add(card);
            }

            await OnSprintChanged.InvokeAsync();
        }
        catch
        {
            // Silent fail — card stays in backlog
        }
    }

    private async Task RemoveFromSprintAsync(Guid cardId)
    {
        try
        {
            await ApiClient.RemoveCardFromSprintAsync(BoardId, Sprint.Id, cardId);

            var card = _sprintCards.FirstOrDefault(c => c.Id == cardId);
            if (card is not null)
            {
                _sprintCards.Remove(card);
                _backlogCards.Add(card);
            }

            await OnSprintChanged.InvokeAsync();
        }
        catch
        {
            // Silent fail — card stays in sprint
        }
    }

    private async Task SaveTargetAsync()
    {
        _isEditingTarget = false;

        if (!int.TryParse(_editTargetSp, out var target) || target < 0) return;

        var normalizedTarget = target > 0 ? target : (int?)null;

        try
        {
            var updated = await ApiClient.UpdateSprintAsync(BoardId, Sprint.Id, new UpdateSprintDto
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

    private static string GetPriorityLabel(CardPriority priority) => priority switch
    {
        CardPriority.Urgent => "🔴 Urgent",
        CardPriority.High => "🟠 High",
        CardPriority.Medium => "🟡 Medium",
        CardPriority.Low => "🔵 Low",
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

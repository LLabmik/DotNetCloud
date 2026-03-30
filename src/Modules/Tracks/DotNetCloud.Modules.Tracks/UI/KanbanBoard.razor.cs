using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Full kanban board with drag-and-drop cards between swimlanes.
/// </summary>
public partial class KanbanBoard : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public BoardDto Board { get; set; } = default!;
    [Parameter, EditorRequired] public List<BoardSwimlaneDto> Swimlanes { get; set; } = [];
    [Parameter, EditorRequired] public Dictionary<Guid, List<CardDto>> CardsBySwimlane { get; set; } = new();
    [Parameter] public EventCallback<Guid> OnCardSelected { get; set; }
    [Parameter] public EventCallback<CardDto> OnCardMoved { get; set; }
    [Parameter] public EventCallback<CardDto> OnCardCreated { get; set; }
    [Parameter] public EventCallback<BoardSwimlaneDto> OnSwimlaneCreated { get; set; }
    [Parameter] public EventCallback<Guid> OnSwimlaneDeleted { get; set; }
    [Parameter] public EventCallback OnRefreshRequested { get; set; }

    // Filters
    private string _filterText = "";
    private string _priorityFilter = "";
    private string _labelFilter = "";

    // Card add
    private Guid? _addingCardToSwimlane;
    private string _newCardTitle = "";

    // Swimlane add
    private bool _showAddSwimlane;
    private string _newSwimlaneTitle = "";

    // Drag state
    private CardDto? _draggedCard;

    private IReadOnlyList<CardDto> GetFilteredCards(Guid swimlaneId)
    {
        if (!CardsBySwimlane.TryGetValue(swimlaneId, out var cards)) return [];

        IEnumerable<CardDto> filtered = cards.Where(c => !c.IsArchived && !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            var query = _filterText.Trim();
            filtered = filtered.Where(c => c.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(_priorityFilter) &&
            Enum.TryParse<CardPriority>(_priorityFilter, out var priority))
        {
            filtered = filtered.Where(c => c.Priority == priority);
        }

        if (!string.IsNullOrEmpty(_labelFilter) && Guid.TryParse(_labelFilter, out var labelId))
        {
            filtered = filtered.Where(c => c.Labels.Any(l => l.Id == labelId));
        }

        return filtered.ToList();
    }

    // ── Drag & Drop ─────────────────────────────────────────

    private void HandleDragStart(CardDto card) => _draggedCard = card;

    private void HandleDragOver() { /* Allow drop */ }

    private async Task HandleDropOnSwimlane(Guid targetSwimlaneId)
    {
        if (_draggedCard is null) return;

        var card = _draggedCard;
        _draggedCard = null;

        if (card.SwimlaneId == targetSwimlaneId) return;

        try
        {
            var targetCards = CardsBySwimlane.TryGetValue(targetSwimlaneId, out var tc) ? tc : [];
            var position = targetCards.Count > 0 ? targetCards.Max(c => c.Position) + 1000 : 1000;

            var moved = await ApiClient.MoveCardAsync(card.Id, new MoveCardDto
            {
                TargetSwimlaneId = targetSwimlaneId,
                Position = position
            });

            if (moved is not null)
            {
                await OnCardMoved.InvokeAsync(moved);
            }
        }
        catch
        {
            // Silently fail; user can refresh
        }
    }

    // ── Add Card ────────────────────────────────────────────

    private void BeginAddCard(Guid swimlaneId)
    {
        _addingCardToSwimlane = swimlaneId;
        _newCardTitle = "";
    }

    private void CancelAddCard()
    {
        _addingCardToSwimlane = null;
        _newCardTitle = "";
    }

    private async Task HandleAddCardKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SubmitNewCardAsync();
        else if (e.Key == "Escape") CancelAddCard();
    }

    private async Task SubmitNewCardAsync()
    {
        if (_addingCardToSwimlane is null || string.IsNullOrWhiteSpace(_newCardTitle)) return;

        try
        {
            var card = await ApiClient.CreateCardAsync(_addingCardToSwimlane.Value, new CreateCardDto
            {
                Title = _newCardTitle.Trim()
            });

            if (card is not null)
            {
                await OnCardCreated.InvokeAsync(card);
            }

            _newCardTitle = "";
        }
        catch
        {
            // Card creation failed
        }
    }

    // ── Add Swimlane ────────────────────────────────────────

    private async Task HandleAddSwimlaneKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SubmitNewSwimlaneAsync();
        else if (e.Key == "Escape") _showAddSwimlane = false;
    }

    private async Task SubmitNewSwimlaneAsync()
    {
        if (string.IsNullOrWhiteSpace(_newSwimlaneTitle)) return;

        try
        {
            var swimlane = await ApiClient.CreateSwimlaneAsync(Board.Id, new CreateBoardSwimlaneDto
            {
                Title = _newSwimlaneTitle.Trim()
            });

            if (swimlane is not null)
            {
                await OnSwimlaneCreated.InvokeAsync(swimlane);
            }

            _newSwimlaneTitle = "";
            _showAddSwimlane = false;
        }
        catch
        {
            // Swimlane creation failed
        }
    }

    private async Task DeleteSwimlaneAsync(Guid swimlaneId)
    {
        try
        {
            await ApiClient.DeleteSwimlaneAsync(Board.Id, swimlaneId);
            await OnSwimlaneDeleted.InvokeAsync(swimlaneId);
        }
        catch
        {
            // Deletion failed
        }
    }

    // ── Helpers ─────────────────────────────────────────────

    private static string GetPriorityClass(CardPriority priority) => priority switch
    {
        CardPriority.Urgent => "priority-urgent",
        CardPriority.High => "priority-high",
        CardPriority.Medium => "priority-medium",
        CardPriority.Low => "priority-low",
        _ => ""
    };

    private static string GetPriorityIcon(CardPriority priority) => priority switch
    {
        CardPriority.Urgent => "🔴",
        CardPriority.High => "🟠",
        CardPriority.Medium => "🟡",
        CardPriority.Low => "🟢",
        _ => ""
    };

    private static bool IsOverdue(DateTime dueDate) => dueDate < DateTime.UtcNow;

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name[..1].ToUpperInvariant();
    }
}

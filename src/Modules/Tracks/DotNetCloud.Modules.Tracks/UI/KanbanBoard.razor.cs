using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Full kanban board with drag-and-drop cards between lists.
/// </summary>
public partial class KanbanBoard : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public BoardDto Board { get; set; } = default!;
    [Parameter, EditorRequired] public List<BoardListDto> Lists { get; set; } = [];
    [Parameter, EditorRequired] public Dictionary<Guid, List<CardDto>> CardsByList { get; set; } = new();
    [Parameter] public EventCallback<Guid> OnCardSelected { get; set; }
    [Parameter] public EventCallback<CardDto> OnCardMoved { get; set; }
    [Parameter] public EventCallback<CardDto> OnCardCreated { get; set; }
    [Parameter] public EventCallback<BoardListDto> OnListCreated { get; set; }
    [Parameter] public EventCallback<Guid> OnListDeleted { get; set; }
    [Parameter] public EventCallback OnRefreshRequested { get; set; }

    // Filters
    private string _filterText = "";
    private string _priorityFilter = "";
    private string _labelFilter = "";

    // Card add
    private Guid? _addingCardToList;
    private string _newCardTitle = "";

    // List add
    private bool _showAddList;
    private string _newListTitle = "";

    // Drag state
    private CardDto? _draggedCard;

    private IReadOnlyList<CardDto> GetFilteredCards(Guid listId)
    {
        if (!CardsByList.TryGetValue(listId, out var cards)) return [];

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

    private async Task HandleDropOnList(Guid targetListId)
    {
        if (_draggedCard is null) return;

        var card = _draggedCard;
        _draggedCard = null;

        if (card.ListId == targetListId) return;

        try
        {
            var targetCards = CardsByList.TryGetValue(targetListId, out var tc) ? tc : [];
            var position = targetCards.Count > 0 ? targetCards.Max(c => c.Position) + 1000 : 1000;

            var moved = await ApiClient.MoveCardAsync(card.Id, new MoveCardDto
            {
                TargetListId = targetListId,
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

    private void BeginAddCard(Guid listId)
    {
        _addingCardToList = listId;
        _newCardTitle = "";
    }

    private void CancelAddCard()
    {
        _addingCardToList = null;
        _newCardTitle = "";
    }

    private async Task HandleAddCardKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SubmitNewCardAsync();
        else if (e.Key == "Escape") CancelAddCard();
    }

    private async Task SubmitNewCardAsync()
    {
        if (_addingCardToList is null || string.IsNullOrWhiteSpace(_newCardTitle)) return;

        try
        {
            var card = await ApiClient.CreateCardAsync(_addingCardToList.Value, new CreateCardDto
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

    // ── Add List ────────────────────────────────────────────

    private async Task HandleAddListKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SubmitNewListAsync();
        else if (e.Key == "Escape") _showAddList = false;
    }

    private async Task SubmitNewListAsync()
    {
        if (string.IsNullOrWhiteSpace(_newListTitle)) return;

        try
        {
            var list = await ApiClient.CreateListAsync(Board.Id, new CreateBoardListDto
            {
                Title = _newListTitle.Trim()
            });

            if (list is not null)
            {
                await OnListCreated.InvokeAsync(list);
            }

            _newListTitle = "";
            _showAddList = false;
        }
        catch
        {
            // List creation failed
        }
    }

    private async Task DeleteListAsync(Guid listId)
    {
        try
        {
            await ApiClient.DeleteListAsync(Board.Id, listId);
            await OnListDeleted.InvokeAsync(listId);
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

using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Backlog view showing cards not assigned to any sprint.
/// Supports filtering, multi-select, and bulk sprint assignment.
/// </summary>
public partial class BacklogView : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public Guid BoardId { get; set; }
    [Parameter, EditorRequired] public List<SprintDto> Sprints { get; set; } = [];
    [Parameter] public List<BoardSwimlaneDto> Swimlanes { get; set; } = [];
    [Parameter] public EventCallback<Guid> OnCardSelected { get; set; }
    [Parameter] public EventCallback OnBacklogChanged { get; set; }

    private readonly List<CardDto> _backlogCards = [];
    private readonly HashSet<Guid> _selectedCardIds = [];
    private bool _isLoading = true;
    private bool _isBulkAssigning;
    private bool _selectAll;
    private string _filterText = "";
    private string _priorityFilter = "";
    private string _bulkTargetSprintId = "";

    // ── Add Card State ──────────────────────────────────────
    private bool _isAddingCard;
    private bool _isSubmittingCard;
    private string _newCardTitle = "";
    private string _newCardSwimlaneId = "";
    private string _newCardPriority = "";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadBacklogAsync();
    }

    private async Task LoadBacklogAsync()
    {
        _isLoading = true;
        try
        {
            var cards = await ApiClient.GetBacklogCardsAsync(BoardId);
            _backlogCards.Clear();
            _backlogCards.AddRange(cards);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshBacklogAsync()
    {
        _selectedCardIds.Clear();
        _selectAll = false;
        await LoadBacklogAsync();
    }

    private IReadOnlyList<CardDto> GetFilteredCards()
    {
        IEnumerable<CardDto> filtered = _backlogCards.Where(c => !c.IsArchived && !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            var query = _filterText.Trim();
            filtered = filtered.Where(c =>
                c.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                $"#{c.CardNumber}".Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(_priorityFilter) &&
            Enum.TryParse<CardPriority>(_priorityFilter, out var priority))
        {
            filtered = filtered.Where(c => c.Priority == priority);
        }

        return filtered.OrderByDescending(c => c.Priority).ThenBy(c => c.CreatedAt).ToList();
    }

    // ── Selection ───────────────────────────────────────────

    private void ToggleCardSelection(Guid cardId)
    {
        if (!_selectedCardIds.Remove(cardId))
            _selectedCardIds.Add(cardId);

        _selectAll = _selectedCardIds.Count == GetFilteredCards().Count && _selectedCardIds.Count > 0;
    }

    private void ToggleSelectAll()
    {
        _selectAll = !_selectAll;
        _selectedCardIds.Clear();

        if (_selectAll)
        {
            foreach (var card in GetFilteredCards())
                _selectedCardIds.Add(card.Id);
        }
    }

    // ── Sprint Assignment ───────────────────────────────────

    private async Task AssignCardToSprintAsync(Guid cardId, ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (string.IsNullOrEmpty(value) || !Guid.TryParse(value, out var sprintId)) return;

        await ApiClient.AddCardToSprintAsync(BoardId, sprintId, cardId);
        _backlogCards.RemoveAll(c => c.Id == cardId);
        _selectedCardIds.Remove(cardId);
        await OnBacklogChanged.InvokeAsync();
    }

    private async Task BulkAssignToSprintAsync()
    {
        if (string.IsNullOrEmpty(_bulkTargetSprintId) ||
            !Guid.TryParse(_bulkTargetSprintId, out var sprintId) ||
            _selectedCardIds.Count == 0)
            return;

        _isBulkAssigning = true;
        try
        {
            await ApiClient.BatchAddCardsToSprintAsync(BoardId, sprintId, _selectedCardIds.ToList());
            _backlogCards.RemoveAll(c => _selectedCardIds.Contains(c.Id));
            _selectedCardIds.Clear();
            _selectAll = false;
            _bulkTargetSprintId = "";
            await OnBacklogChanged.InvokeAsync();
        }
        finally
        {
            _isBulkAssigning = false;
        }
    }

    // ── Helpers ─────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Default swimlane selection to first available when not set
        if (string.IsNullOrEmpty(_newCardSwimlaneId) && Swimlanes.Count > 0)
            _newCardSwimlaneId = Swimlanes[0].Id.ToString();
    }

    // ── Add Card ────────────────────────────────────────────

    private void ToggleAddCard()
    {
        _isAddingCard = !_isAddingCard;
        if (_isAddingCard)
        {
            _newCardTitle = "";
            _newCardPriority = "";
            if (Swimlanes.Count > 0)
                _newCardSwimlaneId = Swimlanes[0].Id.ToString();
        }
    }

    private void CancelAddCard()
    {
        _isAddingCard = false;
        _newCardTitle = "";
        _newCardPriority = "";
    }

    private async Task HandleNewCardKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SubmitNewCardAsync();
        else if (e.Key == "Escape") CancelAddCard();
    }

    private async Task SubmitNewCardAsync()
    {
        if (string.IsNullOrWhiteSpace(_newCardTitle) || Swimlanes.Count == 0) return;

        if (!Guid.TryParse(_newCardSwimlaneId, out var swimlaneId))
            swimlaneId = Swimlanes[0].Id;

        var priority = CardPriority.None;
        if (!string.IsNullOrEmpty(_newCardPriority))
            Enum.TryParse(_newCardPriority, out priority);

        _isSubmittingCard = true;
        try
        {
            var card = await ApiClient.CreateCardAsync(swimlaneId, new CreateCardDto
            {
                Title = _newCardTitle.Trim(),
                Priority = priority
            });

            if (card is not null)
            {
                _backlogCards.Insert(0, card);
                _newCardTitle = "";
                _newCardPriority = "";
                await OnBacklogChanged.InvokeAsync();
            }
        }
        finally
        {
            _isSubmittingCard = false;
        }
    }

    // ── Display Helpers ─────────────────────────────────────

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

    private static string GetContrastTextColor(string? hexColor)
    {
        if (string.IsNullOrEmpty(hexColor)) return "#fff";

        var hex = hexColor.TrimStart('#');
        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";

        if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out _))
            return "#fff";

        var r = int.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber) / 255.0;
        var g = int.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber) / 255.0;
        var b = int.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber) / 255.0;

        r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

        var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return luminance > 0.179 ? "#1a1a2e" : "#ffffff";
    }
}

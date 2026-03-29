using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Main entry component for the Tracks module UI.
/// </summary>
public partial class TracksPage : ComponentBase, IDisposable
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    private enum TracksView { Boards, Board, Teams }

    private TracksView _view = TracksView.Boards;
    private bool _isLoading = true;
    private string? _errorMessage;

    // Board list state
    private readonly List<BoardDto> _boards = [];
    private readonly List<TracksTeamDto> _teams = [];

    // Active board state
    private BoardDto? _selectedBoard;
    private readonly List<BoardListDto> _boardLists = [];
    private readonly Dictionary<Guid, List<CardDto>> _cardsByList = new();
    private readonly List<SprintDto> _sprints = [];

    // Card detail
    private CardDto? _selectedCard;

    // Panels
    private bool _showSprints;
    private bool _showBoardSettings;

    protected override async Task OnInitializedAsync()
    {
        await LoadInitialDataAsync();
    }

    private async Task LoadInitialDataAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            var boardsTask = ApiClient.ListBoardsAsync();
            var teamsTask = ApiClient.ListTeamsAsync();
            await Task.WhenAll(boardsTask, teamsTask);

            _boards.Clear();
            _boards.AddRange(await boardsTask);

            _teams.Clear();
            _teams.AddRange(await teamsTask);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load boards: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    // ── Navigation ──────────────────────────────────────────

    private void ShowBoardList()
    {
        _view = TracksView.Boards;
        _selectedBoard = null;
        _selectedCard = null;
        _showBoardSettings = false;
        _showSprints = false;
    }

    private void ShowTeams()
    {
        _view = TracksView.Teams;
        _selectedBoard = null;
        _selectedCard = null;
    }

    private async Task SelectBoard(Guid boardId)
    {
        _isLoading = true;
        _errorMessage = null;
        _selectedCard = null;
        _showBoardSettings = false;

        try
        {
            _selectedBoard = await ApiClient.GetBoardAsync(boardId);
            if (_selectedBoard is null)
            {
                _errorMessage = "Board not found.";
                return;
            }

            await RefreshBoardDataAsync();
            _view = TracksView.Board;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load board: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SelectTeam(Guid teamId)
    {
        _view = TracksView.Teams;
        await Task.CompletedTask;
    }

    // ── Board Data ──────────────────────────────────────────

    private async Task RefreshBoardDataAsync()
    {
        if (_selectedBoard is null) return;

        var listsTask = ApiClient.ListListsAsync(_selectedBoard.Id);
        var sprintsTask = ApiClient.ListSprintsAsync(_selectedBoard.Id);
        await Task.WhenAll(listsTask, sprintsTask);

        _boardLists.Clear();
        _boardLists.AddRange((await listsTask).OrderBy(l => l.Position));

        _sprints.Clear();
        _sprints.AddRange(await sprintsTask);

        _cardsByList.Clear();
        foreach (var list in _boardLists)
        {
            var cards = await ApiClient.ListCardsAsync(list.Id);
            _cardsByList[list.Id] = cards.OrderBy(c => c.Position).ToList();
        }
    }

    private async Task RefreshBoardAsync()
    {
        if (_selectedBoard is null) return;

        try
        {
            _selectedBoard = await ApiClient.GetBoardAsync(_selectedBoard.Id);
            await RefreshBoardDataAsync();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to refresh board: {ex.Message}";
        }
    }

    private async Task RefreshSprintsAsync()
    {
        if (_selectedBoard is null) return;
        _sprints.Clear();
        _sprints.AddRange(await ApiClient.ListSprintsAsync(_selectedBoard.Id));
        StateHasChanged();
    }

    private async Task RefreshTeamsAsync()
    {
        _teams.Clear();
        _teams.AddRange(await ApiClient.ListTeamsAsync());
        StateHasChanged();
    }

    // ── Card Selection ──────────────────────────────────────

    private async Task SelectCard(Guid cardId)
    {
        try
        {
            _selectedCard = await ApiClient.GetCardAsync(cardId);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load card: {ex.Message}";
        }
    }

    private void CloseCardDetail()
    {
        _selectedCard = null;
    }

    // ── Board Events ────────────────────────────────────────

    private async Task HandleBoardCreated(BoardDto board)
    {
        _boards.Insert(0, board);
        await SelectBoard(board.Id);
    }

    private async Task HandleBoardDeleted(Guid boardId)
    {
        _boards.RemoveAll(b => b.Id == boardId);
        if (_selectedBoard?.Id == boardId)
        {
            ShowBoardList();
        }
        await Task.CompletedTask;
    }

    private async Task HandleBoardUpdated(BoardDto board)
    {
        _selectedBoard = board;
        var index = _boards.FindIndex(b => b.Id == board.Id);
        if (index >= 0) _boards[index] = board;
        await Task.CompletedTask;
    }

    private async Task HandleBoardDeletedFromSettings(Guid boardId)
    {
        await HandleBoardDeleted(boardId);
        ShowBoardList();
        await LoadInitialDataAsync();
    }

    // ── Card Events ─────────────────────────────────────────

    private async Task HandleCardMoved(CardDto card)
    {
        // Remove from old list, add to new
        foreach (var (_, cards) in _cardsByList)
        {
            cards.RemoveAll(c => c.Id == card.Id);
        }

        if (_cardsByList.TryGetValue(card.ListId, out var targetCards))
        {
            targetCards.Add(card);
            targetCards.Sort((a, b) => a.Position.CompareTo(b.Position));
        }

        await Task.CompletedTask;
    }

    private async Task HandleCardCreated(CardDto card)
    {
        if (_cardsByList.TryGetValue(card.ListId, out var cards))
        {
            cards.Add(card);
            cards.Sort((a, b) => a.Position.CompareTo(b.Position));
        }
        await Task.CompletedTask;
    }

    private async Task HandleCardUpdated(CardDto card)
    {
        _selectedCard = card;

        foreach (var (_, cards) in _cardsByList)
        {
            var index = cards.FindIndex(c => c.Id == card.Id);
            if (index >= 0)
            {
                cards[index] = card;
                break;
            }
        }
        await Task.CompletedTask;
    }

    private async Task HandleCardDeleted(Guid cardId)
    {
        foreach (var (_, cards) in _cardsByList)
        {
            cards.RemoveAll(c => c.Id == cardId);
        }

        if (_selectedCard?.Id == cardId)
        {
            _selectedCard = null;
        }
        await Task.CompletedTask;
    }

    // ── List Events ─────────────────────────────────────────

    private async Task HandleListCreated(BoardListDto list)
    {
        _boardLists.Add(list);
        _boardLists.Sort((a, b) => a.Position.CompareTo(b.Position));
        _cardsByList[list.Id] = [];
        await Task.CompletedTask;
    }

    private async Task HandleListDeleted(Guid listId)
    {
        _boardLists.RemoveAll(l => l.Id == listId);
        _cardsByList.Remove(listId);
        await Task.CompletedTask;
    }

    // ── Panel Toggles ───────────────────────────────────────

    private void ToggleSprints() => _showSprints = !_showSprints;

    private void ShowBoardSettings() => _showBoardSettings = true;

    private void CloseBoardSettings() => _showBoardSettings = false;

    public void Dispose()
    {
        // Future: dispose SignalR connections
    }
}

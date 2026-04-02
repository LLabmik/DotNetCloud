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
    [Inject] private ITracksSignalRService SignalRService { get; set; } = default!;

    private enum TracksView { Boards, Board, Teams, Planning, Wizard, Backlog }

    private TracksView _view = TracksView.Boards;
    private bool _sidebarCollapsed;
    private bool _isLoading = true;
    private string? _errorMessage;

    // Board overview state
    private readonly List<BoardDto> _boards = [];
    private readonly List<TracksTeamDto> _teams = [];

    // Active board state
    private BoardDto? _selectedBoard;
    private readonly List<BoardSwimlaneDto> _boardSwimlanes = [];
    private readonly Dictionary<Guid, List<CardDto>> _cardsBySwimlane = new();
    private readonly List<SprintDto> _sprints = [];

    // Card detail
    private CardDto? _selectedCard;

    // Panels
    private bool _showSprints;
    private bool _showBoardSettings;

    // Sprint planning
    private SprintDto? _planningSprint;

    /// <summary>First sprint in Planning or Active status, for sidebar nav.</summary>
    private SprintDto? PlannableSprint => _sprints.FirstOrDefault(s => s.Status is SprintStatus.Planning or SprintStatus.Active);

    protected override async Task OnInitializedAsync()
    {
        SubscribeToRealtimeEvents();
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

    private void ShowBoardSwimlane()
    {
        _view = TracksView.Boards;
        _selectedBoard = null;
        _selectedCard = null;
        _showBoardSettings = false;
        _showSprints = false;
        _initialTeamFilter = null;
    }

    private void ShowTeams()
    {
        _view = TracksView.Teams;
        _selectedBoard = null;
        _selectedCard = null;
    }

    private void OpenSprintPlanning(SprintDto sprint)
    {
        if (_selectedBoard?.Mode != BoardMode.Team) return;
        _planningSprint = sprint;
        _selectedCard = null;
        _showBoardSettings = false;
        _showSprints = false;
        _view = TracksView.Planning;
    }

    private void ClosePlanning()
    {
        _planningSprint = null;
        _view = TracksView.Board;
    }

    private void OpenWizard()
    {
        if (_selectedBoard?.Mode != BoardMode.Team) return;
        _selectedCard = null;
        _showBoardSettings = false;
        _showSprints = false;
        _view = TracksView.Wizard;
    }

    private void OpenBacklog()
    {
        if (_selectedBoard?.Mode != BoardMode.Team) return;
        _selectedCard = null;
        _showBoardSettings = false;
        _showSprints = false;
        _view = TracksView.Backlog;
    }

    private async Task HandleBacklogChanged()
    {
        await RefreshBoardDataAsync();
        StateHasChanged();
    }

    private async Task HandlePlanCreated(SprintPlanOverviewDto overview)
    {
        await RefreshBoardDataAsync();
        _view = TracksView.Board;
        StateHasChanged();
    }

    private void CloseWizard()
    {
        _view = TracksView.Board;
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

    private string? _initialTeamFilter;

    private void SelectTeam(Guid teamId)
    {
        _initialTeamFilter = teamId.ToString();
        _view = TracksView.Boards;
        _selectedBoard = null;
        _selectedCard = null;
    }

    // ── Board Data ──────────────────────────────────────────

    private async Task RefreshBoardDataAsync()
    {
        if (_selectedBoard is null) return;

        var swimlanesTask = ApiClient.ListSwimlanesAsync(_selectedBoard.Id);
        var sprintsTask = ApiClient.ListSprintsAsync(_selectedBoard.Id);
        await Task.WhenAll(swimlanesTask, sprintsTask);

        _boardSwimlanes.Clear();
        _boardSwimlanes.AddRange((await swimlanesTask).OrderBy(swimlane => swimlane.Position));

        _sprints.Clear();
        _sprints.AddRange(await sprintsTask);

        _cardsBySwimlane.Clear();
        foreach (var swimlane in _boardSwimlanes)
        {
            var cards = await ApiClient.ListCardsAsync(swimlane.Id);
            _cardsBySwimlane[swimlane.Id] = cards.OrderBy(c => c.Position).ToList();
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

        var planningSprintId = _planningSprint?.Id;

        _sprints.Clear();
        _sprints.AddRange(await ApiClient.ListSprintsAsync(_selectedBoard.Id));

        if (planningSprintId.HasValue)
        {
            _planningSprint = _sprints.FirstOrDefault(s => s.Id == planningSprintId.Value);
            if (_planningSprint is null && _view == TracksView.Planning)
            {
                _view = TracksView.Board;
            }
        }

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
            ShowBoardSwimlane();
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
        ShowBoardSwimlane();
        await LoadInitialDataAsync();
    }

    // ── Card Events ─────────────────────────────────────────

    private async Task HandleCardMoved(CardDto card)
    {
        // Remove from the previous swimlane, then add to the new swimlane.
        foreach (var (_, cards) in _cardsBySwimlane)
        {
            cards.RemoveAll(c => c.Id == card.Id);
        }

        if (_cardsBySwimlane.TryGetValue(card.SwimlaneId, out var targetCards))
        {
            targetCards.Add(card);
            targetCards.Sort((a, b) => a.Position.CompareTo(b.Position));
        }

        await Task.CompletedTask;
    }

    private async Task HandleCardCreated(CardDto card)
    {
        if (_cardsBySwimlane.TryGetValue(card.SwimlaneId, out var cards))
        {
            cards.Add(card);
            cards.Sort((a, b) => a.Position.CompareTo(b.Position));
        }
        await Task.CompletedTask;
    }

    private async Task HandleCardUpdated(CardDto card)
    {
        _selectedCard = card;

        foreach (var (_, cards) in _cardsBySwimlane)
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
        foreach (var (_, cards) in _cardsBySwimlane)
        {
            cards.RemoveAll(c => c.Id == cardId);
        }

        if (_selectedCard?.Id == cardId)
        {
            _selectedCard = null;
        }
        await Task.CompletedTask;
    }

    // ── Swimlane Events ─────────────────────────────────────

    private async Task HandleSwimlaneCreated(BoardSwimlaneDto swimlane)
    {
        _boardSwimlanes.Add(swimlane);
        _boardSwimlanes.Sort((a, b) => a.Position.CompareTo(b.Position));
        _cardsBySwimlane[swimlane.Id] = [];
        await Task.CompletedTask;
    }

    private async Task HandleSwimlaneDeleted(Guid swimlaneId)
    {
        _boardSwimlanes.RemoveAll(l => l.Id == swimlaneId);
        _cardsBySwimlane.Remove(swimlaneId);
        await Task.CompletedTask;
    }

    // ── Panel Toggles ───────────────────────────────────────

    private void ToggleSprints()
    {
        if (_selectedBoard?.Mode != BoardMode.Team) return;
        _showSprints = !_showSprints;
    }

    private void ShowBoardSettings() => _showBoardSettings = true;

    private void CloseBoardSettings() => _showBoardSettings = false;

    public void Dispose()
    {
        SignalRService.CardActionReceived -= OnCardActionReceived;
        SignalRService.SwimlaneActionReceived -= OnSwimlaneActionReceived;
        SignalRService.CommentActionReceived -= OnCommentActionReceived;
        SignalRService.SprintActionReceived -= OnSprintActionReceived;
        SignalRService.BoardMemberActionReceived -= OnBoardMemberActionReceived;
    }

    // ── Real-time Event Subscriptions ───────────────────────

    private void SubscribeToRealtimeEvents()
    {
        SignalRService.CardActionReceived += OnCardActionReceived;
        SignalRService.SwimlaneActionReceived += OnSwimlaneActionReceived;
        SignalRService.CommentActionReceived += OnCommentActionReceived;
        SignalRService.SprintActionReceived += OnSprintActionReceived;
        SignalRService.BoardMemberActionReceived += OnBoardMemberActionReceived;
    }

    private async void OnCardActionReceived(Guid boardId, Guid cardId, string action)
    {
        if (_selectedBoard?.Id != boardId) return;
        await InvokeAsync(async () =>
        {
            await RefreshBoardDataAsync();
            StateHasChanged();
        });
    }

    private async void OnSwimlaneActionReceived(Guid boardId, Guid swimlaneId, string action)
    {
        if (_selectedBoard?.Id != boardId) return;
        await InvokeAsync(async () =>
        {
            await RefreshBoardDataAsync();
            StateHasChanged();
        });
    }

    private async void OnCommentActionReceived(Guid boardId, Guid cardId, Guid commentId, string action)
    {
        if (_selectedCard?.Id != cardId) return;
        await InvokeAsync(async () =>
        {
            _selectedCard = await ApiClient.GetCardAsync(cardId);
            StateHasChanged();
        });
    }

    private async void OnSprintActionReceived(Guid boardId, Guid sprintId, string action)
    {
        if (_selectedBoard?.Id != boardId) return;
        await InvokeAsync(async () =>
        {
            await RefreshSprintsAsync();
        });
    }

    private async void OnBoardMemberActionReceived(Guid boardId, Guid userId, string action)
    {
        if (_selectedBoard?.Id != boardId) return;
        await InvokeAsync(async () =>
        {
            await RefreshBoardAsync();
        });
    }
}

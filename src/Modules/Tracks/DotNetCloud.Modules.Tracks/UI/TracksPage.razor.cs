using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Main entry component for the Tracks module UI.
/// </summary>
public partial class TracksPage : ComponentBase, IDisposable
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private ITracksSignalRService SignalRService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private enum TracksView { Boards, Board, Teams, Planning, Wizard, Backlog, Timeline, Review }

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

    // Review session
    private ReviewSessionDto? _activeReviewSession;
    private bool _isHost;
    private bool _isStartingReview;
    private string? _reviewStartError;

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

    private void OpenTimeline()
    {
        if (_selectedBoard?.Mode != BoardMode.Team) return;
        _selectedCard = null;
        _showBoardSettings = false;
        _showSprints = false;
        _view = TracksView.Timeline;
    }

    private async Task OpenReview()
    {
        if (_selectedBoard?.Mode != BoardMode.Team) return;
        _selectedCard = null;
        _showBoardSettings = false;
        _showSprints = false;

        // Check for an existing active session on this board
        try
        {
            _activeReviewSession = await ApiClient.GetActiveReviewSessionAsync(_selectedBoard.Id);
        }
        catch
        {
            _activeReviewSession = null;
        }

        if (_activeReviewSession is not null)
        {
            var currentUserId = await GetCurrentUserIdAsync();
            _isHost = currentUserId.HasValue && _activeReviewSession.HostUserId == currentUserId.Value;
        }
        else
        {
            _isHost = false;
        }
        _view = TracksView.Review;
    }

    private async Task<Guid?> GetCurrentUserIdAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        var claim = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? state.User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private async Task HandleReviewStarted(ReviewSessionDto session)
    {
        _activeReviewSession = session;
        _isHost = true;
        StateHasChanged();
    }

    private async Task HandleReviewJoined(ReviewSessionDto session)
    {
        _activeReviewSession = session;
        _isHost = false;
        StateHasChanged();
    }

    private async Task HandleReviewEnded()
    {
        _activeReviewSession = null;
        _isHost = false;
        _view = TracksView.Board;
        await RefreshBoardDataAsync();
        StateHasChanged();
    }

    private async Task StartReviewSession()
    {
        if (_selectedBoard is null) return;
        _isStartingReview = true;
        _reviewStartError = null;
        try
        {
            var session = await ApiClient.StartReviewSessionAsync(_selectedBoard.Id);
            if (session is not null)
            {
                _activeReviewSession = session;
                _isHost = true;
            }
            else
            {
                _reviewStartError = "Failed to start review session.";
            }
        }
        catch (Exception ex)
        {
            _reviewStartError = ex.Message;
        }
        finally
        {
            _isStartingReview = false;
        }
    }

    private async Task HandleReviewSessionUpdated(ReviewSessionDto session)
    {
        _activeReviewSession = session;
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task HandleTimelineSprintSelected(SprintDto sprint)
    {
        // Navigate to the board view (kanban) — the sprint tab selection
        // is handled by setting the sprint filter on the board.
        _view = TracksView.Board;
        StateHasChanged();
    }

    private async Task HandlePlanAdjusted()
    {
        await RefreshSprintsAsync();
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
        SignalRService.ReviewSessionStateChanged -= OnReviewSessionStateChanged;
    }

    // ── Real-time Event Subscriptions ───────────────────────

    private void SubscribeToRealtimeEvents()
    {
        SignalRService.CardActionReceived += OnCardActionReceived;
        SignalRService.SwimlaneActionReceived += OnSwimlaneActionReceived;
        SignalRService.CommentActionReceived += OnCommentActionReceived;
        SignalRService.SprintActionReceived += OnSprintActionReceived;
        SignalRService.BoardMemberActionReceived += OnBoardMemberActionReceived;
        SignalRService.ReviewSessionStateChanged += OnReviewSessionStateChanged;
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

    private async void OnReviewSessionStateChanged(Guid sessionId, Guid boardId, string action)
    {
        if (_selectedBoard?.Id != boardId) return;
        await InvokeAsync(async () =>
        {
            if (action is "ended")
            {
                _activeReviewSession = null;
                _isHost = false;
                if (_view == TracksView.Review)
                {
                    _view = TracksView.Board;
                }
            }
            else
            {
                try
                {
                    _activeReviewSession = await ApiClient.GetReviewSessionAsync(sessionId);
                }
                catch
                {
                    _activeReviewSession = null;
                }
            }
            StateHasChanged();
        });
    }
}

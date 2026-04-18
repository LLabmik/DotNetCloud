using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Host controls for a live review session. Provides card navigation,
/// participant list with online status, and integrated poker controls.
/// Admin+ only on Team boards.
/// </summary>
public partial class ReviewSessionHost : ComponentBase, IDisposable
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private ITracksSignalRService SignalRService { get; set; } = default!;
    [Inject] private ILogger<ReviewSessionHost> Logger { get; set; } = default!;

    /// <summary>The active review session being hosted.</summary>
    [Parameter, EditorRequired] public ReviewSessionDto Session { get; set; } = default!;

    /// <summary>The board for this review session.</summary>
    [Parameter, EditorRequired] public BoardDto Board { get; set; } = default!;

    /// <summary>Available sprints for card filtering.</summary>
    [Parameter] public List<SprintDto> Sprints { get; set; } = [];

    /// <summary>Raised when the session has been ended.</summary>
    [Parameter] public EventCallback OnSessionEnded { get; set; }

    /// <summary>Raised when session state is updated (e.g. new card, poker state change).</summary>
    [Parameter] public EventCallback<ReviewSessionDto> OnSessionUpdated { get; set; }

    // Card navigation
    private readonly List<CardDto> _cards = [];
    private int _currentCardIndex;
    private CardDto? _currentCard;
    private string _selectedSprintId = "";

    // Poker state
    private PokerSessionDto? _activePoker;
    private readonly List<PokerVoteStatusDto> _voteStatuses = [];
    private PokerScale _selectedScale = PokerScale.Fibonacci;
    private string? _hostVote;
    private string? _acceptEstimate;

    // Loading flags
    private bool _isStartingPoker;
    private bool _isSubmittingVote;
    private bool _isRevealingVotes;
    private bool _isAccepting;
    private bool _isStartingRound;
    private string? _errorMessage;

    /// <summary>Number of participants currently connected.</summary>
    private int ConnectedParticipantCount => Session.Participants.Count(p => p.IsConnected);

    private bool HasPreviousCard => _currentCardIndex > 0 && _cards.Count > 0;
    private bool HasNextCard => _currentCardIndex < _cards.Count - 1;

    /// <summary>Number of participants who have voted in the active poker session.</summary>
    private int VotedCount => _voteStatuses.Count(v => v.HasVoted);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        SubscribeToEvents();
        _activePoker = Session.ActivePokerSession;
        await LoadCardsAsync();

        // If session already has a current card, navigate to it
        if (Session.CurrentCardId.HasValue)
        {
            var idx = _cards.FindIndex(c => c.Id == Session.CurrentCardId.Value);
            if (idx >= 0)
            {
                _currentCardIndex = idx;
                _currentCard = _cards[idx];
            }
        }
        else if (_currentCard is not null)
        {
            // First load — sync the initial card to server so poker can reference it
            await SetCurrentCardOnServer();
        }

        if (_activePoker is not null)
        {
            await RefreshVoteStatusAsync();
        }
    }

    private async Task LoadCardsAsync()
    {
        try
        {
            IReadOnlyList<CardDto> cards;
            if (_selectedSprintId == "backlog")
            {
                cards = await ApiClient.GetBacklogCardsAsync(Board.Id);
            }
            else if (Guid.TryParse(_selectedSprintId, out var sprintId))
            {
                cards = await ApiClient.GetSprintCardsAsync(Board.Id, sprintId);
            }
            else
            {
                // Load all cards from all swimlanes
                var swimlanes = await ApiClient.ListSwimlanesAsync(Board.Id);
                var allCards = new List<CardDto>();
                foreach (var sl in swimlanes)
                {
                    var slCards = await ApiClient.ListCardsAsync(sl.Id);
                    allCards.AddRange(slCards);
                }
                cards = allCards;
            }
            _cards.Clear();
            _cards.AddRange(cards.OrderBy(c => c.CardNumber));
            _currentCardIndex = 0;
            _currentCard = _cards.Count > 0 ? _cards[0] : null;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load cards: {ex.Message}";
        }
    }

    // ── Card Navigation ─────────────────────────────────────

    private async Task PreviousCard()
    {
        if (!HasPreviousCard) return;
        _currentCardIndex--;
        _currentCard = _cards[_currentCardIndex];
        _activePoker = null;
        _voteStatuses.Clear();
        _hostVote = null;
        _acceptEstimate = null;
        await SetCurrentCardOnServer();
    }

    private async Task NextCard()
    {
        if (!HasNextCard) return;
        _currentCardIndex++;
        _currentCard = _cards[_currentCardIndex];
        _activePoker = null;
        _voteStatuses.Clear();
        _hostVote = null;
        _acceptEstimate = null;
        await SetCurrentCardOnServer();
    }

    private async Task SetCurrentCardOnServer()
    {
        if (_currentCard is null) return;
        try
        {
            var updated = await ApiClient.SetReviewCurrentCardAsync(Session.Id, _currentCard.Id);
            if (updated is not null)
            {
                _activePoker = updated.ActivePokerSession;
                await OnSessionUpdated.InvokeAsync(updated);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to set current card: {ex.Message}";
        }
    }

    private async Task HandleSprintFilterChanged(ChangeEventArgs e)
    {
        _selectedSprintId = e.Value?.ToString() ?? "";
        await LoadCardsAsync();
        if (_currentCard is not null)
        {
            await SetCurrentCardOnServer();
        }
    }

    // ── Poker Controls ──────────────────────────────────────

    private async Task StartPoker()
    {
        if (_currentCard is null) return;
        _isStartingPoker = true;
        _errorMessage = null;
        try
        {
            var dto = new StartReviewPokerDto { Scale = _selectedScale };
            Logger.LogInformation("StartPoker: SessionId={SessionId}, CardId={CardId}, Scale={Scale}",
                Session.Id, _currentCard.Id, _selectedScale);
            var updated = await ApiClient.StartReviewPokerAsync(Session.Id, dto);
            if (updated is not null)
            {
                _activePoker = updated.ActivePokerSession;
                _voteStatuses.Clear();
                _hostVote = null;
                _acceptEstimate = null;
                await OnSessionUpdated.InvokeAsync(updated);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to start poker: {ex.Message}";
        }
        finally
        {
            _isStartingPoker = false;
        }
    }

    private void SelectHostVote(string value)
    {
        _hostVote = _hostVote == value ? null : value;
    }

    private async Task SubmitHostVote()
    {
        if (_activePoker is null || _hostVote is null) return;
        _isSubmittingVote = true;
        _errorMessage = null;
        try
        {
            await ApiClient.SubmitPokerVoteAsync(_activePoker.Id, new SubmitPokerVoteDto { Estimate = _hostVote });
            await RefreshVoteStatusAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to submit vote: {ex.Message}";
        }
        finally
        {
            _isSubmittingVote = false;
        }
    }

    private async Task RevealVotes()
    {
        if (_activePoker is null) return;
        _isRevealingVotes = true;
        _errorMessage = null;
        try
        {
            var revealed = await ApiClient.RevealPokerSessionAsync(_activePoker.Id);
            if (revealed is not null)
            {
                _activePoker = revealed;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to reveal votes: {ex.Message}";
        }
        finally
        {
            _isRevealingVotes = false;
        }
    }

    private async Task AcceptEstimate()
    {
        if (_activePoker is null || _acceptEstimate is null) return;
        _isAccepting = true;
        _errorMessage = null;
        try
        {
            int? sp = int.TryParse(_acceptEstimate, out var n) ? n : null;
            var accepted = await ApiClient.AcceptPokerEstimateAsync(_activePoker.Id, new AcceptPokerEstimateDto
            {
                AcceptedEstimate = _acceptEstimate,
                StoryPoints = sp
            });
            if (accepted is not null)
            {
                _activePoker = accepted;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to accept estimate: {ex.Message}";
        }
        finally
        {
            _isAccepting = false;
        }
    }

    private async Task StartNewRound()
    {
        if (_activePoker is null) return;
        _isStartingRound = true;
        _errorMessage = null;
        try
        {
            var newRound = await ApiClient.StartNewPokerRoundAsync(_activePoker.Id);
            if (newRound is not null)
            {
                _activePoker = newRound;
                _voteStatuses.Clear();
                _hostVote = null;
                _acceptEstimate = null;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to start new round: {ex.Message}";
        }
        finally
        {
            _isStartingRound = false;
        }
    }

    private async Task RefreshVoteStatusAsync()
    {
        if (_activePoker is null) return;
        try
        {
            var statuses = await ApiClient.GetPokerVoteStatusAsync(_activePoker.Id);
            _voteStatuses.Clear();
            _voteStatuses.AddRange(statuses);
        }
        catch
        {
            // Silently fail — not critical
        }
    }

    // ── Session Controls ────────────────────────────────────

    private async Task EndSession()
    {
        try
        {
            await ApiClient.EndReviewSessionAsync(Session.Id);
            await OnSessionEnded.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to end session: {ex.Message}";
        }
    }

    // ── Helpers ──────────────────────────────────────────────

    internal static string GetStatusClass(ReviewSessionStatus status) => status switch
    {
        ReviewSessionStatus.Active => "status-active",
        ReviewSessionStatus.Paused => "status-paused",
        ReviewSessionStatus.Ended => "status-ended",
        _ => ""
    };

    private string GetStatusClass() => GetStatusClass(Session.Status);

    internal static string GetPokerStatusClass(PokerSessionStatus status) => status switch
    {
        PokerSessionStatus.Voting => "poker-voting",
        PokerSessionStatus.Revealed => "poker-revealed",
        PokerSessionStatus.Completed => "poker-completed",
        PokerSessionStatus.Cancelled => "poker-cancelled",
        _ => ""
    };

    private string GetPokerStatusClass() => _activePoker is not null ? GetPokerStatusClass(_activePoker.Status) : "";

    internal static string[] GetScaleValues(PokerScale scale) => scale switch
    {
        PokerScale.Fibonacci => ["0", "1", "2", "3", "5", "8", "13", "21", "34", "?"],
        PokerScale.TShirt => ["XS", "S", "M", "L", "XL", "XXL", "?"],
        PokerScale.PowersOfTwo => ["0", "1", "2", "4", "8", "16", "32", "?"],
        _ => ["1", "2", "3", "5", "8", "13", "?"]
    };

    private string[] GetScaleValues() => _activePoker is not null
        ? GetScaleValues(_activePoker.Scale)
        : GetScaleValues(_selectedScale);

    // ── SignalR Event Handlers ───────────────────────────────

    private void SubscribeToEvents()
    {
        SignalRService.PokerVoteStatusChanged += OnPokerVoteStatusChanged;
        SignalRService.ReviewPokerStateChanged += OnReviewPokerStateChanged;
        SignalRService.ReviewParticipantChanged += OnReviewParticipantChanged;
    }

    private async void OnPokerVoteStatusChanged(Guid sessionId, Guid pokerId, Guid userId, bool hasVoted)
    {
        if (sessionId != Session.Id) return;
        await InvokeAsync(async () =>
        {
            await RefreshVoteStatusAsync();
            StateHasChanged();
        });
    }

    private async void OnReviewPokerStateChanged(Guid sessionId, Guid pokerId, Guid boardId, string action)
    {
        if (sessionId != Session.Id) return;
        await InvokeAsync(async () =>
        {
            try
            {
                var updated = await ApiClient.GetReviewSessionAsync(Session.Id);
                if (updated is not null)
                {
                    _activePoker = updated.ActivePokerSession;
                    await OnSessionUpdated.InvokeAsync(updated);
                }
            }
            catch
            {
                // Silently fail
            }
            StateHasChanged();
        });
    }

    private async void OnReviewParticipantChanged(Guid sessionId, Guid userId, string action)
    {
        if (sessionId != Session.Id) return;
        await InvokeAsync(async () =>
        {
            try
            {
                var updated = await ApiClient.GetReviewSessionAsync(Session.Id);
                if (updated is not null)
                {
                    await OnSessionUpdated.InvokeAsync(updated);
                }
            }
            catch
            {
                // Silently fail
            }
            StateHasChanged();
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SignalRService.PokerVoteStatusChanged -= OnPokerVoteStatusChanged;
        SignalRService.ReviewPokerStateChanged -= OnReviewPokerStateChanged;
        SignalRService.ReviewParticipantChanged -= OnReviewParticipantChanged;
    }
}

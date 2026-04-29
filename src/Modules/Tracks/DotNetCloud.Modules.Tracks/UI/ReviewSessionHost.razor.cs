using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Host controls for a live review session. Provides item navigation,
/// participant count, and integrated poker controls.
/// </summary>
public partial class ReviewSessionHost : ComponentBase, IDisposable
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private ITracksSignalRService SignalRService { get; set; } = default!;
    [Inject] private ILogger<ReviewSessionHost> Logger { get; set; } = default!;

    /// <summary>The active review session being hosted.</summary>
    [Parameter, EditorRequired] public ReviewSessionDto Session { get; set; } = default!;

    /// <summary>The epic for this review session.</summary>
    [Parameter, EditorRequired] public WorkItemDto Epic { get; set; } = default!;

    /// <summary>Available sprints for item filtering.</summary>
    [Parameter] public List<SprintDto> Sprints { get; set; } = [];

    /// <summary>Raised when the session has been ended.</summary>
    [Parameter] public EventCallback OnSessionEnded { get; set; }

    /// <summary>Raised when session state is updated.</summary>
    [Parameter] public EventCallback<ReviewSessionDto> OnSessionUpdated { get; set; }

    // Item navigation
    private readonly List<WorkItemDto> _items = [];
    private int _currentCardIndex;
    private WorkItemDto? _currentItem;
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
    private string? _errorMessage;

    private int ConnectedParticipantCount => Session.ParticipantCount;

    private bool HasPreviousItem => _currentCardIndex > 0 && _items.Count > 0;
    private bool HasNextItem => _currentCardIndex < _items.Count - 1;

    private int VotedCount => _voteStatuses.Count(v => v.HasVoted);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        SubscribeToEvents();
        await LoadPokerIfActiveAsync();
        await LoadItemsAsync();

        if (Session.CurrentItemId.HasValue)
        {
            var idx = _items.FindIndex(i => i.Id == Session.CurrentItemId.Value);
            if (idx >= 0)
            {
                _currentCardIndex = idx;
                _currentItem = _items[idx];
            }
        }
        else if (_currentItem is not null)
        {
            await SetCurrentItemOnServer();
        }

        if (_activePoker is not null)
        {
            await RefreshVoteStatusAsync();
        }
    }

    private async Task LoadPokerIfActiveAsync()
    {
        // Poker sessions are now separate; try to load the latest active one for this epic
        // TODO: Add API to query active poker session by epic & review session
    }

    private async Task LoadItemsAsync()
    {
        try
        {
            var allItems = await ApiClient.GetBacklogItemsAsync(Epic.Id);

            // Filter by sprint if selected
            if (Guid.TryParse(_selectedSprintId, out var sprintId))
            {
                allItems = allItems.Where(i => i.SprintId == sprintId).ToList();
            }

            _items.Clear();
            _items.AddRange(allItems.OrderBy(i => i.ItemNumber));
            _currentCardIndex = 0;
            _currentItem = _items.Count > 0 ? _items[0] : null;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load items: {ex.Message}";
        }
    }

    // ── Card Navigation ─────────────────────────────────────

    private async Task PreviousItem()
    {
        if (!HasPreviousItem) return;
        _currentCardIndex--;
        _currentItem = _items[_currentCardIndex];
        _activePoker = null;
        _voteStatuses.Clear();
        _hostVote = null;
        _acceptEstimate = null;
        await SetCurrentItemOnServer();
    }

    private async Task NextItem()
    {
        if (!HasNextItem) return;
        _currentCardIndex++;
        _currentItem = _items[_currentCardIndex];
        _activePoker = null;
        _voteStatuses.Clear();
        _hostVote = null;
        _acceptEstimate = null;
        await SetCurrentItemOnServer();
    }

    private async Task SetCurrentItemOnServer()
    {
        if (_currentItem is null) return;
        try
        {
            var updated = await ApiClient.SetReviewCurrentItemAsync(Session.Id, _currentItem.Id);
            if (updated is not null)
            {
                await OnSessionUpdated.InvokeAsync(updated);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to set current item: {ex.Message}";
        }
    }

    private async Task HandleSprintFilterChanged(ChangeEventArgs e)
    {
        _selectedSprintId = e.Value?.ToString() ?? "";
        await LoadItemsAsync();
        if (_currentItem is not null)
        {
            await SetCurrentItemOnServer();
        }
    }

    // ── Poker Controls ──────────────────────────────────────

    private async Task StartPoker()
    {
        if (_currentItem is null) return;
        _isStartingPoker = true;
        _errorMessage = null;
        try
        {
            var dto = new CreatePokerSessionDto { Scale = _selectedScale, ItemId = _currentItem.Id };
            _activePoker = await ApiClient.StartPokerSessionAsync(Epic.Id, dto);
            _voteStatuses.Clear();
            _hostVote = null;
            _acceptEstimate = null;
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
            var accepted = await ApiClient.AcceptPokerEstimateAsync(_activePoker.Id, _acceptEstimate);
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

    private async void OnReviewPokerStateChanged(Guid sessionId, Guid pokerId, Guid productId, string action)
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

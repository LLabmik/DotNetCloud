using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Participant view for a live review session. Auto-follows the host's current item
/// via SignalR events and provides poker voting when active.
/// </summary>
public partial class ReviewSessionParticipant : ComponentBase, IDisposable
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private ITracksSignalRService SignalRService { get; set; } = default!;

    /// <summary>The active review session.</summary>
    [Parameter, EditorRequired] public ReviewSessionDto Session { get; set; } = default!;

    /// <summary>The epic this session is on.</summary>
    [Parameter, EditorRequired] public WorkItemDto Epic { get; set; } = default!;

    /// <summary>Raised when the participant leaves the session.</summary>
    [Parameter] public EventCallback OnLeft { get; set; }

    /// <summary>Raised when the session is updated (participant refresh).</summary>
    [Parameter] public EventCallback<ReviewSessionDto> OnSessionUpdated { get; set; }

    // Item state (synced from host)
    private WorkItemDto? _currentItem;

    // Poker state
    private PokerSessionDto? _activePoker;
    private readonly List<PokerVoteStatusDto> _voteStatuses = [];
    private string? _selectedVote;
    private string? _myVote;
    private bool _hasVoted;
    private bool _isSubmittingVote;
    private string? _errorMessage;

    /// <summary>Number of connected participants.</summary>
    private int ConnectedParticipantCount => Session.ParticipantCount;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        SubscribeToEvents();
        // Poker session loaded via separate API when available

        // Load the current item if session has one
        if (Session.CurrentItemId.HasValue)
        {
            await LoadWorkItemAsync(Session.CurrentItemId.Value);
        }

        if (_activePoker is not null)
        {
            await RefreshVoteStatusAsync();
            CheckIfAlreadyVoted();
        }
    }

    private async Task LoadWorkItemAsync(Guid workItemId)
    {
        try
        {
            _currentItem = await ApiClient.GetWorkItemAsync(workItemId);
        }
        catch
        {
            _currentItem = null;
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
            // Silently fail
        }
    }

    private void CheckIfAlreadyVoted()
    {
        // We don't have the current userId here, but vote status check
        // relies on the server returning our status. We'll mark as voted
        // only after we submit.
    }

    // ── Poker Voting ────────────────────────────────────────

    private async Task SubmitVote()
    {
        if (_activePoker is null || _selectedVote is null) return;
        _isSubmittingVote = true;
        _errorMessage = null;
        try
        {
            await ApiClient.SubmitPokerVoteAsync(_activePoker.Id, new SubmitPokerVoteDto { Estimate = _selectedVote });
            _myVote = _selectedVote;
            _hasVoted = true;
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

    // ── Session Controls ────────────────────────────────────

    private async Task LeaveSession()
    {
        try
        {
            await ApiClient.LeaveReviewSessionAsync(Session.Id);
            await OnLeft.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to leave session: {ex.Message}";
        }
    }

    // ── SignalR Event Handlers ───────────────────────────────

    private void SubscribeToEvents()
    {
        SignalRService.ReviewItemChanged += OnReviewItemChanged;
        SignalRService.PokerVoteStatusChanged += OnPokerVoteStatusChanged;
        SignalRService.ReviewPokerStateChanged += OnReviewPokerStateChanged;
        SignalRService.ReviewParticipantChanged += OnReviewParticipantChanged;
        SignalRService.ReviewSessionStateChanged += OnReviewSessionStateChanged;
    }

    private async void OnReviewItemChanged(Guid sessionId, Guid productId, Guid workItemId)
    {
        if (sessionId != Session.Id) return;
        await InvokeAsync(async () =>
        {
            await LoadWorkItemAsync(workItemId);
            // Reset poker state when item changes
            _activePoker = null;
            _voteStatuses.Clear();
            _selectedVote = null;
            _myVote = null;
            _hasVoted = false;
            StateHasChanged();
        });
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
                    // ActivePokerSession removed from ReviewSessionDto
                    if (action is "started")
                    {
                        // New poker session started — reset vote state
                        _selectedVote = null;
                        _myVote = null;
                        _hasVoted = false;
                        _voteStatuses.Clear();
                    }
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

    private async void OnReviewSessionStateChanged(Guid sessionId, Guid productId, string action)
    {
        if (sessionId != Session.Id) return;
        await InvokeAsync(async () =>
        {
            if (action is "ended")
            {
                await OnLeft.InvokeAsync();
            }
            StateHasChanged();
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SignalRService.ReviewItemChanged -= OnReviewItemChanged;
        SignalRService.PokerVoteStatusChanged -= OnPokerVoteStatusChanged;
        SignalRService.ReviewPokerStateChanged -= OnReviewPokerStateChanged;
        SignalRService.ReviewParticipantChanged -= OnReviewParticipantChanged;
        SignalRService.ReviewSessionStateChanged -= OnReviewSessionStateChanged;
    }
}

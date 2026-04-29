namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// In-process implementation of <see cref="ITracksSignalRService"/> for Blazor InteractiveServer mode.
/// <see cref="TracksRealtimeService"/> fires events directly on this singleton after broadcasting via SignalR,
/// so Blazor Server components receive the signals in-process without needing a <c>HubConnection</c>.
/// </summary>
internal sealed class TracksInProcessSignalRService : ITracksSignalRService
{
    /// <inheritdoc />
    public bool IsActive => true;

    /// <inheritdoc />
    public event Action<Guid, Guid, string>? WorkItemActionReceived;

    /// <inheritdoc />
    public event Action<Guid, Guid, string>? SwimlaneActionReceived;

    /// <inheritdoc />
    public event Action<Guid, Guid, Guid, string>? CommentActionReceived;

    /// <inheritdoc />
    public event Action<Guid, Guid, string>? SprintActionReceived;

    /// <inheritdoc />
    public event Action<Guid>? ActivityReceived;

    /// <inheritdoc />
    public event Action<Guid, Guid, string>? ProductMemberActionReceived;

    /// <inheritdoc />
    public event Action<Guid, string>? TeamActionReceived;

    /// <inheritdoc />
    public event Action<Guid, Guid, Guid>? ReviewItemChanged;

    /// <inheritdoc />
    public event Action<Guid, Guid, string>? ReviewSessionStateChanged;

    /// <inheritdoc />
    public event Action<Guid, Guid, Guid, bool>? PokerVoteStatusChanged;

    /// <inheritdoc />
    public event Action<Guid, Guid, Guid, string>? ReviewPokerStateChanged;

    /// <inheritdoc />
    public event Action<Guid, Guid, string>? ReviewParticipantChanged;

    // ── Raise methods (called by TracksRealtimeService) ─────

    internal void OnWorkItemAction(Guid productId, Guid workItemId, string action)
        => WorkItemActionReceived?.Invoke(productId, workItemId, action);

    internal void OnSwimlaneAction(Guid productId, Guid swimlaneId, string action)
        => SwimlaneActionReceived?.Invoke(productId, swimlaneId, action);

    internal void OnCommentAction(Guid productId, Guid workItemId, Guid commentId, string action)
        => CommentActionReceived?.Invoke(productId, workItemId, commentId, action);

    internal void OnSprintAction(Guid epicId, Guid sprintId, string action)
        => SprintActionReceived?.Invoke(epicId, sprintId, action);

    internal void OnActivity(Guid productId)
        => ActivityReceived?.Invoke(productId);

    internal void OnProductMemberAction(Guid productId, Guid userId, string action)
        => ProductMemberActionReceived?.Invoke(productId, userId, action);

    internal void OnTeamAction(Guid teamId, string action)
        => TeamActionReceived?.Invoke(teamId, action);

    internal void OnReviewItemChanged(Guid sessionId, Guid epicId, Guid itemId)
        => ReviewItemChanged?.Invoke(sessionId, epicId, itemId);

    internal void OnReviewSessionStateChanged(Guid sessionId, Guid epicId, string action)
        => ReviewSessionStateChanged?.Invoke(sessionId, epicId, action);

    internal void OnPokerVoteStatus(Guid sessionId, Guid pokerId, Guid userId, bool hasVoted)
        => PokerVoteStatusChanged?.Invoke(sessionId, pokerId, userId, hasVoted);

    internal void OnReviewPokerStateChanged(Guid sessionId, Guid pokerId, Guid epicId, string action)
        => ReviewPokerStateChanged?.Invoke(sessionId, pokerId, epicId, action);

    internal void OnReviewParticipantChanged(Guid sessionId, Guid userId, string action)
        => ReviewParticipantChanged?.Invoke(sessionId, userId, action);
}

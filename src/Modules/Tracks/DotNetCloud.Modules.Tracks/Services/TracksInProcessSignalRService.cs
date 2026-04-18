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
    public event Action<Guid, Guid, string>? CardActionReceived;

    /// <inheritdoc />
    public event Action<Guid, Guid, string>? SwimlaneActionReceived;

    /// <inheritdoc />
    public event Action<Guid, Guid, Guid, string>? CommentActionReceived;

    /// <inheritdoc />
    public event Action<Guid, Guid, string>? SprintActionReceived;

    /// <inheritdoc />
    public event Action<Guid>? ActivityReceived;

    /// <inheritdoc />
    public event Action<Guid, Guid, string>? BoardMemberActionReceived;

    /// <inheritdoc />
    public event Action<Guid, string>? TeamActionReceived;

    /// <inheritdoc />
    public event Action<Guid, Guid, Guid>? ReviewCardChanged;

    /// <inheritdoc />
    public event Action<Guid, Guid, string>? ReviewSessionStateChanged;

    /// <inheritdoc />
    public event Action<Guid, Guid, Guid, bool>? PokerVoteStatusChanged;

    /// <inheritdoc />
    public event Action<Guid, Guid, Guid, string>? ReviewPokerStateChanged;

    /// <inheritdoc />
    public event Action<Guid, Guid, string>? ReviewParticipantChanged;

    // ── Raise methods (called by TracksRealtimeService) ─────

    internal void OnCardAction(Guid boardId, Guid cardId, string action)
        => CardActionReceived?.Invoke(boardId, cardId, action);

    internal void OnSwimlaneAction(Guid boardId, Guid swimlaneId, string action)
        => SwimlaneActionReceived?.Invoke(boardId, swimlaneId, action);

    internal void OnCommentAction(Guid boardId, Guid cardId, Guid commentId, string action)
        => CommentActionReceived?.Invoke(boardId, cardId, commentId, action);

    internal void OnSprintAction(Guid boardId, Guid sprintId, string action)
        => SprintActionReceived?.Invoke(boardId, sprintId, action);

    internal void OnActivity(Guid boardId)
        => ActivityReceived?.Invoke(boardId);

    internal void OnBoardMemberAction(Guid boardId, Guid userId, string action)
        => BoardMemberActionReceived?.Invoke(boardId, userId, action);

    internal void OnTeamAction(Guid teamId, string action)
        => TeamActionReceived?.Invoke(teamId, action);

    internal void OnReviewCardChanged(Guid sessionId, Guid boardId, Guid cardId)
        => ReviewCardChanged?.Invoke(sessionId, boardId, cardId);

    internal void OnReviewSessionStateChanged(Guid sessionId, Guid boardId, string action)
        => ReviewSessionStateChanged?.Invoke(sessionId, boardId, action);

    internal void OnPokerVoteStatus(Guid sessionId, Guid pokerId, Guid userId, bool hasVoted)
        => PokerVoteStatusChanged?.Invoke(sessionId, pokerId, userId, hasVoted);

    internal void OnReviewPokerStateChanged(Guid sessionId, Guid pokerId, Guid boardId, string action)
        => ReviewPokerStateChanged?.Invoke(sessionId, pokerId, boardId, action);

    internal void OnReviewParticipantChanged(Guid sessionId, Guid userId, string action)
        => ReviewParticipantChanged?.Invoke(sessionId, userId, action);
}

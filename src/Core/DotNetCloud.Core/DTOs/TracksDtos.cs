namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Represents a project board.
/// </summary>
public sealed record BoardDto
{
    /// <summary>
    /// Unique identifier for the board.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Identifier of the user who owns this board.
    /// </summary>
    public required Guid OwnerId { get; init; }

    /// <summary>
    /// Optional team that owns this board. Null for personal boards.
    /// </summary>
    public Guid? TeamId { get; init; }

    /// <summary>
    /// Board title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Optional board description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Hex color code for UI display.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Whether the board has been archived.
    /// </summary>
    public bool IsArchived { get; init; }

    /// <summary>
    /// Whether the board has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Timestamp when the board was deleted, if applicable.
    /// </summary>
    public DateTime? DeletedAt { get; init; }

    /// <summary>
    /// Timestamp when the board was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the board was last modified.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Members of this board.
    /// </summary>
    public IReadOnlyList<BoardMemberDto> Members { get; init; } = [];

    /// <summary>
    /// Lists (columns) in this board.
    /// </summary>
    public IReadOnlyList<BoardListDto> Lists { get; init; } = [];

    /// <summary>
    /// Labels available on this board.
    /// </summary>
    public IReadOnlyList<LabelDto> Labels { get; init; } = [];

    /// <summary>
    /// ETag for conflict detection.
    /// </summary>
    public string? ETag { get; init; }
}

/// <summary>
/// Role a user can have on a board.
/// </summary>
public enum BoardMemberRole
{
    /// <summary>Read-only access to the board.</summary>
    Viewer,

    /// <summary>Can create, edit, and move cards.</summary>
    Member,

    /// <summary>Can manage lists, labels, and members.</summary>
    Admin,

    /// <summary>Full control including board deletion.</summary>
    Owner
}

/// <summary>
/// Represents a user's membership on a board.
/// </summary>
public sealed record BoardMemberDto
{
    /// <summary>
    /// The user's ID.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// The member's role on this board.
    /// </summary>
    public required BoardMemberRole Role { get; init; }

    /// <summary>
    /// Timestamp when the user joined the board.
    /// </summary>
    public required DateTime JoinedAt { get; init; }
}

/// <summary>
/// Represents a column (list) within a board.
/// </summary>
public sealed record BoardListDto
{
    /// <summary>
    /// Unique identifier for the list.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The board this list belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// List title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Hex color code for UI display.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Position within the board (lower = further left).
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// Maximum number of cards allowed (WIP limit). Null means unlimited.
    /// </summary>
    public int? CardLimit { get; init; }

    /// <summary>
    /// Current number of cards in this list.
    /// </summary>
    public int CardCount { get; init; }

    /// <summary>
    /// Timestamp when the list was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the list was last modified.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Priority level for a card.
/// </summary>
public enum CardPriority
{
    /// <summary>No priority set.</summary>
    None,

    /// <summary>Low priority.</summary>
    Low,

    /// <summary>Medium priority.</summary>
    Medium,

    /// <summary>High priority.</summary>
    High,

    /// <summary>Urgent priority.</summary>
    Urgent
}

/// <summary>
/// Represents a card (work item) on a board.
/// </summary>
public sealed record CardDto
{
    /// <summary>
    /// Unique identifier for the card.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The list this card is in.
    /// </summary>
    public required Guid ListId { get; init; }

    /// <summary>
    /// The board this card belongs to (denormalized for convenience).
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// Card title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Markdown description body.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Position within the list (lower = higher up).
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// Card priority level.
    /// </summary>
    public CardPriority Priority { get; init; }

    /// <summary>
    /// Optional due date for the card.
    /// </summary>
    public DateTime? DueDate { get; init; }

    /// <summary>
    /// Story points estimate for the card.
    /// </summary>
    public int? StoryPoints { get; init; }

    /// <summary>
    /// Whether the card has been archived.
    /// </summary>
    public bool IsArchived { get; init; }

    /// <summary>
    /// Whether the card has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Timestamp when the card was deleted, if applicable.
    /// </summary>
    public DateTime? DeletedAt { get; init; }

    /// <summary>
    /// Timestamp when the card was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the card was last modified.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Users assigned to this card.
    /// </summary>
    public IReadOnlyList<CardAssignmentDto> Assignments { get; init; } = [];

    /// <summary>
    /// Labels applied to this card.
    /// </summary>
    public IReadOnlyList<LabelDto> Labels { get; init; } = [];

    /// <summary>
    /// Checklists on this card.
    /// </summary>
    public IReadOnlyList<CardChecklistDto> Checklists { get; init; } = [];

    /// <summary>
    /// Number of comments on this card.
    /// </summary>
    public int CommentCount { get; init; }

    /// <summary>
    /// Number of attachments on this card.
    /// </summary>
    public int AttachmentCount { get; init; }

    /// <summary>
    /// Total tracked time in minutes for this card.
    /// </summary>
    public int TotalTrackedMinutes { get; init; }

    /// <summary>
    /// ETag for conflict detection.
    /// </summary>
    public string? ETag { get; init; }
}

/// <summary>
/// Represents a user assigned to a card.
/// </summary>
public sealed record CardAssignmentDto
{
    /// <summary>
    /// The assigned user's ID.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The assigned user's display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Timestamp when the user was assigned.
    /// </summary>
    public required DateTime AssignedAt { get; init; }
}

/// <summary>
/// Represents a reusable label on a board.
/// </summary>
public sealed record LabelDto
{
    /// <summary>
    /// Unique identifier for the label.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The board this label belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// Label title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Hex color code for the label.
    /// </summary>
    public required string Color { get; init; }
}

/// <summary>
/// Represents a comment on a card.
/// </summary>
public sealed record CardCommentDto
{
    /// <summary>
    /// Unique identifier for the comment.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The card this comment belongs to.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// The user who wrote the comment.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The commenter's display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Markdown content of the comment.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Timestamp when the comment was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the comment was last edited.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Represents a file or URL attachment on a card.
/// </summary>
public sealed record CardAttachmentDto
{
    /// <summary>
    /// Unique identifier for the attachment.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The card this attachment belongs to.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// Reference to a FileNode in the Files module, if applicable.
    /// </summary>
    public Guid? FileNodeId { get; init; }

    /// <summary>
    /// Display file name.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// External URL, if the attachment is not a Files module reference.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// The user who added the attachment.
    /// </summary>
    public required Guid AddedByUserId { get; init; }

    /// <summary>
    /// Timestamp when the attachment was added.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Represents a checklist on a card.
/// </summary>
public sealed record CardChecklistDto
{
    /// <summary>
    /// Unique identifier for the checklist.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The card this checklist belongs to.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// Checklist title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Position within the card (lower = higher up).
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// Items in this checklist.
    /// </summary>
    public IReadOnlyList<ChecklistItemDto> Items { get; init; } = [];
}

/// <summary>
/// Represents a single item in a checklist.
/// </summary>
public sealed record ChecklistItemDto
{
    /// <summary>
    /// Unique identifier for the checklist item.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Item title/description.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Whether the item is completed.
    /// </summary>
    public bool IsCompleted { get; init; }

    /// <summary>
    /// Position within the checklist (lower = higher up).
    /// </summary>
    public required int Position { get; init; }
}

/// <summary>
/// Type of dependency between cards.
/// </summary>
public enum CardDependencyType
{
    /// <summary>This card is blocked by the dependency card.</summary>
    BlockedBy,

    /// <summary>This card relates to the dependency card.</summary>
    RelatesTo
}

/// <summary>
/// Represents a dependency between two cards.
/// </summary>
public sealed record CardDependencyDto
{
    /// <summary>
    /// The card that has the dependency.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// The card that is depended upon.
    /// </summary>
    public required Guid DependsOnCardId { get; init; }

    /// <summary>
    /// Title of the depended-upon card (for display).
    /// </summary>
    public string? DependsOnCardTitle { get; init; }

    /// <summary>
    /// The type of dependency relationship.
    /// </summary>
    public required CardDependencyType Type { get; init; }
}

/// <summary>
/// Status of a sprint.
/// </summary>
public enum SprintStatus
{
    /// <summary>Sprint is being planned, not yet started.</summary>
    Planning,

    /// <summary>Sprint is currently active.</summary>
    Active,

    /// <summary>Sprint has been completed.</summary>
    Completed
}

/// <summary>
/// Represents a time-boxed sprint on a board.
/// </summary>
public sealed record SprintDto
{
    /// <summary>
    /// Unique identifier for the sprint.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The board this sprint belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// Sprint title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Sprint goal description.
    /// </summary>
    public string? Goal { get; init; }

    /// <summary>
    /// Start date of the sprint.
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// End date of the sprint.
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Current status of the sprint.
    /// </summary>
    public required SprintStatus Status { get; init; }

    /// <summary>
    /// Number of cards in this sprint.
    /// </summary>
    public int CardCount { get; init; }

    /// <summary>
    /// Total story points of cards in this sprint.
    /// </summary>
    public int TotalStoryPoints { get; init; }

    /// <summary>
    /// Story points completed in this sprint.
    /// </summary>
    public int CompletedStoryPoints { get; init; }

    /// <summary>
    /// Timestamp when the sprint was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the sprint was last modified.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Represents a time tracking entry on a card.
/// </summary>
public sealed record TimeEntryDto
{
    /// <summary>
    /// Unique identifier for the time entry.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The card this time entry is for.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// The user who logged the time.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Start time of the work period.
    /// </summary>
    public required DateTime StartTime { get; init; }

    /// <summary>
    /// End time of the work period. Null if timer is still running.
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// Duration in minutes. Computed from start/end or manually entered.
    /// </summary>
    public required int DurationMinutes { get; init; }

    /// <summary>
    /// Optional description of the work done.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Timestamp when the entry was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// An activity log entry for a board.
/// </summary>
public sealed record BoardActivityDto
{
    /// <summary>
    /// Unique identifier for the activity entry.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The board this activity occurred on.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The user who performed the action.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// The action that was performed.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Type of entity affected (e.g. "Card", "List", "Sprint").
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// ID of the affected entity.
    /// </summary>
    public required Guid EntityId { get; init; }

    /// <summary>
    /// Additional details as a JSON string.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Timestamp when the activity occurred.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}

// ─────────────────────────────────────────────
// Request DTOs
// ─────────────────────────────────────────────

/// <summary>
/// Request DTO for creating a new board.
/// </summary>
public sealed record CreateBoardDto
{
    /// <summary>
    /// Board title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Optional board description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Hex color code for UI display.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Optional team ID. When set, the board is owned by the team.
    /// </summary>
    public Guid? TeamId { get; init; }
}

/// <summary>
/// Request DTO for updating an existing board.
/// Only non-null fields are applied.
/// </summary>
public sealed record UpdateBoardDto
{
    /// <summary>
    /// Updated title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Updated description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Updated color.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Updated archived state.
    /// </summary>
    public bool? IsArchived { get; init; }
}

/// <summary>
/// Request DTO for creating a new list on a board.
/// </summary>
public sealed record CreateBoardListDto
{
    /// <summary>
    /// List title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Hex color code for UI display.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Maximum number of cards (WIP limit). Null means unlimited.
    /// </summary>
    public int? CardLimit { get; init; }
}

/// <summary>
/// Request DTO for updating a list.
/// Only non-null fields are applied.
/// </summary>
public sealed record UpdateBoardListDto
{
    /// <summary>
    /// Updated title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Updated color.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Updated WIP limit.
    /// </summary>
    public int? CardLimit { get; init; }
}

/// <summary>
/// Request DTO for creating a new card.
/// </summary>
public sealed record CreateCardDto
{
    /// <summary>
    /// Card title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Markdown description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Card priority level.
    /// </summary>
    public CardPriority Priority { get; init; }

    /// <summary>
    /// Optional due date.
    /// </summary>
    public DateTime? DueDate { get; init; }

    /// <summary>
    /// Story points estimate.
    /// </summary>
    public int? StoryPoints { get; init; }

    /// <summary>
    /// User IDs to assign to the card.
    /// </summary>
    public IReadOnlyList<Guid> AssigneeIds { get; init; } = [];

    /// <summary>
    /// Label IDs to apply to the card.
    /// </summary>
    public IReadOnlyList<Guid> LabelIds { get; init; } = [];
}

/// <summary>
/// Request DTO for updating a card.
/// Only non-null fields are applied.
/// </summary>
public sealed record UpdateCardDto
{
    /// <summary>
    /// Updated title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Updated description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Updated priority.
    /// </summary>
    public CardPriority? Priority { get; init; }

    /// <summary>
    /// Updated due date.
    /// </summary>
    public DateTime? DueDate { get; init; }

    /// <summary>
    /// Updated story points.
    /// </summary>
    public int? StoryPoints { get; init; }

    /// <summary>
    /// Updated archived state.
    /// </summary>
    public bool? IsArchived { get; init; }
}

/// <summary>
/// Request DTO for moving a card to a different list and/or position.
/// </summary>
public sealed record MoveCardDto
{
    /// <summary>
    /// Target list ID.
    /// </summary>
    public required Guid TargetListId { get; init; }

    /// <summary>
    /// Target position within the list.
    /// </summary>
    public required int Position { get; init; }
}

/// <summary>
/// Request DTO for creating a label on a board.
/// </summary>
public sealed record CreateLabelDto
{
    /// <summary>
    /// Label title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Hex color code for the label.
    /// </summary>
    public required string Color { get; init; }
}

/// <summary>
/// Request DTO for updating a label.
/// Only non-null fields are applied.
/// </summary>
public sealed record UpdateLabelDto
{
    /// <summary>
    /// Updated title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Updated color.
    /// </summary>
    public string? Color { get; init; }
}

/// <summary>
/// Request DTO for creating a sprint.
/// </summary>
public sealed record CreateSprintDto
{
    /// <summary>
    /// Sprint title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Sprint goal description.
    /// </summary>
    public string? Goal { get; init; }

    /// <summary>
    /// Planned start date.
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Planned end date.
    /// </summary>
    public DateTime? EndDate { get; init; }
}

/// <summary>
/// Request DTO for updating a sprint.
/// Only non-null fields are applied.
/// </summary>
public sealed record UpdateSprintDto
{
    /// <summary>
    /// Updated title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Updated goal.
    /// </summary>
    public string? Goal { get; init; }

    /// <summary>
    /// Updated start date.
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Updated end date.
    /// </summary>
    public DateTime? EndDate { get; init; }
}

/// <summary>
/// Request DTO for creating a time entry.
/// </summary>
public sealed record CreateTimeEntryDto
{
    /// <summary>
    /// Start time of the work period.
    /// </summary>
    public required DateTime StartTime { get; init; }

    /// <summary>
    /// End time of the work period.
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// Duration in minutes. Required if EndTime is not provided.
    /// </summary>
    public int? DurationMinutes { get; init; }

    /// <summary>
    /// Optional description of the work done.
    /// </summary>
    public string? Description { get; init; }
}

// ─────────────────────────────────────────────
// Planning Poker
// ─────────────────────────────────────────────

/// <summary>
/// Status of a planning poker session.
/// </summary>
public enum PokerSessionStatus
{
    /// <summary>Voting is open — estimates are hidden.</summary>
    Voting,

    /// <summary>Votes have been revealed to all participants.</summary>
    Revealed,

    /// <summary>Session completed — an estimate was accepted and applied.</summary>
    Completed,

    /// <summary>Session was cancelled without accepting an estimate.</summary>
    Cancelled
}

/// <summary>
/// Predefined estimation scales for planning poker.
/// </summary>
public enum PokerScale
{
    /// <summary>Fibonacci sequence: 0, 1, 2, 3, 5, 8, 13, 21, 34.</summary>
    Fibonacci,

    /// <summary>T-shirt sizes: XS, S, M, L, XL, XXL.</summary>
    TShirt,

    /// <summary>Powers of two: 0, 1, 2, 4, 8, 16, 32.</summary>
    PowersOfTwo,

    /// <summary>Custom scale defined by the session creator.</summary>
    Custom
}

/// <summary>
/// Represents a planning poker estimation session for a card.
/// </summary>
public sealed record PokerSessionDto
{
    /// <summary>
    /// Unique identifier for the session.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The card being estimated.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// The board the card belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The user who started the session.
    /// </summary>
    public required Guid CreatedByUserId { get; init; }

    /// <summary>
    /// The estimation scale used.
    /// </summary>
    public required PokerScale Scale { get; init; }

    /// <summary>
    /// Custom scale values when Scale is Custom (JSON array of strings).
    /// </summary>
    public string? CustomScaleValues { get; init; }

    /// <summary>
    /// Current session status.
    /// </summary>
    public required PokerSessionStatus Status { get; init; }

    /// <summary>
    /// The accepted estimate (set when Status is Completed).
    /// </summary>
    public string? AcceptedEstimate { get; init; }

    /// <summary>
    /// Current round number (increments on re-vote).
    /// </summary>
    public required int Round { get; init; }

    /// <summary>
    /// Votes cast in the current session. Empty until votes are revealed.
    /// </summary>
    public IReadOnlyList<PokerVoteDto> Votes { get; init; } = [];

    /// <summary>
    /// Timestamp when the session was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the session was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Represents a single vote in a planning poker session.
/// </summary>
public sealed record PokerVoteDto
{
    /// <summary>
    /// The user who cast the vote.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The voter's display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// The estimate value (e.g., "5", "M", "?").
    /// </summary>
    public required string Estimate { get; init; }

    /// <summary>
    /// Timestamp when the vote was cast.
    /// </summary>
    public required DateTime VotedAt { get; init; }

    /// <summary>
    /// Which round this vote belongs to.
    /// </summary>
    public required int Round { get; init; }
}

/// <summary>
/// Request DTO for starting a planning poker session.
/// </summary>
public sealed record CreatePokerSessionDto
{
    /// <summary>
    /// The estimation scale to use.
    /// </summary>
    public required PokerScale Scale { get; init; }

    /// <summary>
    /// Custom scale values when Scale is Custom (e.g., ["XS","S","M","L","XL"]).
    /// </summary>
    public string? CustomScaleValues { get; init; }
}

/// <summary>
/// Request DTO for submitting a vote in a planning poker session.
/// </summary>
public sealed record SubmitPokerVoteDto
{
    /// <summary>
    /// The estimate value (e.g., "5", "M", "?").
    /// </summary>
    public required string Estimate { get; init; }
}

/// <summary>
/// Request DTO for accepting an estimate and completing a poker session.
/// </summary>
public sealed record AcceptPokerEstimateDto
{
    /// <summary>
    /// The accepted estimate value to apply to the card's StoryPoints.
    /// </summary>
    public required string AcceptedEstimate { get; init; }

    /// <summary>
    /// The story points value to set on the card. Null for non-numeric scales.
    /// </summary>
    public int? StoryPoints { get; init; }
}

// ─────────────────────────────────────────────
// Tracks Teams
// ─────────────────────────────────────────────

/// <summary>
/// Role a user can have on a Tracks team.
/// </summary>
public enum TracksTeamMemberRole
{
    /// <summary>Standard team member — inherits Board Member access on team boards.</summary>
    Member,

    /// <summary>Team manager — can manage members and inherits Board Admin access on team boards.</summary>
    Manager,

    /// <summary>Team owner — full control including team deletion. Inherits Board Owner access on team boards.</summary>
    Owner
}

/// <summary>
/// Represents a Tracks team that can own boards and sprints.
/// Team identity comes from Core; this DTO combines Core team info with Tracks-specific role data.
/// </summary>
public sealed record TracksTeamDto
{
    /// <summary>
    /// Core team ID.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Organization the team belongs to.
    /// </summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// Team name (from Core).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional team description (from Core).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Timestamp when the team was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Members of this team with their Tracks-specific roles.
    /// </summary>
    public IReadOnlyList<TracksTeamMemberDto> Members { get; init; } = [];

    /// <summary>
    /// Number of boards owned by this team in the Tracks module.
    /// </summary>
    public int BoardCount { get; init; }
}

/// <summary>
/// Represents a user's membership on a Tracks team.
/// </summary>
public sealed record TracksTeamMemberDto
{
    /// <summary>
    /// The user's ID.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// The member's role on this team.
    /// </summary>
    public required TracksTeamMemberRole Role { get; init; }

    /// <summary>
    /// Timestamp when the user joined the team.
    /// </summary>
    public required DateTime JoinedAt { get; init; }
}

/// <summary>
/// Request DTO for creating a new Tracks team.
/// </summary>
public sealed record CreateTracksTeamDto
{
    /// <summary>
    /// Team name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional team description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Organization ID for the team. If null, the system default organization is used.
    /// </summary>
    public Guid? OrganizationId { get; init; }
}

/// <summary>
/// Request DTO for updating an existing Tracks team.
/// Only non-null fields are applied.
/// </summary>
public sealed record UpdateTracksTeamDto
{
    /// <summary>
    /// Updated name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Updated description.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Request DTO for transferring a board to or from a team.
/// </summary>
public sealed record TransferBoardDto
{
    /// <summary>
    /// The team ID to transfer the board to. Null to make it a personal board.
    /// </summary>
    public Guid? TeamId { get; init; }
}

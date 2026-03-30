namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Delivers real-time Tracks event signals from the server to Blazor UI components.
/// Blazor InteractiveServer components subscribe to these C# events and call
/// <c>StateHasChanged()</c> to refresh the UI.
/// </summary>
/// <remarks>
/// Register a concrete implementation backed by the event bus or a <c>HubConnection</c>
/// in the application host. The <see cref="NullTracksSignalRService"/> stub is registered
/// by default so components remain functional without a live connection.
/// </remarks>
public interface ITracksSignalRService
{
    /// <summary>Whether the service is actively delivering events.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Raised when a card action occurs on a board (created, updated, moved, deleted, assigned).
    /// Args: boardId, cardId, action.
    /// </summary>
    event Action<Guid, Guid, string>? CardActionReceived;

    /// <summary>
    /// Raised when a swimlane action occurs on a board (created, updated, deleted).
    /// Args: boardId, swimlaneId, action.
    /// </summary>
    event Action<Guid, Guid, string>? SwimlaneActionReceived;

    /// <summary>
    /// Raised when a comment action occurs on a card (added, updated, deleted).
    /// Args: boardId, cardId, commentId, action.
    /// </summary>
    event Action<Guid, Guid, Guid, string>? CommentActionReceived;

    /// <summary>
    /// Raised when a sprint action occurs on a board (started, completed, created, deleted).
    /// Args: boardId, sprintId, action.
    /// </summary>
    event Action<Guid, Guid, string>? SprintActionReceived;

    /// <summary>
    /// Raised when an activity entry is recorded on a board.
    /// Args: boardId.
    /// </summary>
    event Action<Guid>? ActivityReceived;

    /// <summary>
    /// Raised when a board member changes (added, removed, role_updated).
    /// Args: boardId, userId, action.
    /// </summary>
    event Action<Guid, Guid, string>? BoardMemberActionReceived;

    /// <summary>
    /// Raised when a team changes (created, deleted, member_added, member_removed).
    /// Args: teamId, action.
    /// </summary>
    event Action<Guid, string>? TeamActionReceived;
}

/// <summary>
/// No-op implementation used when no real-time connection is configured.
/// All event subscriptions are silently ignored.
/// </summary>
internal sealed class NullTracksSignalRService : ITracksSignalRService
{
    /// <inheritdoc />
    public bool IsActive => false;

    /// <inheritdoc />
#pragma warning disable CS0067 // Event is never used — intentional for null-object stub
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
#pragma warning restore CS0067
}

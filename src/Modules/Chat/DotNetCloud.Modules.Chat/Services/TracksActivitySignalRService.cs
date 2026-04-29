namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Delivers real-time Tracks activity signals to Chat Blazor UI components.
/// Components subscribe to events to display product activity in the Chat interface.
/// </summary>
/// <remarks>
/// <para>
/// This service is an <b>optional</b> integration point — when the Tracks module
/// is not installed, the <see cref="NullTracksActivitySignalRService"/> stub is used
/// and all events are silently ignored. Components degrade gracefully by hiding
/// Tracks-related UI elements when <see cref="IsActive"/> is <c>false</c>.
/// </para>
/// <para>
/// Register a concrete implementation in the application host when the Tracks module
/// is present. The concrete implementation should subscribe to SignalR hub events
/// for the <c>TracksActivityNotification</c> event name and fire the appropriate
/// C# event on this interface.
/// </para>
/// </remarks>
public interface ITracksActivitySignalRService
{
    /// <summary>Whether the service is actively delivering Tracks events.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Raised when any Tracks activity occurs (work item created/moved/deleted, sprint started, etc.).
    /// Args: action, productId, timestamp.
    /// </summary>
    event Action<TracksActivitySignal>? ActivityReceived;

    /// <summary>
    /// Raised when the current user is assigned to a work item.
    /// Args: workItemId, productId, assignedByUserId.
    /// </summary>
    event Action<Guid, Guid, Guid>? WorkItemAssignedToMe;
}

/// <summary>
/// A lightweight signal describing a Tracks product activity event.
/// </summary>
public sealed record TracksActivitySignal
{
    /// <summary>The action that occurred (e.g., workitem_created, workitem_moved, sprint_started).</summary>
    public required string Action { get; init; }

    /// <summary>The product the activity occurred on.</summary>
    public required Guid ProductId { get; init; }

    /// <summary>When the activity occurred.</summary>
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// No-op implementation used when the Tracks module is not installed.
/// All event subscriptions are silently ignored.
/// </summary>
internal sealed class NullTracksActivitySignalRService : ITracksActivitySignalRService
{
    /// <inheritdoc />
    public bool IsActive => false;

    /// <inheritdoc />
#pragma warning disable CS0067 // Event is never used — intentional for null-object stub
    public event Action<TracksActivitySignal>? ActivityReceived;
    /// <inheritdoc />
    public event Action<Guid, Guid, Guid>? WorkItemAssignedToMe;
#pragma warning restore CS0067
}

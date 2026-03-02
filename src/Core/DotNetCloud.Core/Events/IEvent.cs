namespace DotNetCloud.Core.Events;

/// <summary>
/// Marker interface for all events in the DotNetCloud system.
/// All events published through the event bus must implement this interface.
/// </summary>
public interface IEvent
{
    /// <summary>
    /// Gets the unique identifier for this event instance.
    /// Enables tracking and correlation of events across the system.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the timestamp when the event was created.
    /// Used for ordering, auditing, and event replay.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the source module ID that published this event.
    /// Enables filtering and routing events by origin.
    /// </summary>
    string SourceModuleId { get; }
}

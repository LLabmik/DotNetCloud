namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised when a resource is shared with a user.
/// Triggers in-app notification for the recipient.
/// </summary>
public sealed record ResourceSharedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the user who shared the resource.
    /// </summary>
    public required Guid SharedByUserId { get; init; }

    /// <summary>
    /// The ID of the user the resource was shared with.
    /// </summary>
    public required Guid SharedWithUserId { get; init; }

    /// <summary>
    /// The module that owns the shared resource (e.g., "dotnetcloud.files", "dotnetcloud.notes").
    /// </summary>
    public required string SourceModuleId { get; init; }

    /// <summary>
    /// The type of entity that was shared.
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// The ID of the shared entity.
    /// </summary>
    public required Guid EntityId { get; init; }

    /// <summary>
    /// Display name of the shared entity.
    /// </summary>
    public required string EntityDisplayName { get; init; }

    /// <summary>
    /// Permission level granted (e.g., "ReadOnly", "ReadWrite").
    /// </summary>
    public required string Permission { get; init; }
}

/// <summary>
/// Raised when a user is mentioned in content (note, chat message, comment).
/// </summary>
public sealed record UserMentionedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the user who was mentioned.
    /// </summary>
    public required Guid MentionedUserId { get; init; }

    /// <summary>
    /// The ID of the user who created the mention.
    /// </summary>
    public required Guid MentionedByUserId { get; init; }

    /// <summary>
    /// The module where the mention occurred.
    /// </summary>
    public required string SourceModuleId { get; init; }

    /// <summary>
    /// The type of content containing the mention (e.g., "Note", "ChatMessage").
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// The ID of the content containing the mention.
    /// </summary>
    public required Guid ContentId { get; init; }

    /// <summary>
    /// Display title or excerpt of the content.
    /// </summary>
    public required string ContentTitle { get; init; }
}

/// <summary>
/// Raised when a reminder fires for a user.
/// </summary>
public sealed record ReminderTriggeredEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The user who should receive the reminder.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The module that owns the entity being reminded about.
    /// </summary>
    public required string SourceModuleId { get; init; }

    /// <summary>
    /// The type of entity (e.g., "CalendarEvent", "Task").
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// The ID of the entity.
    /// </summary>
    public required Guid EntityId { get; init; }

    /// <summary>
    /// Display title for the reminder.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// When the event/task occurs (UTC).
    /// </summary>
    public DateTime? DueAtUtc { get; init; }
}

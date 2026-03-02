namespace DotNetCloud.Core.Events;

/// <summary>
/// Marker interface for all domain events in the DotNetCloud system.
/// 
/// All events published through the event bus must implement this interface.
/// Events represent significant things that happened in the system that
/// other modules might want to react to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Event Contract:</b>
/// 
/// All events must provide:
/// <list type="bullet">
///   <item><description>EventId - unique identifier for this event (correlation/tracing)</description></item>
///   <item><description>CreatedAt - timestamp when event was created (ordering/auditing)</description></item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Event Design Principles:</b>
/// 
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Immutability:</b> Use records instead of classes for immutable event definitions
///       <code>
///       public sealed record UserCreatedEvent : IEvent
///       {
///           public required Guid EventId { get; init; }
///           public required DateTime CreatedAt { get; init; }
///           public required Guid UserId { get; init; }
///       }
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Minimal Data:</b> Include only data needed by subscribers. Reference larger
///       objects by ID rather than including full data.
///       <code>
///       // Good: subscribers can fetch full document if needed
///       public sealed record DocumentCreatedEvent : IEvent
///       {
///           public Guid DocumentId { get; init; }
///           public Guid OwnerId { get; init; }
///           // Not: public Document FullDocument { get; init; }
///       }
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>No Sensitive Data:</b> Never include passwords, tokens, or encrypted data
///       in events (they are logged and audited)
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Past Tense Naming:</b> Event names should be past tense describing what happened
///       <code>
///       UserCreatedEvent    ✓ Good
///       UserCreating        ✗ Bad (not past tense)
///       CreateUserEvent     ✗ Bad (not past tense)
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Documentation:</b> Document when/why events are raised so subscribers understand context
///     </description>
///   </item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Example Domain Event:</b>
/// 
/// <code>
/// namespace DotNetCloud.Core.Events;
/// 
/// /// &lt;summary&gt;
/// /// Published when a user is successfully created in the system.
/// /// Raised by UserService after user account is persisted to database.
/// /// &lt;/summary&gt;
/// public sealed record UserCreatedEvent : IEvent
/// {
///     /// Unique identifier for this event instance (for correlation/tracing)
///     public required Guid EventId { get; init; }
///     
///     /// When the event was created (UTC)
///     public required DateTime CreatedAt { get; init; }
///     
///     /// ID of the newly created user
///     public required Guid UserId { get; init; }
///     
///     /// Email address provided during signup
///     public required string Email { get; init; }
///     
///     /// Display name selected by user
///     public required string DisplayName { get; init; }
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Publishing Events:</b>
/// 
/// <code>
/// public class UserService
/// {
///     private readonly IEventBus _eventBus;
///     
///     public async Task CreateUserAsync(CreateUserDto dto, CallerContext caller, CancellationToken ct)
///     {
///         // Validate and create user
///         var user = new User { /* ... */ };
///         
///         // Save to database
///         await _dbContext.SaveChangesAsync(ct);
///         
///         // Publish event with required properties
///         var @event = new UserCreatedEvent
///         {
///             EventId = Guid.NewGuid(),           // Unique event ID
///             CreatedAt = DateTime.UtcNow,        // Current time in UTC
///             UserId = user.Id,
///             Email = user.Email,
///             DisplayName = user.DisplayName
///         };
///         
///         await _eventBus.PublishAsync(@event, caller, ct);
///     }
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Event Bus Guarantees:</b>
/// 
/// <list type="bullet">
///   <item><description>Fire-and-forget: PublishAsync returns after event is queued, not after delivery</description></item>
///   <item><description>Async delivery: Handlers are executed asynchronously (may not complete immediately)</description></item>
///   <item><description>No ordering guarantees: If multiple modules raise events, ordering is not guaranteed</description></item>
///   <item><description>Handler isolation: Exception in one handler doesn't affect other handlers</description></item>
/// </list>
/// </para>
/// </remarks>
/// <seealso cref="IEventHandler{TEvent}"/>
/// <seealso cref="IEventBus"/>
public interface IEvent
{
    /// <summary>
    /// Gets the unique identifier for this event instance.
    /// 
    /// Used for event correlation, tracing, and deduplication.
    /// Each event instance should have a unique EventId.
    /// </summary>
    /// <remarks>
    /// Typically generated when the event is created:
    /// <code>
    /// EventId = Guid.NewGuid()
    /// </code>
    /// 
    /// Used for:
    /// <list type="bullet">
    ///   <item><description>Distributed tracing (correlate across services)</description></item>
    ///   <item><description>Deduplication (detect if event was processed multiple times)</description></item>
    ///   <item><description>Debugging (search logs for specific event)</description></item>
    ///   <item><description>Auditing (link event to system changes)</description></item>
    /// </list>
    /// </remarks>
    Guid EventId { get; }

    /// <summary>
    /// Gets the timestamp when the event was created.
    /// 
    /// Always in UTC. Used for ordering events, auditing, and event replay scenarios.
    /// </summary>
    /// <remarks>
    /// Should be set when the event is created:
    /// <code>
    /// CreatedAt = DateTime.UtcNow
    /// </code>
    /// 
    /// Used for:
    /// <list type="bullet">
    ///   <item><description>Chronological ordering of events</description></item>
    ///   <item><description>Auditing and compliance (when did this happen)</description></item>
    ///   <item><description>Event replay (replay from specific timestamp)</description></item>
    ///   <item><description>Time-based filtering and queries</description></item>
    ///   <item><description>Performance monitoring (event processing latency)</description></item>
    /// </list>
    /// </remarks>
    DateTime CreatedAt { get; }
}

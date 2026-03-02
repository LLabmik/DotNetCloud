namespace DotNetCloud.Core.Events;

/// <summary>
/// Generic interface for handling events of a specific type.
/// 
/// Modules implement this interface to subscribe to and react to domain events.
/// Handlers enable loosely-coupled communication between modules through an event-driven architecture.
/// </summary>
/// <typeparam name="TEvent">
/// The specific event type this handler processes. Must implement <see cref="IEvent"/>.
/// Use covariance to group related events.
/// </typeparam>
/// <remarks>
/// <para>
/// <b>Handler Implementation Pattern:</b>
/// 
/// <code>
/// public class UserCreatedEventHandler : IEventHandler&lt;UserCreatedEvent&gt;
/// {
///     private readonly INotificationService _notifications;
///     private readonly IModuleSettings _settings;
///     
///     public UserCreatedEventHandler(
///         INotificationService notifications,
///         IModuleSettings settings)
///     {
///         _notifications = notifications;
///         _settings = settings;
///     }
///     
///     public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken)
///     {
///         try
///         {
///             // 1. Load any needed context
///             var settings = await _settings.GetAsync(@event.UserId, cancellationToken);
///             
///             // 2. Perform handler logic
///             await _notifications.SendAsync(
///                 @event.UserId,
///                 "Welcome",
///                 $"Welcome to DotNetCloud!",
///                 cancellationToken);
///             
///             // 3. Update any downstream systems
///             // (e.g., sync to analytics, update cache, etc.)
///         }
///         catch (Exception ex)
///         {
///             // 4. Log error but don't throw
///             // (Event bus will catch and log; don't cascade failures)
///             Console.WriteLine($"Error in UserCreatedEventHandler: {ex.Message}");
///         }
///     }
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Handler Registration Pattern:</b>
/// 
/// Handlers are registered during module initialization:
/// 
/// <code>
/// public class MyModule : IModule
/// {
///     private readonly IEventBus _eventBus;
///     
///     public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
///     {
///         // Create handler instance (or get from DI)
///         var handler = new UserCreatedEventHandler(
///             context.Services.GetRequiredService&lt;INotificationService&gt;(),
///             context.Services.GetRequiredService&lt;IModuleSettings&gt;());
///         
///         // Subscribe to events
///         await _eventBus.SubscribeAsync(handler, cancellationToken);
///     }
///     
///     public async Task StopAsync(CancellationToken cancellationToken)
///     {
///         // Unsubscribe during shutdown
///         await _eventBus.UnsubscribeAsync(handler, cancellationToken);
///     }
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Handler Best Practices:</b>
/// 
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Keep handlers fast:</b> Complete quickly; dispatch long operations to background jobs.
///       Event bus may be waiting for handler completion.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Handle exceptions internally:</b> Catch and log exceptions; don't throw (prevents
///       cascading failures to other handlers).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Implement idempotency:</b> Handlers may be called multiple times for the same event
///       (due to retries or replays). Ensure repeated calls are safe.
///       <code>
///       // Good: Check if already processed
///       public async Task HandleAsync(UserCreatedEvent @event, CancellationToken ct)
///       {
///           if (await _db.UserSettings.AnyAsync(u => u.UserId == @event.UserId, ct))
///               return;  // Already processed
///           
///           // Create settings
///           var settings = new UserSettings { UserId = @event.UserId };
///           _db.Add(settings);
///           await _db.SaveChangesAsync(ct);
///       }
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Respect cancellation tokens:</b> Check IsCancellationRequested in loops;
///       pass tokens to async operations.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Use specific event types:</b> Handlers should handle one specific event type,
///       not a base event interface.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Log important decisions:</b> Log when handlers take action, for debugging and auditing.
///     </description>
///   </item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Handler Guarantees:</b>
/// 
/// <list type="bullet">
///   <item><description>Async: Handlers are executed asynchronously; no guaranteed order</description></item>
///   <item><description>Isolation: Exception in one handler doesn't affect others</description></item>
///   <item><description>Guaranteed delivery: Each registered handler receives each event</description></item>
///   <item><description>No ordering: Multiple handlers may run concurrently</description></item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Common Event Handler Patterns:</b>
/// 
/// <list type="number">
///   <item>
///     <description>
///       <b>Reactive Processing:</b> React immediately to event, perform side-effect
///       <code>
///       public class EmailNotificationEventHandler : IEventHandler&lt;UserCreatedEvent&gt;
///       {
///           public async Task HandleAsync(UserCreatedEvent @event, CancellationToken ct)
///           {
///               await _emailService.SendWelcomeAsync(@event.Email, ct);
///           }
///       }
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Data Synchronization:</b> Keep related data in sync across modules
///       <code>
///       public class UserCacheInvalidationHandler : IEventHandler&lt;UserUpdatedEvent&gt;
///       {
///           public async Task HandleAsync(UserUpdatedEvent @event, CancellationToken ct)
///           {
///               await _cache.InvalidateAsync($"user:{@event.UserId}", ct);
///           }
///       }
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Cascading Operations (Saga):</b> Multi-step workflow across modules
///       <code>
///       public class OrganizationSetupSaga : 
///           IEventHandler&lt;OrganizationCreatedEvent&gt;
///       {
///           // Reacts to organization creation
///           // Publishes new events to trigger cascading setup
///           public async Task HandleAsync(OrganizationCreatedEvent @event, CancellationToken ct)
///           {
///               // Create default team
///               // Create default channels
///               // Publish events for other modules to react
///           }
///       }
///       </code>
///     </description>
///   </item>
/// </list>
/// </para>
/// </remarks>
/// <seealso cref="IEvent"/>
/// <seealso cref="IEventBus"/>
public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    /// <summary>
    /// Handles the specified event asynchronously.
    /// 
    /// Implementations should be idempotent, as events may be delivered multiple times
    /// due to retries, system restarts, or event replay scenarios.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the handler operation. Handlers should respect this token
    /// and stop processing if cancellation is requested.
    /// </param>
    /// <returns>A task representing the asynchronous handling operation.</returns>
    /// <remarks>
    /// <para>
    /// <b>Execution Semantics:</b>
    /// 
    /// <list type="bullet">
    ///   <item><description>Executed asynchronously (may not complete immediately)</description></item>
    ///   <item><description>May run concurrently with other handlers for different events</description></item>
    ///   <item><description>Event bus catches exceptions; handlers should log internally</description></item>
    ///   <item><description>May be called multiple times for same event (be idempotent)</description></item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Exception Handling:</b>
    /// 
    /// Don't throw exceptions; catch and log internally. Throwing prevents cascading
    /// execution to other handlers and complicates the event bus logic.
    /// </para>
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// When cancellationToken is canceled. Handlers should respect this gracefully.
    /// </exception>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

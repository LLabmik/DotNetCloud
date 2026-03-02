namespace DotNetCloud.Core.Modules;

/// <summary>
/// Defines the core interface for a module in the DotNetCloud system.
/// 
/// Modules are plugin-style components that extend the platform's functionality.
/// They follow a strict lifecycle (Initialize → Start → Running → Stop) and communicate
/// with other modules through events, ensuring loose coupling and isolation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Module Lifecycle:</b>
/// 
/// Each module follows a well-defined lifecycle with distinct phases:
/// 
/// <code>
/// [Discovery] 
///     ↓
/// [Loading]
///     ↓
/// [Initialization] ← Module.InitializeAsync()
///     ↓
/// [Started] ← Module.StartAsync()
///     ↓
/// [Running] ← Module processes events and requests
///     ↓
/// [Stopping] ← Module.StopAsync()
///     ↓
/// [Stopped] ← Module is unloaded
/// </code>
/// </para>
/// 
/// <para>
/// <b>Lifecycle Guarantees:</b>
/// 
/// <list type="bullet">
///   <item>
///     <description>
///       <b>InitializeAsync is called exactly once:</b> before StartAsync, 
///       to set up the module's internal state
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>StartAsync is called exactly once:</b> after all modules have initialized, 
///       to begin accepting work
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>StopAsync is called exactly once:</b> during system shutdown, 
///       to gracefully release resources
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>InitializeAsync must complete before StartAsync:</b> system waits for all 
///       initializations to complete before starting any module
///     </description>
///   </item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Example Implementation:</b>
/// 
/// <code>
/// public class DocumentsModule : IModule
/// {
///     private readonly IStorageProvider _storage;
///     private readonly IEventBus _eventBus;
///     private CancellationTokenSource? _shutdownCts;
///     
///     public IModuleManifest Manifest =&gt; new DocumentsModuleManifest();
///     
///     public DocumentsModule(IStorageProvider storage, IEventBus eventBus)
///     {
///         _storage = storage;
///         _eventBus = eventBus;
///     }
///     
///     public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
///     {
///         try
///         {
///             // 1. Validate required capabilities are available
///             if (_storage == null)
///                 throw new InvalidOperationException("IStorageProvider capability required");
///             
///             // 2. Load configuration
///             var config = context.Configuration["documents"] as string;
///             _settings = ParseSettings(config);
///             
///             // 3. Subscribe to events
///             var handler = new UserDeletedEventHandler(_storage);
///             await _eventBus.SubscribeAsync(handler, cancellationToken);
///             
///             // 4. Setup internal state
///             _initialized = true;
///         }
///         catch (Exception ex)
///         {
///             // Log and rethrow - don't silently fail
///             Console.WriteLine($"Failed to initialize Documents module: {ex.Message}");
///             throw;
///         }
///     }
///     
///     public async Task StartAsync(CancellationToken cancellationToken)
///     {
///         try
///         {
///             if (!_initialized)
///                 throw new InvalidOperationException("Module not initialized");
///             
///             _shutdownCts = new CancellationTokenSource();
///             
///             // Start background tasks
///             _ = ProcessUploadQueueAsync(_shutdownCts.Token);
///             
///             // Establish external connections
///             // ...
///             
///             _running = true;
///             Console.WriteLine("Documents module started");
///         }
///         catch (Exception ex)
///         {
///             Console.WriteLine($"Failed to start Documents module: {ex.Message}");
///             throw;
///         }
///     }
///     
///     public async Task StopAsync(CancellationToken cancellationToken)
///     {
///         try
///         {
///             _running = false;
///             
///             // Signal background tasks to stop
///             _shutdownCts?.Cancel();
///             
///             // Wait for in-flight operations to complete (with timeout)
///             using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
///             using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
///             
///             await _backgroundProcessor.ConfigureAwait(false);
///             
///             // Close connections gracefully
///             // ...
///             
///             // Unsubscribe from events
///             await _eventBus.UnsubscribeAsync(new UserDeletedEventHandler(_storage));
///             
///             Console.WriteLine("Documents module stopped");
///         }
///         catch (Exception ex)
///         {
///             // Log but don't throw - attempt graceful shutdown anyway
///             Console.WriteLine($"Error stopping Documents module: {ex.Message}");
///         }
///     }
///     
///     private async Task ProcessUploadQueueAsync(CancellationToken cancellationToken)
///     {
///         while (!cancellationToken.IsCancellationRequested)
///         {
///             try
///             {
///                 // Process work
///                 await Task.Delay(1000, cancellationToken);
///             }
///             catch (OperationCanceledException)
///             {
///                 // Expected during shutdown
///                 break;
///             }
///         }
///     }
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Best Practices:</b>
/// 
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Keep InitializeAsync fast:</b> Don't perform long operations; prefer lazy-loading in StartAsync
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Validate manifest requirements:</b> Throw if required capabilities are missing
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Use async/await throughout:</b> Never block threads with Result or Wait()
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Respect cancellation tokens:</b> Pass tokens through and check IsCancellationRequested
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Log lifecycle events:</b> Help operators understand what's happening
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Implement IModuleLifecycle for async disposal:</b> If using unmanaged resources
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Don't throw from StopAsync:</b> Attempt graceful shutdown; log errors instead
///     </description>
///   </item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Failure Handling:</b>
/// 
/// <list type="bullet">
///   <item>
///     <description>
///       <b>InitializeAsync failure:</b> Module is not started; system logs error and continues. 
///       Module features are unavailable.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>StartAsync failure:</b> Module is not activated; system logs error and continues. 
///       Module features are unavailable.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>StopAsync failure:</b> System logs error and proceeds with shutdown. 
///       Resources may leak if not handled carefully.
///     </description>
///   </item>
/// </list>
/// </para>
/// </remarks>
/// <seealso cref="IModuleLifecycle"/>
/// <seealso cref="IModuleManifest"/>
/// <seealso cref="ModuleInitializationContext"/>
public interface IModule
{
    /// <summary>
    /// Gets the module's manifest containing metadata about capabilities, events, and requirements.
    /// </summary>
    /// <remarks>
    /// The manifest is immutable and should be consistent throughout the module's lifetime.
    /// </remarks>
    IModuleManifest Manifest { get; }

    /// <summary>
    /// Initializes the module with its context and configuration.
    /// Called exactly once when the module is first loaded into the system.
    /// </summary>
    /// <param name="context">
    /// The initialization context containing configuration, available capabilities, 
    /// and system caller context for executing system operations.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the initialization.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <remarks>
    /// <para>
    /// <b>When Called:</b> After module is loaded but before StartAsync
    /// </para>
    /// 
    /// <para>
    /// <b>Responsibilities:</b>
    /// 
    /// <list type="bullet">
    ///   <item><description>Validate required capabilities are available (injected as non-null)</description></item>
    ///   <item><description>Load and validate module configuration</description></item>
    ///   <item><description>Setup internal state and data structures</description></item>
    ///   <item><description>Register event subscriptions with the event bus</description></item>
    ///   <item><description>Perform one-time initialization tasks</description></item>
    ///   <item><description>Throw if initialization fails (don't silently continue)</description></item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Important:</b> Keep initialization fast. Don't connect to external services or perform
    /// long-running operations here. Defer those to StartAsync.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a required capability is missing or configuration is invalid.
    /// </exception>
    Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the module, enabling it to process events and requests.
    /// Called exactly once after all modules have completed initialization.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the startup.</param>
    /// <returns>A task representing the asynchronous startup operation.</returns>
    /// <remarks>
    /// <para>
    /// <b>When Called:</b> After InitializeAsync completes successfully for all modules
    /// </para>
    /// 
    /// <para>
    /// <b>Responsibilities:</b>
    /// 
    /// <list type="bullet">
    ///   <item><description>Begin processing events and requests</description></item>
    ///   <item><description>Start background tasks and timers</description></item>
    ///   <item><description>Establish external connections (to other services, APIs, etc.)</description></item>
    ///   <item><description>Perform any time-consuming setup deferred from InitializeAsync</description></item>
    ///   <item><description>Ensure idempotency - can be called again if first call fails</description></item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Important:</b> Module should be responsive after this completes. Don't block
    /// indefinitely waiting for external services; implement timeouts and retries.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the module has not been initialized or cannot be started.
    /// </exception>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the module gracefully, allowing it to complete in-flight operations.
    /// Called exactly once during system shutdown or module unloading.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to cancel the shutdown. Modules should attempt graceful shutdown regardless
    /// of this token's state, but may use it to implement a hard timeout.
    /// </param>
    /// <returns>A task representing the asynchronous shutdown operation.</returns>
    /// <remarks>
    /// <para>
    /// <b>When Called:</b> During system shutdown or explicit module stop
    /// </para>
    /// 
    /// <para>
    /// <b>Responsibilities:</b>
    /// 
    /// <list type="bullet">
    ///   <item><description>Signal background tasks to stop accepting new work</description></item>
    ///   <item><description>Wait for in-flight operations to complete (with reasonable timeout)</description></item>
    ///   <item><description>Gracefully close external connections</description></item>
    ///   <item><description>Unsubscribe from events</description></item>
    ///   <item><description>Release resources (connections, files, etc.)</description></item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Important:</b> This method should attempt graceful shutdown in all cases.
    /// Don't throw exceptions (log instead) - the system will force-kill the module if
    /// it doesn't complete in time.
    /// </para>
    /// </remarks>
    Task StopAsync(CancellationToken cancellationToken = default);
}

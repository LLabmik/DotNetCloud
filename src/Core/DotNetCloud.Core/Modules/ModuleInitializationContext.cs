namespace DotNetCloud.Core.Modules;

using DotNetCloud.Core.Authorization;

/// <summary>
/// Provides the context and resources needed for a module to initialize.
/// 
/// Contains configuration, dependency injection container, and access to platform capabilities.
/// Passed to IModule.InitializeAsync() to give modules everything they need to set up.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purpose:</b>
/// 
/// The initialization context provides modules with:
/// <list type="bullet">
///   <item><description>Module identification (ModuleId)</description></item>
///   <item><description>Dependency injection container (Services)</description></item>
///   <item><description>Configuration settings (Configuration)</description></item>
///   <item><description>System caller context (SystemCaller)</description></item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Typical Initialization Flow:</b>
/// 
/// <code>
/// public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
/// {
///     // 1. Access module ID
///     Console.WriteLine($"Initializing module: {context.ModuleId}");
///     
///     // 2. Get required capabilities from DI container
///     var eventBus = context.Services.GetRequiredService&lt;IEventBus&gt;();
///     var userDir = context.Services.GetRequiredService&lt;IUserDirectory&gt;();
///     
///     // Get optional capabilities (injected as null if not granted)
///     var storage = context.Services.GetService&lt;IStorageProvider&gt;();
///     if (storage == null)
///     {
///         Console.WriteLine("Warning: IStorageProvider not granted");
///     }
///     
///     // 3. Load configuration
///     if (context.Configuration.TryGetValue("max_file_size", out var maxSizeObj))
///     {
///         _maxFileSize = (long)maxSizeObj;
///     }
///     
///     // 4. Subscribe to events
///     var handler = new UserDeletedEventHandler(storage);
///     await eventBus.SubscribeAsync(handler, cancellationToken);
///     
///     // 5. Perform initialization
///     await LoadDefaultSettingsAsync(cancellationToken);
///     
///     Console.WriteLine($"Module {context.ModuleId} initialized successfully");
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Error Handling During Initialization:</b>
/// 
/// <code>
/// public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
/// {
///     try
///     {
///         // Validate required capabilities
///         var eventBus = context.Services.GetRequiredService&lt;IEventBus&gt;();
///         var userDir = context.Services.GetRequiredService&lt;IUserDirectory&gt;();
///         
///         // If required capability missing, GetRequiredService throws
///         // Module initialization fails, module is not started
///     }
///     catch (InvalidOperationException ex)
///     {
///         // Required service not found - fail fast
///         Console.WriteLine($"Required capability missing: {ex.Message}");
///         throw;  // Don't silently continue
///     }
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Configuration Access Pattern:</b>
/// 
/// <code>
/// public class DocumentsModuleManifest : IModuleManifest
/// {
///     // Manifest declares what configuration this module accepts
/// }
/// 
/// // Configuration format in system config:
/// {
///     "modules": {
///         "dotnetcloud.documents": {
///             "max_file_size": 104857600,  // 100MB
///             "enable_versioning": true,
///             "storage_path": "/var/lib/dotnetcloud/documents"
///         }
///     }
/// }
/// 
/// // In initialization:
/// public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken ct)
/// {
///     var moduleConfig = context.Configuration["dotnetcloud.documents"] as Dictionary&lt;string, object&gt;
///     if (moduleConfig != null)
///     {
///         _maxFileSize = Convert.ToInt64(moduleConfig["max_file_size"]);
///         _enableVersioning = Convert.ToBoolean(moduleConfig["enable_versioning"]);
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public record ModuleInitializationContext
{
    /// <summary>
    /// Gets the unique identifier of the module being initialized.
    /// 
    /// Typically in format "organization.modulename", e.g., "dotnetcloud.files", "dotnetcloud.chat".
    /// Used for logging, capability grants, and event routing.
    /// </summary>
    /// <remarks>
    /// This is the same ID as in <see cref="IModuleManifest.Id"/>.
    /// </remarks>
    public required string ModuleId { get; init; }

    /// <summary>
    /// Gets the service provider for dependency injection and capability resolution.
    /// 
    /// Use this to retrieve required capabilities (e.g., IUserDirectory, IEventBus)
    /// and other services the module needs during initialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Retrieving Services:</b>
    /// 
    /// <code>
    /// // Get required capability (throws if not available)
    /// var eventBus = context.Services.GetRequiredService&lt;IEventBus&gt;();
    /// 
    /// // Get optional capability (returns null if not granted)
    /// var storage = context.Services.GetService&lt;IStorageProvider&gt;();
    /// 
    /// // Get with fallback
    /// var notifications = context.Services.GetService&lt;INotificationService&gt;()
    ///     ?? throw new InvalidOperationException("INotificationService required");
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// <b>Services Available:</b>
    /// 
    /// <list type="bullet">
    ///   <item><description>Capability interfaces (public tier granted automatically)</description></item>
    ///   <item><description>Restricted/Privileged capabilities (only if granted to module)</description></item>
    ///   <item><description>IEventBus for event subscriptions</description></item>
    ///   <item><description>ILogger&lt;T&gt; for logging</description></item>
    ///   <item><description>Other platform services as needed</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Gets the configuration dictionary for the module.
    /// 
    /// Contains module-specific configuration values from the system configuration file.
    /// Format and content depend on module-specific configuration schema.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Configuration Format:</b>
    /// 
    /// Configuration is typically stored as a nested dictionary in the system config:
    /// 
    /// <code>
    /// // appsettings.json or environment variables
    /// {
    ///     "modules": {
    ///         "dotnetcloud.documents": {
    ///             "storage_path": "/data/documents",
    ///             "max_file_size": 104857600,
    ///             "enable_versioning": true
    ///         }
    ///     }
    /// }
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// <b>Accessing Configuration:</b>
    /// 
    /// <code>
    /// public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken ct)
    /// {
    ///     // Check if configuration exists
    ///     if (context.Configuration.TryGetValue("storage_path", out var pathObj))
    ///     {
    ///         _storagePath = (string)pathObj;
    ///     }
    ///     else
    ///     {
    ///         _storagePath = "/var/lib/dotnetcloud/default";  // Default
    ///     }
    ///     
    ///     // Type-safe configuration access
    ///     var maxSize = context.Configuration.ContainsKey("max_file_size")
    ///         ? Convert.ToInt64(context.Configuration["max_file_size"])
    ///         : 104857600;  // 100MB default
    /// }
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// <b>Configuration Best Practices:</b>
    /// 
    /// <list type="bullet">
    ///   <item><description>Provide sensible defaults if configuration not specified</description></item>
    ///   <item><description>Validate configuration during initialization</description></item>
    ///   <item><description>Log loaded configuration (without sensitive values)</description></item>
    ///   <item><description>Throw if required configuration is invalid</description></item>
    ///   <item><description>Support hot-reloading configuration if applicable</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public required IReadOnlyDictionary<string, object> Configuration { get; init; }

    /// <summary>
    /// Gets the caller context representing the system identity initializing the module.
    /// 
    /// Can be used for logging and permission checking during initialization.
    /// Always a System-type caller (CallerType.System).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Typical Usage:</b>
    /// 
    /// <code>
    /// public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken ct)
    /// {
    ///     // Log initialization with system caller
    ///     var logger = context.Services.GetService&lt;ILogger&lt;MyModule&gt;&gt;();
    ///     logger.LogInformation(
    ///         "Module initialization requested by {CallerType}",
    ///         context.SystemCaller.Type);
    ///     
    ///     // Use system caller for initialization operations
    ///     await _auditLog.LogAsync(
    ///         "Module initialized",
    ///         context.ModuleId,
    ///         context.SystemCaller);
    /// }
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// <b>Note:</b> This is always a System caller (no user identity).
    /// For operations requiring user context, perform them later during regular operation.
    /// </para>
    /// </remarks>
    public required CallerContext SystemCaller { get; init; }
}

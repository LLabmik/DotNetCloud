namespace DotNetCloud.Core.Modules;

using DotNetCloud.Core.Authorization;

/// <summary>
/// Provides the context and resources needed for a module to initialize.
/// Contains configuration, dependency injection container, and access to platform capabilities.
/// </summary>
public record ModuleInitializationContext
{
    /// <summary>
    /// Gets the unique identifier of the module being initialized.
    /// </summary>
    public required string ModuleId { get; init; }

    /// <summary>
    /// Gets the service provider for dependency injection and capability resolution.
    /// Use this to retrieve required capabilities (e.g., IUserDirectory, IEventBus).
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Gets the configuration dictionary for the module.
    /// Contains module-specific configuration values.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Configuration { get; init; }

    /// <summary>
    /// Gets the caller context representing the system identity initializing the module.
    /// Can be used for logging and permission checking during initialization.
    /// </summary>
    public required CallerContext SystemCaller { get; init; }
}

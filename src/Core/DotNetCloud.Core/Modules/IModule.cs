namespace DotNetCloud.Core.Modules;

/// <summary>
/// Defines the core interface for a module in the DotNetCloud system.
/// Modules are plugin-style components that extend the platform's functionality.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Gets the module's manifest containing metadata about capabilities, events, and requirements.
    /// </summary>
    IModuleManifest Manifest { get; }

    /// <summary>
    /// Initializes the module with its context and configuration.
    /// Called once when the module is first loaded into the system.
    /// This is where the module should validate its requirements and set up internal state.
    /// </summary>
    /// <param name="context">The initialization context containing configuration and available capabilities.</param>
    /// <param name="cancellationToken">A token to cancel the initialization.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the module, enabling it to process events and requests.
    /// Called after all modules have been initialized.
    /// This is where the module should begin accepting work.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the startup.</param>
    /// <returns>A task representing the asynchronous startup operation.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the module gracefully, allowing it to complete in-flight operations.
    /// Called during system shutdown or when the module is being unloaded.
    /// This is where the module should clean up resources and stop accepting new work.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the shutdown (though modules should attempt graceful shutdown).</param>
    /// <returns>A task representing the asynchronous shutdown operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}

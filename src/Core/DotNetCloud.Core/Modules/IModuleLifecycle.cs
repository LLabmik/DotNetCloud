namespace DotNetCloud.Core.Modules;

/// <summary>
/// Defines the full lifecycle interface for a module, including disposal.
/// Implements <see cref="IModule"/> and adds async disposal support for proper resource cleanup.
/// </summary>
public interface IModuleLifecycle : IModule, IAsyncDisposable
{
    /// <summary>
    /// Disposes the module and releases all held resources asynchronously.
    /// Called after <see cref="IModule.StopAsync"/> completes to allow final cleanup operations.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal operation.</returns>
    /// <remarks>
    /// This method is called via <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// Implementations should override this method to provide custom disposal logic,
    /// while <see cref="IAsyncDisposable.DisposeAsync"/> typically calls this method.
    /// </remarks>
    new Task DisposeAsync();
}

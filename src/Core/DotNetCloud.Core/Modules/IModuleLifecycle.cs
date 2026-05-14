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
    /// This method is declared with the <c>new</c> keyword to redefine <see cref="IAsyncDisposable.DisposeAsync"/>
    /// as a <see cref="Task"/>-returning method, enabling async disposal within the module lifecycle.
    /// Implementations should provide custom disposal logic in this method,
    /// while the <see cref="IAsyncDisposable.DisposeAsync"/> from <see cref="IAsyncDisposable"/> typically delegates to this method.
    /// Note: This is a redefinition pattern, not an override — the base <see cref="IAsyncDisposable.DisposeAsync"/> remains available.
    /// </remarks>
    new Task DisposeAsync();
}

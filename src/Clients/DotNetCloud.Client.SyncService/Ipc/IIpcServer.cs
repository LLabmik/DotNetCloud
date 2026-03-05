namespace DotNetCloud.Client.SyncService.Ipc;

/// <summary>
/// IPC server that accepts client connections over a Named Pipe (Windows)
/// or a Unix domain socket (Linux/macOS) and dispatches commands to
/// <see cref="ContextManager.ISyncContextManager"/>.
/// </summary>
public interface IIpcServer
{
    /// <summary>
    /// Starts the listener and begins accepting client connections in the background.
    /// Returns immediately; the accept loop runs until <see cref="StopAsync"/> is called.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the listener and disconnects all active client handlers.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

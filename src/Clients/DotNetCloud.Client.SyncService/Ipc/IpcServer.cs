using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using DotNetCloud.Client.SyncService.ContextManager;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncService.Ipc;

/// <summary>
/// IPC server that listens on a Named Pipe (Windows) or Unix domain socket (Linux/macOS).
/// Accepts multiple concurrent client connections, handing each off to an
/// <see cref="IpcClientHandler"/> running on the thread pool.
/// </summary>
public sealed class IpcServer : IIpcServer, IAsyncDisposable
{
    /// <summary>Named pipe name used on Windows (accessible as <c>\\.\pipe\dotnetcloud-sync</c>).</summary>
    public const string PipeName = "dotnetcloud-sync";

    /// <summary>Unix domain socket path used on Linux/macOS.</summary>
    public const string UnixSocketPath = "/run/dotnetcloud/sync.sock";

    private readonly ISyncContextManager _contextManager;
    private readonly ILogger<IpcServer> _logger;

    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Socket? _unixSocket;

    // Tracks in-flight client handler tasks for clean shutdown
    private readonly object _clientLock = new();
    private readonly List<Task> _clientTasks = [];

    /// <summary>Initializes a new <see cref="IpcServer"/>.</summary>
    public IpcServer(ISyncContextManager contextManager, ILogger<IpcServer> logger)
    {
        _contextManager = contextManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _listenTask = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ListenNamedPipeAsync(_cts.Token)
            : ListenUnixSocketAsync(_cts.Token);

        _logger.LogInformation("IPC server started ({Mode}).",
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Named Pipe" : "Unix Socket");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();

        // Dispose the Unix socket to unblock AcceptAsync
        _unixSocket?.Dispose();

        if (_listenTask is not null)
        {
            try { await _listenTask; }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IPC listen task failed during shutdown.");
            }
        }

        // Wait for all active client handlers to finish
        Task[] pending;
        lock (_clientLock)
        {
            pending = [.. _clientTasks];
        }

        if (pending.Length > 0)
        {
            await Task.WhenAll(
                pending.Select(t => t.ContinueWith(_ => { }, CancellationToken.None)));
        }

        _logger.LogInformation("IPC server stopped.");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _cts?.Dispose();
        _unixSocket?.Dispose();
    }

    // ── Named Pipe (Windows) ──────────────────────────────────────────────

    private async Task ListenNamedPipeAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Listening on named pipe: {PipeName}.", PipeName);

        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellationToken);
                _logger.LogDebug("Named pipe client connected.");

                AcceptClient(pipe);
            }
            catch (OperationCanceledException)
            {
                pipe?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                pipe?.Dispose();
                _logger.LogError(ex, "Error accepting named pipe connection.");
            }
        }
    }

    // ── Unix Socket (Linux/macOS) ─────────────────────────────────────────

    private async Task ListenUnixSocketAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Listening on Unix socket: {SocketPath}.", UnixSocketPath);

        var socketDir = Path.GetDirectoryName(UnixSocketPath);
        if (socketDir is not null)
            Directory.CreateDirectory(socketDir);

        // Remove stale socket file from a previous run
        if (File.Exists(UnixSocketPath))
            File.Delete(UnixSocketPath);

        _unixSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _unixSocket.Bind(new UnixDomainSocketEndPoint(UnixSocketPath));
        _unixSocket.Listen(backlog: 10);

        while (!cancellationToken.IsCancellationRequested)
        {
            Socket? clientSocket = null;
            try
            {
                clientSocket = await _unixSocket.AcceptAsync(cancellationToken);
                _logger.LogDebug("Unix socket client connected.");

                var stream = new NetworkStream(clientSocket, ownsSocket: true);
                AcceptClient(stream);
            }
            catch (OperationCanceledException)
            {
                clientSocket?.Dispose();
                break;
            }
            catch (ObjectDisposedException)
            {
                // _unixSocket was disposed in StopAsync
                break;
            }
            catch (Exception ex)
            {
                clientSocket?.Dispose();
                _logger.LogError(ex, "Error accepting Unix socket connection.");
            }
        }
    }

    // ── Client task management ────────────────────────────────────────────

    private void AcceptClient(Stream stream)
    {
        var handler = new IpcClientHandler(stream, _contextManager, _logger);
        var token = _cts?.Token ?? CancellationToken.None;
        var task = handler.HandleAsync(token);

        lock (_clientLock)
        {
            _clientTasks.Add(task);
        }

        _ = task.ContinueWith(_ =>
        {
            lock (_clientLock) { _clientTasks.Remove(task); }
        }, CancellationToken.None);
    }
}

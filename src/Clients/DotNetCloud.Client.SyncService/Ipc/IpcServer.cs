using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using DotNetCloud.Client.SyncService.ContextManager;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

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
                pipe = CreateNamedPipe();

                await pipe.WaitForConnectionAsync(cancellationToken);
                _logger.LogDebug("Named pipe client connected.");

                var callerIdentity = ResolveNamedPipeIdentity(pipe);
                AcceptClient(pipe, callerIdentity);
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

    /// <summary>
    /// Creates a <see cref="NamedPipeServerStream"/> with an ACL that allows
    /// interactive (non-elevated) users to connect when the service runs as SYSTEM.
    /// </summary>
    private static NamedPipeServerStream CreateNamedPipe()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }

        var security = new PipeSecurity();

        // Owner (SYSTEM or whichever account runs the service) gets full control.
        security.AddAccessRule(new PipeAccessRule(
            WindowsIdentity.GetCurrent().User!,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // Interactive users (the logged-in desktop user) get read/write so the
        // tray app can connect.
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity: security);
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
                var callerIdentity = IpcCallerIdentity.Unavailable;
                AcceptClient(stream, callerIdentity);
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

    private static IpcCallerIdentity ResolveNamedPipeIdentity(NamedPipeServerStream pipe)
    {
        string? userName = null;
        SafeAccessTokenHandle? accessToken = null;

        try
        {
            userName = pipe.GetImpersonationUserName();
        }
        catch
        {
            return IpcCallerIdentity.Unavailable;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            accessToken = TryCaptureClientAccessToken(pipe);

        return IpcCallerIdentity.FromWindowsPipeUserName(userName, accessToken);
    }

    [SupportedOSPlatform("windows")]
    private static SafeAccessTokenHandle? TryCaptureClientAccessToken(NamedPipeServerStream pipe)
    {
        try
        {
            SafeAccessTokenHandle? duplicated = null;

            pipe.RunAsClient(() =>
            {
                using var currentIdentity =
                    WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.Duplicate);
                duplicated = DuplicateAccessToken(currentIdentity.AccessToken);
            });

            return duplicated;
        }
        catch
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static SafeAccessTokenHandle? DuplicateAccessToken(SafeAccessTokenHandle sourceToken)
    {
        var duplicatedHandle = IntPtr.Zero;
        var duplicated = DuplicateTokenEx(
            sourceToken.DangerousGetHandle(),
            TOKEN_QUERY | TOKEN_IMPERSONATE | TOKEN_DUPLICATE,
            IntPtr.Zero,
            SecurityImpersonation,
            TokenImpersonation,
            ref duplicatedHandle);

        if (!duplicated || duplicatedHandle == IntPtr.Zero)
            return null;

        return new SafeAccessTokenHandle(duplicatedHandle);
    }

    private void AcceptClient(Stream stream, IpcCallerIdentity callerIdentity)
    {
        var handler = new IpcClientHandler(stream, _contextManager, _logger, callerIdentity);
        var token = _cts?.Token ?? CancellationToken.None;
        var task = handler.HandleAsync(token);

        lock (_clientLock)
        {
            _clientTasks.Add(task);
        }

        _ = task.ContinueWith(_ =>
        {
            lock (_clientLock) { _clientTasks.Remove(task); }

            callerIdentity.WindowsAccessToken?.Dispose();
        }, CancellationToken.None);
    }

    private const uint TOKEN_QUERY = 0x0008;
    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_IMPERSONATE = 0x0004;
    private const int SecurityImpersonation = 2;
    private const int TokenImpersonation = 2;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        uint dwDesiredAccess,
        IntPtr lpTokenAttributes,
        int impersonationLevel,
        int tokenType,
        ref IntPtr phNewToken);
}

using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
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
                var callerIdentity = ResolveUnixSocketIdentity(clientSocket);
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
            // GetImpersonationUserName() can fail for MSIX AppContainer clients.
            // Fall through to alternative approaches below.
        }

        if (OperatingSystem.IsWindows())
        {
            // If GetImpersonationUserName failed, resolve identity via RunAsClient.
            if (userName is null)
                userName = TryResolveIdentityViaRunAsClient(pipe);

            // If RunAsClient also failed (MSIX AppContainer), resolve via client PID.
            if (userName is null)
                userName = TryResolveIdentityViaClientProcessId(pipe);

            accessToken = TryCaptureClientAccessToken(pipe);
        }

        // Last resort: when running inside an MSIX package, all pipe identity
        // APIs fail due to AppContainer restrictions. Return a special identity
        // that bypasses ownership checks — the MSIX sandbox already isolates access.
        if (string.IsNullOrWhiteSpace(userName))
            return IsRunningAsPackagedApp() ? IpcCallerIdentity.MsixPackagedCaller : IpcCallerIdentity.Unavailable;

        return IpcCallerIdentity.FromWindowsPipeUserName(userName, accessToken);
    }

    [SupportedOSPlatform("windows")]
    private static string? TryResolveIdentityViaRunAsClient(NamedPipeServerStream pipe)
    {
        try
        {
            string? name = null;
            pipe.RunAsClient(() =>
            {
                using var identity = WindowsIdentity.GetCurrent(
                    TokenAccessLevels.Query | TokenAccessLevels.Duplicate);
                name = identity.Name;
            });
            return name;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Last-resort identity resolution for MSIX AppContainer clients where pipe
    /// impersonation is unavailable. Gets the client PID from the pipe handle,
    /// opens the process token, and reads the owner identity.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static string? TryResolveIdentityViaClientProcessId(NamedPipeServerStream pipe)
    {
        try
        {
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var clientPid) || clientPid == 0)
                return null;

            var processHandle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, clientPid);
            if (processHandle == IntPtr.Zero)
                return null;

            try
            {
                if (!OpenProcessToken(processHandle, TOKEN_QUERY, out var tokenHandle))
                    return null;

                try
                {
                    using var identity = new WindowsIdentity(tokenHandle);
                    return identity.Name;
                }
                finally
                {
                    CloseHandle(tokenHandle);
                }
            }
            finally
            {
                CloseHandle(processHandle);
            }
        }
        catch
        {
            return null;
        }
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

    private IpcCallerIdentity ResolveUnixSocketIdentity(Socket clientSocket)
    {
        if (!OperatingSystem.IsLinux())
            return IpcCallerIdentity.Unavailable;

        if (!TryGetLinuxPeerCredentials(clientSocket, out var peerCredentials))
            return IpcCallerIdentity.Unavailable;

        var userName = TryGetLinuxUserName(peerCredentials.Uid);
        return IpcCallerIdentity.FromUnixPeerCredentials(peerCredentials.Uid, peerCredentials.Gid, userName);
    }

    private static bool TryGetLinuxPeerCredentials(Socket socket, out LinuxUCred credentials)
    {
        credentials = default;

        var optionLength = (uint)Marshal.SizeOf<LinuxUCred>();
        var handle = socket.SafeHandle.DangerousGetHandle();
        var result = getsockopt(
            handle.ToInt32(),
            LinuxSolSocket,
            LinuxSoPeerCred,
            out credentials,
            ref optionLength);

        return result == 0;
    }

    private static string? TryGetLinuxUserName(uint uid)
    {
        const int BufferLength = 4096;
        var buffer = new byte[BufferLength];
        var getPwResult = getpwuid_r(uid, out var passwd, buffer, (nuint)buffer.Length, out var passwdResult);

        if (getPwResult != 0 || passwdResult == IntPtr.Zero || passwd.pw_name == IntPtr.Zero)
            return null;

        return Marshal.PtrToStringAnsi(passwd.pw_name);
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

    /// <summary>
    /// Detects whether the current process is running inside an MSIX package
    /// by checking for the Windows.ApplicationModel.Package API.
    /// </summary>
    private static bool IsRunningAsPackagedApp()
    {
        try
        {
            var packagePath = Environment.GetEnvironmentVariable("PACKAGE_FAMILY_NAME");
            if (!string.IsNullOrEmpty(packagePath))
                return true;

            // Check if running from a WindowsApps directory
            var location = AppContext.BaseDirectory;
            return location.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private const uint TOKEN_QUERY = 0x0008;
    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_IMPERSONATE = 0x0004;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const int SecurityImpersonation = 2;
    private const int TokenImpersonation = 2;
    private const int LinuxSolSocket = 1;
    private const int LinuxSoPeerCred = 17;

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxUCred
    {
        public int Pid;
        public uint Uid;
        public uint Gid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxPasswd
    {
        public IntPtr pw_name;
        public IntPtr pw_passwd;
        public uint pw_uid;
        public uint pw_gid;
        public IntPtr pw_gecos;
        public IntPtr pw_dir;
        public IntPtr pw_shell;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        uint dwDesiredAccess,
        IntPtr lpTokenAttributes,
        int impersonationLevel,
        int tokenType,
        ref IntPtr phNewToken);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeClientProcessId(
        SafePipeHandle Pipe,
        out uint ClientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        uint dwProcessId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(
        IntPtr ProcessHandle,
        uint DesiredAccess,
        out IntPtr TokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("libc", SetLastError = true)]
    private static extern int getsockopt(
        int socket,
        int level,
        int optionName,
        out LinuxUCred optionValue,
        ref uint optionLength);

    [DllImport("libc", SetLastError = true)]
    private static extern int getpwuid_r(
        uint uid,
        out LinuxPasswd pwd,
        byte[] buffer,
        nuint bufferLength,
        out IntPtr result);
}

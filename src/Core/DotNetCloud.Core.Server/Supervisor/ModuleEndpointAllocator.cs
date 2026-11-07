using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Sandbox;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Mints the gRPC endpoint for a module: a per-module Unix socket, a per-module named pipe, or a
/// loopback TCP port from the configured range.
/// </summary>
/// <remarks>
/// <para>
/// This is the single endpoint authority. <c>ProcessSupervisor</c> (which launches module hosts)
/// and <c>ModuleEndpointProvider</c> (which resolves a channel before a module is running) both
/// use it, so their answers cannot drift — the previous duplicated TCP allocator in
/// <c>ModuleEndpointProvider</c> had no collision probing and could disagree with the supervisor.
/// </para>
/// <para>
/// Socket endpoints are deterministic per module, which is what makes it safe to cache a gRPC
/// channel or REST invoker per module id.
/// </para>
/// </remarks>
public sealed class ModuleEndpointAllocator
{
    /// <summary>
    /// The maximum accepted Unix socket path length.
    /// </summary>
    /// <remarks>
    /// <c>sockaddr_un.sun_path</c> is 108 bytes on Linux including the terminating NUL. Leave
    /// headroom for a shorter root so the path does not change when a directory is renamed.
    /// </remarks>
    internal const int MaxUnixSocketPathLength = 100;

    /// <summary>The socket file name used inside a module's <c>run</c> directory.</summary>
    internal const string SocketFileName = "module.sock";

    private readonly ModuleTransportKind _kind;
    private readonly string? _unixSocketDirectory;
    private readonly string _namedPipePrefix;
    private readonly ProcessSupervisorOptions _options;
    private readonly string _dataRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleEndpointAllocator"/> class.
    /// </summary>
    /// <param name="kind">The transport to mint.</param>
    /// <param name="unixSocketDirectory">Optional shared Unix socket directory override.</param>
    /// <param name="namedPipePrefix">The Windows named pipe prefix.</param>
    /// <param name="options">The supervisor options (TCP port range).</param>
    /// <param name="dataRoot">The server data directory that holds <c>modules/</c>.</param>
    public ModuleEndpointAllocator(
        ModuleTransportKind kind,
        string? unixSocketDirectory,
        string namedPipePrefix,
        ProcessSupervisorOptions options,
        string dataRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);

        _kind = kind;
        _unixSocketDirectory = string.IsNullOrWhiteSpace(unixSocketDirectory) ? null : unixSocketDirectory;
        _namedPipePrefix = string.IsNullOrWhiteSpace(namedPipePrefix) ? "dotnetcloud" : namedPipePrefix;
        _options = options;
        _dataRoot = dataRoot;
    }

    /// <summary>
    /// Creates an allocator from the resolved transport selection.
    /// </summary>
    /// <param name="selection">The resolved transport.</param>
    /// <param name="options">The supervisor options.</param>
    /// <param name="dataRoot">The server data directory.</param>
    /// <returns>The allocator.</returns>
    internal static ModuleEndpointAllocator FromSelection(
        ModuleTransportSelection selection,
        ProcessSupervisorOptions options,
        string dataRoot)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return new ModuleEndpointAllocator(
            selection.Kind, selection.UnixSocketDirectory, selection.NamedPipePrefix, options, dataRoot);
    }

    /// <summary>Gets the transport kind this allocator mints.</summary>
    public ModuleTransportKind Kind => _kind;

    /// <summary>
    /// Resolves the endpoint for a module.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="allocatedEndpoints">
    /// Endpoints already handed to other modules; only consulted for TCP port probing.
    /// </param>
    /// <returns>The endpoint. Never a socket endpoint whose path cannot be represented.</returns>
    public ModuleEndpoint Resolve(string moduleId, IEnumerable<string>? allocatedEndpoints = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        switch (_kind)
        {
            case ModuleTransportKind.UnixDomainSocket:
                var socketPath = GetUnixSocketPath(moduleId);
                if (socketPath is not null && socketPath.Length <= MaxUnixSocketPathLength)
                {
                    return ModuleEndpoint.UnixDomainSocket(socketPath);
                }

                // A too-long socket path is unbindable, so fall back rather than crash the module.
                return ModuleEndpoint.Tcp(AllocateTcpPort(moduleId, allocatedEndpoints));

            case ModuleTransportKind.NamedPipe:
                return ModuleEndpoint.NamedPipe(GetNamedPipeName(moduleId));

            default:
                return ModuleEndpoint.Tcp(AllocateTcpPort(moduleId, allocatedEndpoints));
        }
    }

    /// <summary>
    /// Gets the Unix socket path for a module, or <see langword="null"/> when the transport is not
    /// socket based.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The absolute socket path.</returns>
    public string? GetUnixSocketPath(string moduleId)
    {
        if (_kind != ModuleTransportKind.UnixDomainSocket)
        {
            return null;
        }

        var directory = _unixSocketDirectory;
        if (!string.IsNullOrWhiteSpace(directory))
        {
            // A shared override directory holds every module's socket, so the file name must be
            // unique per module. The default per-module run directory needs only the fixed name.
            return Path.Combine(directory, moduleId + ".sock");
        }

        return Path.Combine(GetRunDirectory(moduleId), SocketFileName);
    }

    /// <summary>
    /// Gets the named pipe name for a module.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The pipe name, without the <c>\\.\pipe\</c> prefix.</returns>
    public string GetNamedPipeName(string moduleId) => $"{_namedPipePrefix}-{moduleId}";

    /// <summary>
    /// Gets the module's private runtime directory: <c>&lt;dataRoot&gt;/modules/&lt;moduleId&gt;/run</c>.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The absolute runtime directory path.</returns>
    public string GetRunDirectory(string moduleId) =>
        ModuleSandboxLayout.Create(_dataRoot, moduleId).RunDirectory;

    private int AllocateTcpPort(string moduleId, IEnumerable<string>? allocatedEndpoints)
    {
        // Reserve TcpPortRangeStart for the core gRPC server. Module ports use the module id hash
        // to pick a semi-stable offset, then probe forward to skip collisions.
        var range = Math.Max(1, _options.TcpPortRangeEnd - _options.TcpPortRangeStart - 1);
        var basePort = _options.TcpPortRangeStart + 1 + (Math.Abs(StringComparer.Ordinal.GetHashCode(moduleId)) % range);
        var port = basePort;

        var allocated = new HashSet<int>();
        if (allocatedEndpoints is not null)
        {
            foreach (var endpoint in allocatedEndpoints)
            {
                if (ModuleEndpoint.TryParse(endpoint, out var parsed)
                    && parsed.Transport == ModuleTransportKind.Tcp
                    && parsed.Address.LastIndexOf(':') is var colon and > 0
                    && int.TryParse(parsed.Address.AsSpan(colon + 1), out var existing))
                {
                    allocated.Add(existing);
                }
            }
        }

        var end = _options.TcpPortRangeEnd;
        while (port <= end && allocated.Contains(port))
        {
            port++;
        }

        if (port > end)
        {
            port = _options.TcpPortRangeStart + 1;
            while (port < basePort && allocated.Contains(port))
            {
                port++;
            }
        }

        return port;
    }
}

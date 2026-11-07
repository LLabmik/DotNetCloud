namespace DotNetCloud.Core.Modules.Supervisor;

/// <summary>
/// Selects the transport used between the core and module processes.
/// </summary>
public enum ModuleTransportMode
{
    /// <summary>
    /// Pick the best transport for the current platform: a Unix domain socket on Unix-like
    /// systems and a Windows named pipe on Windows. Falls back to loopback TCP when the chosen
    /// socket path cannot be represented (see <c>Modules:UnixSocketDirectory</c>).
    /// </summary>
    Auto = 0,

    /// <summary>Always use a Unix domain socket (Unix) or a Windows named pipe (Windows).</summary>
    Sockets = 1,

    /// <summary>
    /// Always use loopback TCP inside the configured <c>TcpPortRange</c>. Required when module
    /// processes do not share a filesystem with the core (for example separate containers) and
    /// documented as the escape hatch for constrained hosts. Credential authentication is
    /// enforced on every transport, so TCP widens reachability but not authorization.
    /// </summary>
    Tcp = 2,
}

/// <summary>
/// Configuration for the core↔module transport.
/// </summary>
/// <remarks>
/// <para>
/// Bound from the <c>Modules</c> configuration section (for example
/// <c>Modules:Transport=Sockets</c> or the environment variable <c>Modules__Transport=Tcp</c>).
/// </para>
/// <para>
/// This is the only place the transport is decided. It replaces the two hard-coded
/// <c>ProcessSupervisorOptions.PreferTcpTransport = true</c> assignments that made sockets
/// impossible to enable.
/// </para>
/// </remarks>
public sealed class ModuleTransportOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Modules";

    /// <summary>
    /// Gets or sets the transport mode. Default: <see cref="ModuleTransportMode.Auto"/>.
    /// </summary>
    public ModuleTransportMode Transport { get; set; } = ModuleTransportMode.Auto;

    /// <summary>
    /// Gets or sets an explicit directory for module Unix sockets. When unset, each socket lives in
    /// the module's own runtime directory: <c>&lt;dataDir&gt;/modules/&lt;moduleId&gt;/run/module.sock</c>.
    /// </summary>
    /// <remarks>
    /// A <c>sockaddr_un</c> path is limited to ~107 bytes on Linux. Set this to a short path (for
    /// example <c>/run/dotnetcloud</c>) on installations whose data directory is deeply nested;
    /// otherwise that module falls back to loopback TCP with a warning.
    /// </remarks>
    public string? UnixSocketDirectory { get; set; }

    /// <summary>
    /// Gets or sets the Windows named pipe prefix. Default: <c>dotnetcloud</c>, producing pipes
    /// such as <c>\\.\pipe\dotnetcloud-dotnetcloud.files</c>.
    /// </summary>
    public string NamedPipePrefix { get; set; } = "dotnetcloud";
}

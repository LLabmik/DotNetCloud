using DotNetCloud.Core.Modules.Supervisor;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// The resolved module transport for this process.
/// </summary>
/// <remarks>
/// <para>
/// Resolution happens exactly once, at registration time, so the supervisor, the gRPC channel
/// manager, the REST proxy and the endpoint provider can never disagree about which transport is
/// in use. Previously two call sites independently overwrote
/// <see cref="ProcessSupervisorOptions.PreferTcpTransport"/> to <see langword="true"/> and the
/// value was never read, so sockets could not be enabled at all.
/// </para>
/// <para>
/// Precedence: <see cref="ModuleTransportOptions.Transport"/> wins when it is not
/// <see cref="ModuleTransportMode.Auto"/>; otherwise the legacy
/// <see cref="ProcessSupervisorOptions.PreferTcpTransport"/> flag can force TCP; otherwise the
/// platform default (Unix domain socket / Windows named pipe) applies.
/// </para>
/// </remarks>
internal sealed class ModuleTransportSelection
{
    private ModuleTransportSelection(ModuleTransportKind kind, string? unixSocketDirectory, string namedPipePrefix)
    {
        Kind = kind;
        UnixSocketDirectory = unixSocketDirectory;
        NamedPipePrefix = namedPipePrefix;
    }

    /// <summary>Gets the transport kind in use.</summary>
    public ModuleTransportKind Kind { get; }

    /// <summary>Gets the explicit Unix socket directory override, when configured.</summary>
    public string? UnixSocketDirectory { get; }

    /// <summary>Gets the Windows named pipe prefix.</summary>
    public string NamedPipePrefix { get; }

    /// <summary>Gets a value indicating whether endpoints are socket/pipe based.</summary>
    public bool IsSocketTransport => Kind is ModuleTransportKind.UnixDomainSocket or ModuleTransportKind.NamedPipe;

    /// <summary>
    /// Resolves the transport for a platform.
    /// </summary>
    /// <param name="transport">The module transport options.</param>
    /// <param name="supervisor">The supervisor options (legacy TCP preference).</param>
    /// <param name="isWindows">Whether the current platform is Windows.</param>
    /// <returns>The resolved selection.</returns>
    public static ModuleTransportSelection Resolve(
        ModuleTransportOptions transport,
        ProcessSupervisorOptions supervisor,
        bool isWindows)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(supervisor);

        var kind = transport.Transport switch
        {
            ModuleTransportMode.Tcp => ModuleTransportKind.Tcp,
            ModuleTransportMode.Sockets => isWindows ? ModuleTransportKind.NamedPipe : ModuleTransportKind.UnixDomainSocket,
            _ => supervisor.PreferTcpTransport
                ? ModuleTransportKind.Tcp
                : isWindows ? ModuleTransportKind.NamedPipe : ModuleTransportKind.UnixDomainSocket,
        };

        return new ModuleTransportSelection(
            kind,
            string.IsNullOrWhiteSpace(transport.UnixSocketDirectory) ? null : transport.UnixSocketDirectory,
            string.IsNullOrWhiteSpace(transport.NamedPipePrefix) ? "dotnetcloud" : transport.NamedPipePrefix);
    }

    /// <summary>
    /// Resolves the transport for the current platform.
    /// </summary>
    /// <param name="transport">The module transport options.</param>
    /// <param name="supervisor">The supervisor options.</param>
    /// <returns>The resolved selection.</returns>
    public static ModuleTransportSelection Resolve(ModuleTransportOptions transport, ProcessSupervisorOptions supervisor) =>
        Resolve(transport, supervisor, OperatingSystem.IsWindows());

    /// <inheritdoc />
    public override string ToString() => Kind.ToString();
}

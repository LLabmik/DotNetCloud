using System.Diagnostics.CodeAnalysis;

namespace DotNetCloud.Core.Modules.Supervisor;

/// <summary>
/// The transport family used for a core↔module connection.
/// </summary>
public enum ModuleTransportKind
{
    /// <summary>Cleartext HTTP/2 over a loopback TCP socket (fallback transport).</summary>
    Tcp = 0,

    /// <summary>Cleartext HTTP/2 over a Unix domain socket (Linux/macOS).</summary>
    UnixDomainSocket = 1,

    /// <summary>Cleartext HTTP/2 over a Windows named pipe.</summary>
    NamedPipe = 2,
}

/// <summary>
/// A parsed module gRPC endpoint: a transport plus the address that transport understands.
/// </summary>
/// <remarks>
/// <para>
/// The endpoint string form is what the supervisor passes to a module host in
/// <c>DOTNETCLOUD_GRPC_ENDPOINT</c> and what the core stores in <see cref="ModuleProcessInfo.GrpcEndpoint"/>:
/// </para>
/// <list type="bullet">
/// <item><description><c>http://localhost:50105</c> — loopback TCP.</description></item>
/// <item><description><c>unix:///data/modules/dotnetcloud.files/run/module.sock</c> — Unix domain socket.</description></item>
/// <item><description><c>net.pipe://dotnetcloud-dotnetcloud.files</c> — Windows named pipe.</description></item>
/// </list>
/// <para>
/// Historically every host did <c>endpoint.Replace("unix://", "http://")</c> and then bound
/// <c>uri.Port</c>, which silently turned a socket endpoint into loopback port 80. Parsing through
/// this type — and binding through the shared host helper — removes that failure mode.
/// </para>
/// </remarks>
public sealed record ModuleEndpoint
{
    /// <summary>The scheme prefix for Unix domain socket endpoints.</summary>
    public const string UnixSocketScheme = "unix://";

    /// <summary>The scheme prefix for Windows named pipe endpoints.</summary>
    public const string NamedPipeScheme = "net.pipe://";

    /// <summary>An alternative named pipe scheme accepted on parse.</summary>
    public const string LegacyNamedPipeScheme = "pipe://";

    /// <summary>
    /// Gets the transport family of this endpoint.
    /// </summary>
    public required ModuleTransportKind Transport { get; init; }

    /// <summary>
    /// Gets the transport-specific address.
    /// </summary>
    /// <remarks>
    /// Absolute socket file path for <see cref="ModuleTransportKind.UnixDomainSocket"/>, the pipe
    /// name for <see cref="ModuleTransportKind.NamedPipe"/>, and <c>host:port</c> for
    /// <see cref="ModuleTransportKind.Tcp"/>.
    /// </remarks>
    public required string Address { get; init; }

    /// <summary>
    /// Gets a value indicating whether this endpoint is bound to a socket/pipe rather than a TCP port.
    /// </summary>
    public bool IsSocketTransport => Transport is ModuleTransportKind.UnixDomainSocket or ModuleTransportKind.NamedPipe;

    /// <summary>
    /// Creates a loopback TCP endpoint for a port.
    /// </summary>
    /// <param name="port">The loopback port.</param>
    /// <param name="host">The host name (defaults to <c>localhost</c>).</param>
    /// <returns>The endpoint.</returns>
    public static ModuleEndpoint Tcp(int port, string host = "localhost")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        return new ModuleEndpoint
        {
            Transport = ModuleTransportKind.Tcp,
            Address = $"{host}:{port}",
        };
    }

    /// <summary>
    /// Creates a Unix domain socket endpoint for an absolute socket path.
    /// </summary>
    /// <param name="socketPath">The absolute socket file path.</param>
    /// <returns>The endpoint.</returns>
    public static ModuleEndpoint UnixDomainSocket(string socketPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);

        return new ModuleEndpoint
        {
            Transport = ModuleTransportKind.UnixDomainSocket,
            Address = socketPath,
        };
    }

    /// <summary>
    /// Creates a Windows named pipe endpoint for a pipe name.
    /// </summary>
    /// <param name="pipeName">The pipe name (without the <c>\\.\pipe\</c> prefix).</param>
    /// <returns>The endpoint.</returns>
    public static ModuleEndpoint NamedPipe(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        return new ModuleEndpoint
        {
            Transport = ModuleTransportKind.NamedPipe,
            Address = pipeName,
        };
    }

    /// <summary>
    /// Formats the endpoint back into its canonical string form.
    /// </summary>
    /// <returns>The endpoint string.</returns>
    public string ToEndpointString() => Transport switch
    {
        ModuleTransportKind.UnixDomainSocket when !Address.StartsWith(UnixSocketScheme, StringComparison.OrdinalIgnoreCase)
            => UnixSocketScheme + Address,
        ModuleTransportKind.UnixDomainSocket => Address,
        ModuleTransportKind.NamedPipe when Address.StartsWith(NamedPipeScheme, StringComparison.OrdinalIgnoreCase)
            || Address.StartsWith(LegacyNamedPipeScheme, StringComparison.OrdinalIgnoreCase) => Address,
        ModuleTransportKind.NamedPipe => NamedPipeScheme + Address,
        _ when Address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || Address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) => Address,
        _ => "http://" + Address,
    };

    /// <summary>
    /// Formats the endpoint for logs and diagnostics.
    /// </summary>
    /// <returns>The endpoint string.</returns>
    public override string ToString() => ToEndpointString();

    /// <summary>
    /// The <c>http://</c>-style base address a client should use for this endpoint.
    /// </summary>
    /// <remarks>
    /// HTTP clients cannot dial <c>unix://</c> or <c>net.pipe://</c> URLs, so socket endpoints
    /// advertise <c>http://localhost</c> and the connection is redirected to the socket by a
    /// <c>SocketsHttpHandler.ConnectCallback</c>. TCP endpoints keep their real authority.
    /// </remarks>
    public string ToClientBaseAddress() =>
        Transport == ModuleTransportKind.Tcp ? ToEndpointString() : "http://localhost";

    /// <summary>
    /// Parses an endpoint string.
    /// </summary>
    /// <param name="value">The endpoint string.</param>
    /// <param name="endpoint">The parsed endpoint when successful.</param>
    /// <returns><see langword="true"/> when the value is a recognised endpoint.</returns>
    public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out ModuleEndpoint? endpoint)
    {
        endpoint = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();

        if (text.StartsWith(UnixSocketScheme, StringComparison.OrdinalIgnoreCase))
        {
            var path = text[UnixSocketScheme.Length..];
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            endpoint = ModuleEndpoint.UnixDomainSocket(path);
            return true;
        }

        // Tolerate the single-slash form (unix:/path) and the absolute form (unix:///path).
        if (text.StartsWith("unix:", StringComparison.OrdinalIgnoreCase))
        {
            var path = text["unix:".Length..];
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            path = path.TrimStart('/');
            if (path.Length == 0)
            {
                return false;
            }

            if (!Path.IsPathRooted(path))
            {
                path = "/" + path;
            }

            endpoint = ModuleEndpoint.UnixDomainSocket(path);
            return true;
        }

        foreach (var scheme in new[] { NamedPipeScheme, LegacyNamedPipeScheme })
        {
            if (text.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                var pipeName = text[scheme.Length..].TrimStart('/');
                if (string.IsNullOrWhiteSpace(pipeName))
                {
                    return false;
                }

                endpoint = ModuleEndpoint.NamedPipe(pipeName);
                return true;
            }
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            endpoint = ModuleEndpoint.Tcp(uri.Port, uri.Host);
            return true;
        }

        // Bare "host:port" form.
        var colon = text.LastIndexOf(':');
        if (colon > 0
            && int.TryParse(text.AsSpan(colon + 1), out var port)
            && port is > 0 and <= 65535)
        {
            endpoint = ModuleEndpoint.Tcp(port, text[..colon]);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses an endpoint string, throwing when it is not recognised.
    /// </summary>
    /// <param name="value">The endpoint string.</param>
    /// <returns>The parsed endpoint.</returns>
    /// <exception cref="FormatException">The value is not a recognised endpoint.</exception>
    public static ModuleEndpoint Parse(string value) =>
        TryParse(value, out var endpoint)
            ? endpoint
            : throw new FormatException($"'{value}' is not a recognised module gRPC endpoint.");
}

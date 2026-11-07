using DotNetCloud.Core.Modules.Supervisor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Binds a module host to the endpoint the process supervisor assigned.
/// </summary>
/// <remarks>
/// <para>
/// Every module host used to inline the same eight lines:
/// <c>new Uri(endpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://"))</c> and then
/// bind <c>uri.Port</c> on loopback. For a socket endpoint that replacement yields a host-less URI
/// whose port is 80, so the socket path was silently discarded and every host would have raced for
/// <c>127.0.0.1:80</c>. This helper parses through <see cref="ModuleEndpoint"/> and binds the real
/// transport: <c>ListenUnixSocket</c>, <c>ListenNamedPipe</c>, or loopback TCP.
/// </para>
/// <para>
/// HTTP/2 cleartext (h2c) is used on every transport. <c>ModulePeerAuthenticationInterceptor</c>
/// stays registered in each host unchanged — it is transport-agnostic and fails closed when the host
/// was not launched by the supervisor.
/// </para>
/// </remarks>
public static class ModuleGrpcEndpointBinding
{
    /// <summary>The environment variable carrying the module's own gRPC endpoint.</summary>
    public const string GrpcEndpointVariable = "DOTNETCLOUD_GRPC_ENDPOINT";

    /// <summary>
    /// Configures Kestrel on <paramref name="builder"/> to listen on
    /// <see cref="GrpcEndpointVariable"/>.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The endpoint that was bound, or <see langword="null"/> when the variable is unset.</returns>
    /// <remarks>
    /// An unset variable means the host was started outside the supervisor (a developer running it
    /// directly); it then keeps the default Kestrel URLs so it is still reachable for debugging.
    /// </remarks>
    public static ModuleEndpoint? ConfigureModuleGrpcEndpoint(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var raw = Environment.GetEnvironmentVariable(GrpcEndpointVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!ModuleEndpoint.TryParse(raw, out var endpoint))
        {
            Console.Error.WriteLine(
                $"ModuleGrpcEndpointBinding: '{raw}' is not a recognised endpoint; falling back to the default Kestrel URLs.");
            return null;
        }

        builder.WebHost.ConfigureKestrel(kestrel => Bind(kestrel, endpoint));
        return endpoint;
    }

    /// <summary>
    /// Binds a Kestrel server to a parsed module endpoint over HTTP/2.
    /// </summary>
    /// <param name="kestrel">The Kestrel options.</param>
    /// <param name="endpoint">The module endpoint.</param>
    public static void Bind(KestrelServerOptions kestrel, ModuleEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(kestrel);
        ArgumentNullException.ThrowIfNull(endpoint);

        switch (endpoint.Transport)
        {
            case ModuleTransportKind.UnixDomainSocket:
                kestrel.ListenUnixSocket(endpoint.Address, listen => listen.Protocols = HttpProtocols.Http2);
                break;

            case ModuleTransportKind.NamedPipe:
                kestrel.ListenNamedPipe(endpoint.Address, listen => listen.Protocols = HttpProtocols.Http2);
                break;

            default:
                var port = new Uri(endpoint.ToEndpointString()).Port;
                kestrel.Listen(System.Net.IPAddress.Loopback, port, listen => listen.Protocols = HttpProtocols.Http2);
                break;
        }
    }
}

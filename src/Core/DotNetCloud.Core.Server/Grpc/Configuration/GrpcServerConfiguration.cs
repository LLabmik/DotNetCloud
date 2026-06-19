using DotNetCloud.Core.Modules.Supervisor;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DotNetCloud.Core.Server.Grpc.Configuration;

/// <summary>
/// Configures gRPC server transport — always cleartext HTTP/2 over TCP.
/// </summary>
internal static class GrpcServerConfiguration
{
    /// <summary>
    /// Configures the core gRPC server endpoint that modules connect to for capability access.
    /// </summary>
    public static void ConfigureCoreGrpcEndpoint(WebApplicationBuilder builder, ProcessSupervisorOptions options)
    {
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.ListenLocalhost(options.TcpPortRangeStart, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });
    }
}

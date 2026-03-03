using System.Runtime.InteropServices;
using DotNetCloud.Core.Modules.Supervisor;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DotNetCloud.Core.Server.Grpc.Configuration;

/// <summary>
/// Configures gRPC server transport based on the operating system and environment.
/// Supports Unix domain sockets (Linux), Named Pipes (Windows), and TCP fallback.
/// </summary>
internal static class GrpcServerConfiguration
{
    /// <summary>
    /// Configures the core gRPC server endpoint that modules connect to for capability access.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="options">Process supervisor options containing transport configuration.</param>
    public static void ConfigureCoreGrpcEndpoint(WebApplicationBuilder builder, ProcessSupervisorOptions options)
    {
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            if (options.PreferTcpTransport)
            {
                ConfigureTcpEndpoint(kestrel, options.TcpPortRangeStart);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ConfigureUnixSocketEndpoint(kestrel, options.UnixSocketDirectory, "core");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ConfigureNamedPipeEndpoint(kestrel, options.NamedPipePrefix, "core");
            }
            else
            {
                ConfigureTcpEndpoint(kestrel, options.TcpPortRangeStart);
            }
        });
    }

    /// <summary>
    /// Gets the gRPC endpoint address for a module process based on the current platform.
    /// </summary>
    /// <param name="options">Process supervisor options.</param>
    /// <param name="moduleId">The module identifier (e.g., "dotnetcloud.files").</param>
    /// <param name="tcpPort">The TCP port for fallback transport.</param>
    /// <returns>The gRPC endpoint address string.</returns>
    public static string GetModuleEndpoint(ProcessSupervisorOptions options, string moduleId, int tcpPort = 0)
    {
        var safeName = SanitizeModuleName(moduleId);

        if (options.PreferTcpTransport)
        {
            return $"http://localhost:{tcpPort}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $"unix://{GetUnixSocketPath(options.UnixSocketDirectory, safeName)}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"net.pipe://localhost/{options.NamedPipePrefix}-{safeName}";
        }

        return $"http://localhost:{tcpPort}";
    }

    /// <summary>
    /// Gets the Unix socket path for a given module.
    /// </summary>
    internal static string GetUnixSocketPath(string baseDirectory, string moduleName)
    {
        return Path.Combine(baseDirectory, $"{moduleName}.sock");
    }

    /// <summary>
    /// Gets the Named Pipe name for a given module.
    /// </summary>
    internal static string GetNamedPipeName(string prefix, string moduleName)
    {
        return $"{prefix}-{moduleName}";
    }

    /// <summary>
    /// Sanitizes a module ID for use in socket/pipe names.
    /// Replaces dots with hyphens (e.g., "dotnetcloud.files" → "dotnetcloud-files").
    /// </summary>
    internal static string SanitizeModuleName(string moduleId)
    {
        return moduleId.Replace('.', '-');
    }

    /// <summary>
    /// Ensures the Unix socket directory exists with appropriate permissions.
    /// </summary>
    internal static void EnsureUnixSocketDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void ConfigureUnixSocketEndpoint(KestrelServerOptions kestrel, string socketDir, string name)
    {
        EnsureUnixSocketDirectory(socketDir);
        var socketPath = GetUnixSocketPath(socketDir, name);

        // Remove stale socket file from previous run
        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }

        kestrel.ListenUnixSocket(socketPath, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });
    }

    private static void ConfigureNamedPipeEndpoint(KestrelServerOptions kestrel, string prefix, string name)
    {
        var pipeName = GetNamedPipeName(prefix, name);

        kestrel.ListenNamedPipe(pipeName, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });
    }

    private static void ConfigureTcpEndpoint(KestrelServerOptions kestrel, int port)
    {
        kestrel.ListenLocalhost(port, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });
    }
}

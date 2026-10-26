using Grpc.Net.Client;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Provides the authenticated gRPC channel the core uses to call a module host.
/// </summary>
/// <remarks>
/// Channels are per-module and carry the supervisor-issued credential, so callers must never
/// build their own channel to a module endpoint — doing so produces a call the module rejects.
/// </remarks>
public interface IModuleGrpcChannelProvider
{
    /// <summary>
    /// Gets or creates the shared channel for <paramref name="moduleId"/>.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="endpoint">The module's gRPC endpoint, e.g. <c>http://localhost:50105</c>.</param>
    /// <returns>The shared channel. Do not dispose it.</returns>
    GrpcChannel GetOrCreateChannel(string moduleId, string endpoint);
}

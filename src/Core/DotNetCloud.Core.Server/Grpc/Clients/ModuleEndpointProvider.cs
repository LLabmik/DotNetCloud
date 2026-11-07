using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Supervisor;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Resolves gRPC endpoints for process-isolated modules by querying the
/// <see cref="IProcessSupervisor"/> which tracks running module processes.
/// Falls back to the deterministic endpoint the supervisor will assign when a module is not
/// running yet, so a channel created early still points at the right socket or port.
/// </summary>
public sealed class ModuleEndpointProvider
{
    private readonly IProcessSupervisor _supervisor;
    private readonly IModuleGrpcChannelProvider _channelProvider;
    private readonly ModuleEndpointAllocator _endpoints;
    private readonly ILogger<ModuleEndpointProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleEndpointProvider"/> class.
    /// </summary>
    /// <param name="supervisor">The process supervisor.</param>
    /// <param name="channelProvider">The credential-bearing channel provider.</param>
    /// <param name="endpoints">The shared endpoint allocator.</param>
    /// <param name="logger">The logger.</param>
    public ModuleEndpointProvider(
        IProcessSupervisor supervisor,
        IModuleGrpcChannelProvider channelProvider,
        ModuleEndpointAllocator endpoints,
        ILogger<ModuleEndpointProvider> logger)
    {
        _supervisor = supervisor;
        _channelProvider = channelProvider;
        _endpoints = endpoints;
        _logger = logger;
    }

    /// <summary>
    /// Gets the authenticated gRPC channel for a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <returns>The shared, credential-bearing channel. Do not dispose it.</returns>
    /// <remarks>
    /// Module hosts reject unauthenticated data-plane calls, so every core→module client must use
    /// this rather than building its own <see cref="GrpcChannel.ForAddress(string, GrpcChannelOptions)"/>.
    /// </remarks>
    public GrpcChannel GetChannel(string moduleId) =>
        _channelProvider.GetOrCreateChannel(moduleId, GetEndpoint(moduleId));

    /// <summary>
    /// Gets the gRPC endpoint for a module: the running module's endpoint when available,
    /// otherwise the endpoint the supervisor will assign on its next start.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <returns>The endpoint string (socket, pipe or loopback TCP form).</returns>
    public string GetEndpoint(string moduleId)
    {
        var info = _supervisor.GetModuleInfo(moduleId);
        if (info is not null && !string.IsNullOrEmpty(info.GrpcEndpoint))
        {
            _logger.LogDebug("Module {ModuleId} endpoint from supervisor: {Endpoint}", moduleId, info.GrpcEndpoint);
            return info.GrpcEndpoint;
        }

        // Deterministic: sockets/pipes are derived from the module id, and the TCP port is the
        // same hash + probe start the supervisor uses, so this cannot disagree with the launch.
        var fallback = _endpoints.Resolve(moduleId).ToEndpointString();
        _logger.LogDebug("Module {ModuleId} not yet running, using planned endpoint: {Endpoint}", moduleId, fallback);
        return fallback;
    }
}

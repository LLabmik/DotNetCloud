using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Supervisor;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Resolves gRPC endpoints for process-isolated modules by querying the
/// <see cref="IProcessSupervisor"/> which tracks running module processes.
/// Falls back to TCP defaults when modules aren't yet running.
/// </summary>
public sealed class ModuleEndpointProvider
{
    private readonly IProcessSupervisor _supervisor;
    private readonly IModuleGrpcChannelProvider _channelProvider;
    private readonly ProcessSupervisorOptions _options;
    private readonly ILogger<ModuleEndpointProvider> _logger;

    public ModuleEndpointProvider(
        IProcessSupervisor supervisor,
        IModuleGrpcChannelProvider channelProvider,
        Microsoft.Extensions.Options.IOptions<ProcessSupervisorOptions> options,
        ILogger<ModuleEndpointProvider> logger)
    {
        _supervisor = supervisor;
        _channelProvider = channelProvider;
        _options = options.Value;
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
    /// Gets the gRPC endpoint for a module. Uses the running module's endpoint
    /// if available, otherwise falls back to a TCP default based on module ID hash.
    /// </summary>
    public string GetEndpoint(string moduleId)
    {
        // If the module is already running via the supervisor, use its actual endpoint
        var info = _supervisor.GetModuleInfo(moduleId);
        if (info is not null && !string.IsNullOrEmpty(info.GrpcEndpoint))
        {
            _logger.LogDebug("Module {ModuleId} endpoint from supervisor: {Endpoint}", moduleId, info.GrpcEndpoint);
            return info.GrpcEndpoint;
        }

        // Fallback: compute TCP endpoint matching the supervisor's port allocation
        var port = AllocateTcpPort(moduleId);
        var fallback = $"http://localhost:{port}";
        _logger.LogDebug("Module {ModuleId} not yet running, using fallback: {Endpoint}", moduleId, fallback);
        return fallback;
    }

    private int AllocateTcpPort(string moduleId)
    {
        // Reserve TcpPortRangeStart for the core gRPC server.
        // Module ports start at TcpPortRangeStart + 1.
        var hash = Math.Abs(moduleId.GetHashCode());
        var range = Math.Max(1, _options.TcpPortRangeEnd - _options.TcpPortRangeStart - 1);
        return _options.TcpPortRangeStart + 1 + (hash % range);
    }
}

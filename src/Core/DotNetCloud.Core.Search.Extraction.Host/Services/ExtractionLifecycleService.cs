using DotNetCloud.Core.Grpc.Lifecycle;
using Grpc.Core;

namespace DotNetCloud.Core.Search.Extraction.Host.Services;

/// <summary>
/// Lifecycle gRPC service for the extraction worker.
/// The worker has no module state, database, or capabilities to manage, so
/// initialize/start/stop are no-ops that report success. The manifest identifies
/// the worker as <c>dotnetcloud.extraction</c>.
/// </summary>
public sealed class ExtractionLifecycleService : ModuleLifecycle.ModuleLifecycleBase
{
    private readonly ILogger<ExtractionLifecycleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionLifecycleService"/> class.
    /// </summary>
    public ExtractionLifecycleService(ILogger<ExtractionLifecycleService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public override Task<InitializeResponse> Initialize(InitializeRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Initializing extraction worker via gRPC: {ModuleId}", request.ModuleId);
        return Task.FromResult(new InitializeResponse { Success = true });
    }

    /// <inheritdoc />
    public override Task<StartResponse> Start(StartRequest request, ServerCallContext context)
    {
        return Task.FromResult(new StartResponse { Success = true });
    }

    /// <inheritdoc />
    public override Task<StopResponse> Stop(StopRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Stopping extraction worker via gRPC");
        return Task.FromResult(new StopResponse { Success = true });
    }

    /// <inheritdoc />
    public override Task<HealthCheckResponse> HealthCheck(HealthCheckRequest request, ServerCallContext context)
    {
        var response = new HealthCheckResponse
        {
            Status = HealthStatus.Healthy,
            Description = "Extraction worker is healthy"
        };
        response.Metadata.Add("module_id", "dotnetcloud.extraction");
        response.Metadata.Add("version", "1.0.0");
        return Task.FromResult(response);
    }

    /// <inheritdoc />
    public override Task<GetManifestResponse> GetManifest(GetManifestRequest request, ServerCallContext context)
    {
        var response = new GetManifestResponse
        {
            ModuleId = "dotnetcloud.extraction",
            Name = "Extraction",
            Version = "1.0.0"
        };
        return Task.FromResult(response);
    }
}

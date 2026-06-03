using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules;
using Grpc.Core;

namespace DotNetCloud.Modules.AI.Host.Services;

/// <summary>
/// Lifecycle gRPC service for the AI module.
/// Allows the core supervisor to initialize, start, stop, health-check, and inspect the AI module.
/// </summary>
public sealed class AiLifecycleService : ModuleLifecycle.ModuleLifecycleBase
{
    private readonly AiModule _module;
    private readonly ILogger<AiLifecycleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiLifecycleService"/> class.
    /// </summary>
    public AiLifecycleService(AiModule module, ILogger<AiLifecycleService> logger)
    {
        _module = module;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<InitializeResponse> Initialize(
        InitializeRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Initializing AI module via gRPC: {ModuleId}", request.ModuleId);

            var config = request.Configuration
                .ToDictionary(kv => kv.Key, kv => (object)kv.Value);

            var initContext = new ModuleInitializationContext
            {
                ModuleId = request.ModuleId,
                Services = context.GetHttpContext().RequestServices,
                Configuration = config,
                SystemCaller = Core.Authorization.CallerContext.CreateSystemContext()
            };

            await _module.InitializeAsync(initContext, context.CancellationToken);

            return new InitializeResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AI module");
            return new InitializeResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<StartResponse> Start(
        StartRequest request, ServerCallContext context)
    {
        try
        {
            await _module.StartAsync(context.CancellationToken);
            return new StartResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start AI module");
            return new StartResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<StopResponse> Stop(
        StopRequest request, ServerCallContext context)
    {
        try
        {
            await _module.StopAsync(context.CancellationToken);
            return new StopResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop AI module");
            return new StopResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override Task<HealthCheckResponse> HealthCheck(
        HealthCheckRequest request, ServerCallContext context)
    {
        var response = new HealthCheckResponse
        {
            Status = HealthStatus.Healthy,
            Description = "AI module is healthy"
        };
        response.Metadata.Add("module_id", _module.Manifest.Id);
        response.Metadata.Add("version", _module.Manifest.Version);

        return Task.FromResult(response);
    }

    /// <inheritdoc />
    public override Task<GetManifestResponse> GetManifest(
        GetManifestRequest request, ServerCallContext context)
    {
        var manifest = _module.Manifest;
        var response = new GetManifestResponse
        {
            ModuleId = manifest.Id,
            Name = manifest.Name,
            Version = manifest.Version
        };
        response.RequiredCapabilities.AddRange(manifest.RequiredCapabilities);
        response.PublishedEvents.AddRange(manifest.PublishedEvents);
        response.SubscribedEvents.AddRange(manifest.SubscribedEvents);

        return Task.FromResult(response);
    }
}

using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules;
using Grpc.Core;

namespace DotNetCloud.Modules.About.Host.Services;

/// <summary>
/// Implements the <see cref="ModuleLifecycle.ModuleLifecycleBase"/> gRPC service
/// so the core supervisor can control this module's lifecycle and check health.
/// </summary>
public sealed class AboutLifecycleService : ModuleLifecycle.ModuleLifecycleBase
{
    private readonly AboutModule _module;
    private readonly ILogger<AboutLifecycleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutLifecycleService"/> class.
    /// </summary>
    public AboutLifecycleService(AboutModule module, ILogger<AboutLifecycleService> logger)
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
            _logger.LogInformation("Initializing About module via gRPC: {ModuleId}", request.ModuleId);

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
            _logger.LogError(ex, "Failed to initialize About module");
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
            _logger.LogError(ex, "Failed to start About module");
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
            _logger.LogError(ex, "Failed to stop About module");
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
            Description = "About module is healthy"
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

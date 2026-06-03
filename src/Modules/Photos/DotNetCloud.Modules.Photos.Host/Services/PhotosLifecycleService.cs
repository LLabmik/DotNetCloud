using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules;
using Grpc.Core;

namespace DotNetCloud.Modules.Photos.Host.Services;

/// <summary>
/// Lifecycle gRPC service for the Photos module.
/// Allows the core supervisor to initialize, start, stop, health-check, and inspect the module.
/// </summary>
public sealed class PhotosLifecycleService : ModuleLifecycle.ModuleLifecycleBase
{
    private readonly PhotosModule _module;
    private readonly ILogger<PhotosLifecycleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotosLifecycleService"/> class.
    /// </summary>
    public PhotosLifecycleService(PhotosModule module, ILogger<PhotosLifecycleService> logger)
    {
        _module = module;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<InitializeResponse> Initialize(InitializeRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Initializing Photos module via gRPC: {ModuleId}", request.ModuleId);
            var config = request.Configuration.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
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
            _logger.LogError(ex, "Failed to initialize Photos module");
            return new InitializeResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<StartResponse> Start(StartRequest request, ServerCallContext context)
    {
        try
        { await _module.StartAsync(context.CancellationToken); return new StartResponse { Success = true }; }
        catch (Exception ex) { _logger.LogError(ex, "Failed to start Photos module"); return new StartResponse { Success = false, ErrorMessage = ex.Message }; }
    }

    /// <inheritdoc />
    public override async Task<StopResponse> Stop(StopRequest request, ServerCallContext context)
    {
        try
        { await _module.StopAsync(context.CancellationToken); return new StopResponse { Success = true }; }
        catch (Exception ex) { _logger.LogError(ex, "Failed to stop Photos module"); return new StopResponse { Success = false, ErrorMessage = ex.Message }; }
    }

    /// <inheritdoc />
    public override Task<HealthCheckResponse> HealthCheck(HealthCheckRequest request, ServerCallContext context)
    {
        var response = new HealthCheckResponse { Status = HealthStatus.Healthy, Description = "Photos module is healthy" };
        response.Metadata.Add("module_id", _module.Manifest.Id);
        response.Metadata.Add("version", _module.Manifest.Version);
        return Task.FromResult(response);
    }

    /// <inheritdoc />
    public override Task<GetManifestResponse> GetManifest(GetManifestRequest request, ServerCallContext context)
    {
        var m = _module.Manifest;
        var response = new GetManifestResponse { ModuleId = m.Id, Name = m.Name, Version = m.Version };
        response.RequiredCapabilities.AddRange(m.RequiredCapabilities);
        response.PublishedEvents.AddRange(m.PublishedEvents);
        response.SubscribedEvents.AddRange(m.SubscribedEvents);
        return Task.FromResult(response);
    }
}

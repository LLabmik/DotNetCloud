using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules;
using Grpc.Core;

namespace DotNetCloud.Modules.Bookmarks.Host.Services;

/// <summary>
/// gRPC lifecycle service for the Bookmarks module.
/// Receives lifecycle commands from the core supervisor.
/// </summary>
public sealed class BookmarksLifecycleService : ModuleLifecycle.ModuleLifecycleBase
{
    private readonly BookmarksModule _module;
    private readonly ILogger<BookmarksLifecycleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarksLifecycleService"/> class.
    /// </summary>
    public BookmarksLifecycleService(BookmarksModule module, ILogger<BookmarksLifecycleService> logger)
    {
        _module = module;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<InitializeResponse> Initialize(InitializeRequest request, ServerCallContext context)
    {
        try
        {
            var config = request.Configuration.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
            var initContext = new ModuleInitializationContext
            {
                ModuleId = request.ModuleId,
                Services = context.GetHttpContext().RequestServices,
                Configuration = config,
                SystemCaller = CallerContext.CreateSystemContext()
            };
            await _module.InitializeAsync(initContext, context.CancellationToken);
            return new InitializeResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Bookmarks module");
            return new InitializeResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<StartResponse> Start(StartRequest request, ServerCallContext context)
    {
        try
        {
            await _module.StartAsync(context.CancellationToken);
            return new StartResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Bookmarks module");
            return new StartResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<StopResponse> Stop(StopRequest request, ServerCallContext context)
    {
        try
        {
            await _module.StopAsync(context.CancellationToken);
            return new StopResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Bookmarks module");
            return new StopResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override Task<HealthCheckResponse> HealthCheck(HealthCheckRequest request, ServerCallContext context)
    {
        var response = new HealthCheckResponse
        {
            Status = HealthStatus.Healthy,
            Description = "Bookmarks module is healthy"
        };
        response.Metadata.Add("module_id", _module.Manifest.Id);
        response.Metadata.Add("version", _module.Manifest.Version);
        return Task.FromResult(response);
    }

    /// <inheritdoc />
    public override Task<GetManifestResponse> GetManifest(GetManifestRequest request, ServerCallContext context)
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

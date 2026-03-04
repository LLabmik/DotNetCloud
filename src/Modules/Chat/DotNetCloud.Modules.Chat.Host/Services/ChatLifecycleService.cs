using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules;
using Grpc.Core;

namespace DotNetCloud.Modules.Chat.Host.Services;

/// <summary>
/// Implements the <see cref="ModuleLifecycle.ModuleLifecycleBase"/> gRPC service
/// so the core supervisor can control the Chat module's lifecycle and check health.
/// </summary>
public sealed class ChatLifecycleService : ModuleLifecycle.ModuleLifecycleBase
{
    private readonly ChatModule _module;
    private readonly ILogger<ChatLifecycleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatLifecycleService"/> class.
    /// </summary>
    public ChatLifecycleService(ChatModule module, ILogger<ChatLifecycleService> logger)
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
            _logger.LogInformation("Initializing Chat module via gRPC: {ModuleId}", request.ModuleId);

            var config = request.Configuration
                .ToDictionary(kv => kv.Key, kv => (object)kv.Value);

            var initContext = new ModuleInitializationContext
            {
                ModuleId = request.ModuleId,
                Services = GetServiceProvider(context),
                Configuration = config,
                SystemCaller = Core.Authorization.CallerContext.CreateSystemContext()
            };

            await _module.InitializeAsync(initContext, context.CancellationToken);

            return new InitializeResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Chat module");
            return new InitializeResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
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
            _logger.LogError(ex, "Failed to start Chat module");
            return new StartResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
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
            _logger.LogError(ex, "Failed to stop Chat module");
            return new StopResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public override Task<HealthCheckResponse> HealthCheck(
        HealthCheckRequest request, ServerCallContext context)
    {
        var response = new HealthCheckResponse
        {
            Status = HealthStatus.Healthy,
            Description = "Chat module is healthy"
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

    private static IServiceProvider GetServiceProvider(ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.RequestServices;
    }
}

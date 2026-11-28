using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules;
using Grpc.Core;

namespace DotNetCloud.Modules.MyModule.Host.Services;

/// <summary>
/// The lifecycle service the supervisor calls. Every module host must answer these five calls:
/// without them the core cannot initialise, start, stop or health-check the process.
/// </summary>
public sealed class MyModuleLifecycleService : TrackedModuleLifecycleBase
{
    private readonly MyModule _module;
    private readonly ILogger<MyModuleLifecycleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyModuleLifecycleService"/> class.
    /// </summary>
    public MyModuleLifecycleService(MyModule module, ILogger<MyModuleLifecycleService> logger)
    {
        _module = module;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<InitializeResponse> Initialize(
        InitializeRequest request,
        ServerCallContext context)
    {
        try
        {
            var configuration = request.Configuration
                .ToDictionary(pair => pair.Key, pair => (object)pair.Value);

            var initContext = new ModuleInitializationContext
            {
                ModuleId = request.ModuleId,
                Services = GetServiceProvider(context),
                Configuration = configuration,
                SystemCaller = CallerContext.CreateSystemContext(),
            };

            await _module.InitializeAsync(initContext, context.CancellationToken);
            return new InitializeResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize {ModuleId}", _module.Manifest.Id);
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
            _logger.LogError(ex, "Failed to start {ModuleId}", _module.Manifest.Id);
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
            _logger.LogError(ex, "Failed to stop {ModuleId}", _module.Manifest.Id);
            return new StopResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override Task<HealthCheckResponse> HealthCheck(
        HealthCheckRequest request,
        ServerCallContext context)
    {
        var response = new HealthCheckResponse
        {
            Status = HealthStatus.Healthy,
            Description = _module.IsRunning ? "Running" : "Process up, lifecycle not started",
        };
        response.Metadata.Add("module_id", _module.Manifest.Id);
        response.Metadata.Add("version", _module.Manifest.Version);

        return Task.FromResult(response);
    }

    /// <inheritdoc />
    public override Task<GetManifestResponse> GetManifest(
        GetManifestRequest request,
        ServerCallContext context)
    {
        var manifest = _module.Manifest;
        var response = new GetManifestResponse
        {
            ModuleId = manifest.Id,
            Name = manifest.Name,
            Version = manifest.Version,
        };
        response.RequiredCapabilities.AddRange(manifest.RequiredCapabilities);
        response.PublishedEvents.AddRange(manifest.PublishedEvents);
        response.SubscribedEvents.AddRange(manifest.SubscribedEvents);

        return Task.FromResult(response);
    }

    private static IServiceProvider GetServiceProvider(ServerCallContext context) =>
        context.GetHttpContext().RequestServices;
}

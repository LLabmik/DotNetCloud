using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Grpc.Configuration;
using DotNetCloud.Core.Server.Grpc.Interceptors;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Quotas;
using DotNetCloud.Core.Server.Sandbox;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Server.Supervisor;
using DotNetCloud.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DotNetCloud.Core.Server.Extensions;

/// <summary>
/// Extension methods for registering the process supervisor and related services.
/// </summary>
internal static class SupervisorServiceExtensions
{
    /// <summary>
    /// Adds the process supervisor and module management services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure supervisor options.</param>
    /// <param name="enableDetailedErrors">
    /// Whether the module-facing gRPC server should include exception details in status messages.
    /// Only valid in a development environment; production callers pass <see langword="false"/>.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProcessSupervisor(
        this IServiceCollection services,
        Action<ProcessSupervisorOptions>? configureOptions = null,
        bool enableDetailedErrors = false)
    {
        // Register options
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.AddOptions<ProcessSupervisorOptions>()
                .BindConfiguration(ProcessSupervisorOptions.SectionName);
        }

        // Register module loading services
        services.AddSingleton<ModuleDiscoveryService>();
        services.AddSingleton<ModuleManifestLoader>();
        services.AddScoped<ModuleConfigurationLoader>();

        // What each manifest declares it publishes — consulted by PublishEvent so a module can
        // only raise the events it declared (identity is the verified module on the transport).
        services.AddSingleton<IModuleManifestRegistry, ModuleManifestRegistry>();

        // Register supervisor infrastructure
        services.AddSingleton<GrpcChannelManager>();
        services.AddSingleton<IModuleGrpcChannelProvider>(sp => sp.GetRequiredService<GrpcChannelManager>());
        services.AddSingleton<ResourceLimiter>();

        // Per-module resource quota policy (Modules:Quotas) and the single resolver used by the
        // supervisor, the provisioning service and the admin surface.
        services.AddOptions<ModuleQuotasOptions>().BindConfiguration(ModuleQuotasOptions.SectionName);
        services.AddSingleton<ModuleQuotaResolver>();

        // Per-module API rate limits: quotas are cached (the hot path never hits the database) and
        // consumed through fixed one-minute windows keyed by module id and category.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ModuleQuotaCache>();
        services.AddSingleton<ModuleRateLimiter>();
        services.AddSingleton<ModuleRateLimitInterceptor>();

        // Measured data-store/data-directory usage with QuotaExceeded suspension (F10).
        services.AddSingleton<ModuleUsageMeasurer>();
        services.AddHostedService<ModuleUsageMonitor>();

        // Per-module credentials minted by the supervisor at launch and required on every
        // capability call (binds module-id to a secret only the core hands out).
        services.AddSingleton<ModuleCredentialStore>();

        // Signing key for end-user assertions. Modules get only the public half.
        services.AddSingleton<ModuleAssertionKeyRing>();

        // Sandboxed module data stores (scoped schema + principal, encrypted connection string).
        services.AddSingleton<IModuleDataStoreProvisioningService, ModuleDataStoreProvisioningService>();

        // Upgrade path (§9): adopt already-installed modules' core-managed schemas before the
        // supervisor spawns anything. Registered before the supervisor so StartAsync completes first.
        services.AddSingleton<ModuleAdoptionService>();
        services.AddHostedService<ModuleSandboxMigrationHostedService>();

        // Register gRPC interceptors
        services.AddSingleton<ICallerIdentityResolver, CallerIdentityResolver>();
        services.AddSingleton<AuthenticationInterceptor>();
        services.AddSingleton<CallerContextInterceptor>();

        // Access evaluation owns the grant/capability decision; the interceptor is a thin adapter
        // so the internal REST facade can reuse the same policy.
        services.AddSingleton<IModuleAccessEvaluator, ModuleAccessEvaluator>();
        services.AddSingleton<ModuleAccessInterceptor>();
        services.AddSingleton<TracingInterceptor>();
        services.AddSingleton<ErrorHandlingInterceptor>();
        services.AddSingleton<LoggingInterceptor>();

        // Register gRPC services
        services.AddSingleton<CoreCapabilitiesServiceImpl>();

        // Canonical module API: adapts every RPC onto the legacy capability handlers so both
        // surfaces share one code path and one grant check.
        services.AddSingleton<ModuleHealthRegistry>();
        services.AddSingleton<ModuleApiV1Service>();
        services.AddSingleton<TokenIntrospectionServiceImpl>();

        // Internal JSON facade for module backends without a gRPC client. It reuses the same
        // interceptors (credential, caller context, rate limit, capability) as the gRPC listener.
        services.AddSingleton<Rest.ModuleRestApiService>();

        // Register process supervisor as both IProcessSupervisor and IHostedService
        services.AddSingleton<ProcessSupervisor>();
        services.AddSingleton<IProcessSupervisor>(sp => sp.GetRequiredService<ProcessSupervisor>());
        services.AddHostedService(sp => sp.GetRequiredService<ProcessSupervisor>());

        // Register admin module management service
        services.AddScoped<IAdminModuleService, AdminModuleService>();

        // Register gRPC server
        services.AddGrpc(options =>
        {
            options.MaxReceiveMessageSize = 16 * 1024 * 1024; // 16 MB
            options.MaxSendMessageSize = 16 * 1024 * 1024; // 16 MB

            // Exception details leak stack traces and internal paths to module processes. Only a
            // development host may return them; production relies on ErrorHandlingInterceptor.
            options.EnableDetailedErrors = enableDetailedErrors;

            // Add interceptors in order
            options.Interceptors.Add<LoggingInterceptor>();
            options.Interceptors.Add<AuthenticationInterceptor>();
            options.Interceptors.Add<CallerContextInterceptor>();
            options.Interceptors.Add<ModuleRateLimitInterceptor>();
            options.Interceptors.Add<ModuleAccessInterceptor>();
            options.Interceptors.Add<TracingInterceptor>();
            options.Interceptors.Add<ErrorHandlingInterceptor>();
        });

        return services;
    }

    /// <summary>
    /// Configures the gRPC server endpoint for module communication.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    public static void ConfigureGrpcForModules(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration
            .GetSection(ProcessSupervisorOptions.SectionName)
            .Get<ProcessSupervisorOptions>() ?? new ProcessSupervisorOptions();

        // TCP transport for cross-platform compatibility (Linux + Windows)
        options.PreferTcpTransport = true;

        GrpcServerConfiguration.ConfigureCoreGrpcEndpoint(builder, options);
    }

    /// <summary>
    /// Maps gRPC services for module communication.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapModuleGrpcServices(this WebApplication app)
    {
        app.MapGrpcService<CoreCapabilitiesServiceImpl>();
        app.MapGrpcService<ModuleApiV1Service>();
        app.MapGrpcService<TokenIntrospectionServiceImpl>();
        return app;
    }
}

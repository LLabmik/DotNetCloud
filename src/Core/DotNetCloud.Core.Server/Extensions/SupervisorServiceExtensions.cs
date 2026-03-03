using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Grpc.Configuration;
using DotNetCloud.Core.Server.Grpc.Interceptors;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Server.Supervisor;
using DotNetCloud.Core.Services;
using Microsoft.Extensions.DependencyInjection;
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
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProcessSupervisor(
        this IServiceCollection services,
        Action<ProcessSupervisorOptions>? configureOptions = null)
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
        services.AddScoped<CapabilityValidator>();
        services.AddScoped<ModuleConfigurationLoader>();

        // Register supervisor infrastructure
        services.AddSingleton<GrpcChannelManager>();
        services.AddSingleton<ResourceLimiter>();

        // Register gRPC interceptors
        services.AddSingleton<AuthenticationInterceptor>();
        services.AddSingleton<CallerContextInterceptor>();
        services.AddSingleton<TracingInterceptor>();
        services.AddSingleton<ErrorHandlingInterceptor>();
        services.AddSingleton<LoggingInterceptor>();

        // Register gRPC services
        services.AddSingleton<CoreCapabilitiesServiceImpl>();

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
            options.EnableDetailedErrors = true;

            // Add interceptors in order
            options.Interceptors.Add<LoggingInterceptor>();
            options.Interceptors.Add<AuthenticationInterceptor>();
            options.Interceptors.Add<CallerContextInterceptor>();
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
        return app;
    }
}

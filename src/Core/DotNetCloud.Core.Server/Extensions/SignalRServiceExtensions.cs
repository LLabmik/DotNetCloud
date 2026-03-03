using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Server.Configuration;
using DotNetCloud.Core.Server.RealTime;
using Microsoft.AspNetCore.Http.Connections;

namespace DotNetCloud.Core.Server.Extensions;

/// <summary>
/// Extension methods for registering SignalR real-time communication services.
/// </summary>
internal static class SignalRServiceExtensions
{
    /// <summary>
    /// Adds SignalR services, connection tracking, presence, and broadcaster to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDotNetCloudSignalR(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options
        var options = new SignalROptions();
        configuration.GetSection(SignalROptions.SectionName).Bind(options);
        services.Configure<SignalROptions>(configuration.GetSection(SignalROptions.SectionName));

        // Add SignalR with configuration
        services.AddSignalR(hubOptions =>
        {
            hubOptions.KeepAliveInterval = TimeSpan.FromSeconds(options.KeepAliveIntervalSeconds);
            hubOptions.ClientTimeoutInterval = TimeSpan.FromSeconds(options.ClientTimeoutSeconds);
            hubOptions.HandshakeTimeout = TimeSpan.FromSeconds(options.HandshakeTimeoutSeconds);
            hubOptions.MaximumParallelInvocationsPerClient = options.MaximumParallelInvocationsPerClient;
            hubOptions.MaximumReceiveMessageSize = options.MaximumReceiveMessageSize;
            hubOptions.EnableDetailedErrors = options.EnableDetailedErrors;
        });

        // Register connection tracking (singleton for cross-request state)
        services.AddSingleton<UserConnectionTracker>();

        // Register presence service (singleton, wraps connection tracker + last-seen state)
        services.AddSingleton<PresenceService>();
        services.AddSingleton<IPresenceTracker>(sp => sp.GetRequiredService<PresenceService>());

        // Register broadcaster (singleton, uses IHubContext which is also singleton)
        services.AddSingleton<RealtimeBroadcasterService>();
        services.AddSingleton<IRealtimeBroadcaster>(sp => sp.GetRequiredService<RealtimeBroadcasterService>());

        return services;
    }

    /// <summary>
    /// Maps the DotNetCloud SignalR hub endpoint and configures transports.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapDotNetCloudHubs(this WebApplication app)
    {
        var options = new SignalROptions();
        app.Configuration.GetSection(SignalROptions.SectionName).Bind(options);

        app.MapHub<CoreHub>(options.HubPath, connectionOptions =>
        {
            var transports = HttpTransportType.None;

            if (options.EnableWebSockets)
                transports |= HttpTransportType.WebSockets;
            if (options.EnableServerSentEvents)
                transports |= HttpTransportType.ServerSentEvents;
            if (options.EnableLongPolling)
                transports |= HttpTransportType.LongPolling;

            // Default to all transports if none explicitly enabled
            if (transports == HttpTransportType.None)
                transports = HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;

            connectionOptions.Transports = transports;

            if (options.WebSocketKeepAliveSeconds > 0)
            {
                connectionOptions.WebSockets.CloseTimeout = TimeSpan.FromSeconds(options.WebSocketKeepAliveSeconds);
            }
        })
        .RequireAuthorization();

        return app;
    }
}

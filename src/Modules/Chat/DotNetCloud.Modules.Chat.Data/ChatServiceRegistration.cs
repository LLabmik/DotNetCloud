using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Chat.Data;

/// <summary>
/// Extension methods for registering chat services in the DI container.
/// </summary>
public static class ChatServiceRegistration
{
    /// <summary>
    /// Registers all chat service implementations in the DI container.
    /// </summary>
    public static IServiceCollection AddChatServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FcmPushOptions>(configuration.GetSection(FcmPushOptions.SectionName));

        services.AddHttpClient("fcm");

        services.AddScoped<IChannelService, ChannelService>();
        services.AddScoped<IChannelMemberService, ChannelMemberService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IReactionService, ReactionService>();
        services.AddScoped<IPinService, PinService>();
        services.AddSingleton<ITypingIndicatorService, TypingIndicatorService>();

        // Administrator-configurable limits and retention policy (module "dotnetcloud.chat" in the
        // core SystemSettings table). Scoped so each request/sweep observes freshly saved values;
        // the provider itself caches for a short window to avoid a settings read per message.
        services.AddScoped<IChatSettingsProvider, ChatSettingsProvider>();
        services.AddScoped<IChatRetentionService, ChatRetentionService>();

        // SSRF-safe link previews for chat messages (registered alongside MessageService so both
        // the in-process web host and the process-isolated module host unfurl consistently).
        services.AddSingleton<SafeUrlFetcher>();
        services.AddSingleton<ILinkPreviewService, LinkPreviewService>();
        services.AddSingleton<IChatRealtimeService, ChatRealtimeService>();
        services.AddSingleton<IChatMessageNotifier, InProcessChatMessageNotifier>();
        services.AddScoped<GlobalChatNotificationState>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IChannelInviteService, ChannelInviteService>();
        services.AddScoped<IMentionNotificationService, MentionNotificationService>();
        services.AddSingleton<INotificationPreferenceStore, DbNotificationPreferenceStore>();
        services.AddSingleton<INotificationDeliveryQueue, InMemoryNotificationDeliveryQueue>();
        services.AddSingleton<IFcmTransport>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<FcmPushOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.CredentialsPath) && File.Exists(options.CredentialsPath))
            {
                return new FcmHttpTransport(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("fcm"),
                    sp.GetRequiredService<IOptions<FcmPushOptions>>(),
                    sp.GetRequiredService<ILogger<FcmHttpTransport>>());
            }

            return new FcmLoggingTransport(sp.GetRequiredService<ILogger<FcmLoggingTransport>>());
        });
        // Push notification providers and router
        services.AddSingleton<FcmPushProvider>();
        services.AddSingleton<IPushProviderEndpoint>(sp => sp.GetRequiredService<FcmPushProvider>());
        services.AddSingleton<NotificationRouter>();
        services.AddSingleton<IQueuedNotificationDispatcher>(sp => sp.GetRequiredService<NotificationRouter>());
        services.AddSingleton<IPushNotificationService>(sp => sp.GetRequiredService<NotificationRouter>());
        services.AddHostedService<NotificationDeliveryBackgroundService>();
        services.AddHostedService<ChatEventSubscriber>();

        // Video call management
        services.AddScoped<IVideoCallService, VideoCallService>();
        services.AddScoped<ICallSignalingService, CallSignalingService>();
        services.AddScoped<ICallNotificationHandler, CallNotificationEventHandler>();
        services.AddScoped<IWebRtcInteropService>(sp =>
            sp.GetService<IJSRuntime>() is null
                ? new NoOpWebRtcInteropService()
                : ActivatorUtilities.CreateInstance<WebRtcInteropService>(sp));
        services.AddScoped<IUserBlockService, UserBlockService>();

        // ICE server configuration (built-in STUN + optional TURN)
        services.Configure<IceServerOptions>(configuration.GetSection(IceServerOptions.SectionName));
        services.AddSingleton<IIceServerService, IceServerService>();
        services.AddHostedService<StunServer>();

        // LiveKit SFU integration (optional — falls back to NullLiveKitService when not configured)
        services.Configure<LiveKitOptions>(configuration.GetSection(LiveKitOptions.SectionName));
        services.AddHttpClient("livekit");
        services.AddSingleton<ILiveKitService>(sp =>
        {
            var liveKitOptions = sp.GetRequiredService<IOptions<LiveKitOptions>>().Value;
            if (liveKitOptions.Enabled && liveKitOptions.IsValid())
            {
                return new LiveKitService(
                    sp.GetRequiredService<IOptions<LiveKitOptions>>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<ILogger<LiveKitService>>());
            }

            return new NullLiveKitService(
                sp.GetRequiredService<ILogger<NullLiveKitService>>(),
                liveKitOptions.MaxP2PParticipants);
        });

        // Chat image upload storage
        services.AddSingleton<IChatImageStore, LocalChatImageStore>();

        // Cross-module Tracks activity display (null-object when Tracks not installed)
        services.AddSingleton<ITracksActivitySignalRService, NullTracksActivitySignalRService>();

        // Real-time broadcasting (null-object in module-host mode — no in-process SignalR available).
        // The real IRealtimeBroadcaster lives in Core.Server's SignalR infrastructure.
        // Module hosts that need real-time broadcasts should use a gRPC-based broadcaster.
        // The gRPC-based broadcaster (GrpcRealtimeBroadcaster) is registered in Chat.Host/Program.cs.
        services.AddSingleton<IRealtimeBroadcaster, NullRealtimeBroadcasterService>();

        return services;
    }

    /// <summary>
    /// Registers the background retention/archiving sweep.
    /// </summary>
    /// <remarks>
    /// Call this from the process-isolated Chat module host only. Core.Server also builds an
    /// in-process chat container for the Blazor UI, and registering the sweep there as well would
    /// run the retention policy twice against the same database.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddChatRetentionBackgroundService(this IServiceCollection services)
    {
        services.AddHostedService<ChatRetentionBackgroundService>();
        return services;
    }
}

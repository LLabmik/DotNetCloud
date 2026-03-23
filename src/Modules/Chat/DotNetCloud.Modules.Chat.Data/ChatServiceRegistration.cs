using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        services.Configure<UnifiedPushOptions>(configuration.GetSection(UnifiedPushOptions.SectionName));

        services.AddHttpClient("fcm");

        services.AddScoped<IChannelService, ChannelService>();
        services.AddScoped<IChannelMemberService, ChannelMemberService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IReactionService, ReactionService>();
        services.AddScoped<IPinService, PinService>();
        services.AddSingleton<ITypingIndicatorService, TypingIndicatorService>();
        services.AddSingleton<IChatRealtimeService, ChatRealtimeService>();
        services.AddSingleton<IChatMessageNotifier, InProcessChatMessageNotifier>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IChannelInviteService, ChannelInviteService>();
        services.AddScoped<IMentionNotificationService, MentionNotificationService>();
        services.AddSingleton<INotificationPreferenceStore, InMemoryNotificationPreferenceStore>();
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
        services.AddSingleton<IUnifiedPushTransport, UnifiedPushLoggingTransport>();

        // Push notification providers and router
        services.AddSingleton<FcmPushProvider>();
        services.AddSingleton<UnifiedPushProvider>();
        services.AddSingleton<IPushProviderEndpoint>(sp => sp.GetRequiredService<FcmPushProvider>());
        services.AddSingleton<IPushProviderEndpoint>(sp => sp.GetRequiredService<UnifiedPushProvider>());
        services.AddSingleton<NotificationRouter>();
        services.AddSingleton<IQueuedNotificationDispatcher>(sp => sp.GetRequiredService<NotificationRouter>());
        services.AddSingleton<IPushNotificationService>(sp => sp.GetRequiredService<NotificationRouter>());
        services.AddHostedService<NotificationDeliveryBackgroundService>();

        return services;
    }
}

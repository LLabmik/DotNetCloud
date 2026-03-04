using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Chat.Data;

/// <summary>
/// Extension methods for registering chat services in the DI container.
/// </summary>
public static class ChatServiceRegistration
{
    /// <summary>
    /// Registers all chat service implementations in the DI container.
    /// </summary>
    public static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        services.AddScoped<IChannelService, ChannelService>();
        services.AddScoped<IChannelMemberService, ChannelMemberService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IReactionService, ReactionService>();
        services.AddScoped<IPinService, PinService>();
        services.AddSingleton<ITypingIndicatorService, TypingIndicatorService>();
        services.AddSingleton<IChatRealtimeService, ChatRealtimeService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();

        // Push notification providers and router
        services.AddSingleton<FcmPushProvider>();
        services.AddSingleton<UnifiedPushProvider>();
        services.AddSingleton<NotificationRouter>();
        services.AddSingleton<IPushNotificationService>(sp => sp.GetRequiredService<NotificationRouter>());

        return services;
    }
}

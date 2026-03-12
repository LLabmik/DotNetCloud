using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Client.Android.Views;
using DotNetCloud.Client.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Configures MAUI hosting and registers all application services via dependency injection.
/// </summary>
public static class MauiProgram
{
    /// <summary>Creates and returns the configured <see cref="MauiApp"/> instance.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ── Infrastructure ────────────────────────────────────────────
        builder.Services.AddSingleton<ISecureTokenStore, AndroidKeyStoreTokenStore>();
        builder.Services.AddSingleton<IServerConnectionStore, PreferenceServerConnectionStore>();
        builder.Services.AddSingleton<ILocalMessageCache, SqliteMessageCache>();
        builder.Services.AddSingleton<IPendingMessageQueue, SqlitePendingMessageQueue>();

        // ── Auth ──────────────────────────────────────────────────────
        builder.Services.AddSingleton<IOAuth2Service, MauiOAuth2Service>();

        // ── Chat / real-time ──────────────────────────────────────────
        builder.Services.AddSingleton<IChatSignalRClient, SignalRChatClient>();
        builder.Services.AddHttpClient<IChatRestClient, HttpChatRestClient>();

        // ── Push notifications ────────────────────────────────────────
#if GOOGLEPLAY
        builder.Services.AddSingleton<IPushNotificationService, FcmPushService>();
#elif FDROID
        builder.Services.AddSingleton<IPushNotificationService, UnifiedPushService>();
#endif

        // ── Photo auto-upload ─────────────────────────────────────────
        builder.Services.AddHttpClient<IPhotoAutoUploadService, PhotoAutoUploadService>();

        // ── ViewModels ────────────────────────────────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<ChannelListViewModel>();
        builder.Services.AddTransient<MessageListViewModel>();
        builder.Services.AddTransient<ChannelDetailsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // ── Pages ─────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ChannelListPage>();
        builder.Services.AddTransient<MessageListPage>();
        builder.Services.AddTransient<ChannelDetailsPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Expose the service provider globally via CommunityToolkit.Mvvm
        Ioc.Default.ConfigureServices(app.Services);

        return app;
    }
}

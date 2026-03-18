using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Files;
using DotNetCloud.Client.Android.Platforms.Android;
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
        builder.Services.AddSingleton<IAppPreferences, MauiAppPreferences>();

        // ── Auth ──────────────────────────────────────────────────────
        builder.Services.AddSingleton<IOAuth2Service, MauiOAuth2Service>();
        builder.Services.AddTransient<AuthenticatedHttpClientHandler>();

        // ── Chat / real-time ──────────────────────────────────────────
        builder.Services.AddSingleton<IChatSignalRClient, SignalRChatClient>();
        builder.Services.AddHttpClient<IChatRestClient, HttpChatRestClient>()
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);

        // ── Push notifications ────────────────────────────────────────
#if GOOGLEPLAY
        builder.Services.AddSingleton<IPushNotificationService, FcmPushService>();
#elif FDROID
        builder.Services.AddSingleton<IPushNotificationService, UnifiedPushService>();
#endif

        // ── Photo auto-upload ─────────────────────────────────────────
        builder.Services.AddHttpClient<IPhotoAutoUploadService, PhotoAutoUploadService>()
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);

        // ── Files / media upload ────────────────────────────────────
        builder.Services.AddHttpClient<IFileRestClient, HttpFileRestClient>()
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);
        builder.Services.AddSingleton<IMediaAutoUploadService, MediaAutoUploadService>();

        // ── Platform services ─────────────────────────────────────────
        builder.Services.AddSingleton<IBatteryOptimizationService, AndroidBatteryOptimizationService>();

        // ── ViewModels ────────────────────────────────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<ChannelListViewModel>();
        builder.Services.AddTransient<MessageListViewModel>();
        builder.Services.AddTransient<ChannelDetailsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<FileBrowserViewModel>();

        // ── Pages ─────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ChannelListPage>();
        builder.Services.AddTransient<MessageListPage>();
        builder.Services.AddTransient<ChannelDetailsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<FileBrowserPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Expose the service provider globally via CommunityToolkit.Mvvm
        Ioc.Default.ConfigureServices(app.Services);

        return app;
    }
}

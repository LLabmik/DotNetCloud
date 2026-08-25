using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Files;
using DotNetCloud.Client.Android.Music;
using DotNetCloud.Client.Android.Notes;
using DotNetCloud.Client.Android.Platforms.Android;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Client.Android.Views;
using DotNetCloud.Client.Core;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Services;
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
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

        // ── Infrastructure ────────────────────────────────────────────
        builder.Services.AddSingleton<ISecureTokenStore, AndroidKeyStoreTokenStore>();
        builder.Services.AddSingleton<IServerConnectionStore, PreferenceServerConnectionStore>();
        builder.Services.AddSingleton<ILocalMessageCache, SqliteMessageCache>();
        builder.Services.AddSingleton<IAppPreferences, MauiAppPreferences>();

        // ── Offline queue / sync ──────────────────────────────────────
        builder.Services.AddSingleton<IOfflineOperationQueue, SqliteOfflineOperationQueue>();
        builder.Services.AddSingleton<IConnectivityMonitor, ConnectivityMonitorService>();
        builder.Services.AddSingleton<IOfflineSyncService, OfflineSyncService>();
        builder.Services.AddSingleton<IServerReachabilityService, ServerReachabilityService>();
        builder.Services.AddSingleton<ConnectivityViewModel>();
        builder.Services.AddTransient<TimeoutHandler>();

        // ── Auth ──────────────────────────────────────────────────────
        builder.Services.AddSingleton<IOAuth2Service, MauiOAuth2Service>();
        builder.Services.AddSingleton<ITokenRefreshService, TokenRefreshService>();
        builder.Services.AddTransient<AuthenticatedHttpClientHandler>();

        // ── Chat / real-time (single shared CoreHub connection) ─────
        builder.Services.AddSingleton<ICoreHubClient, SignalRChatClient>();
        builder.Services.AddSingleton<IChatSignalRClient>(sp => sp.GetRequiredService<ICoreHubClient>());
        builder.Services.AddSingleton<ICalendarSignalRClient, CalendarSignalRClient>();
        builder.Services.AddHttpClient<IChatRestClient, HttpChatRestClient>()
            .AddHttpMessageHandler<TimeoutHandler>()
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);

        // ── Push notifications ────────────────────────────────────────
#if GOOGLEPLAY
        builder.Services.AddSingleton<IPushNotificationService, FcmPushService>();
#elif FDROID
        builder.Services.AddSingleton<IPushNotificationService, UnifiedPushService>();
#endif

        // ── Files / media upload ────────────────────────────────────
        builder.Services.AddHttpClient<IFileRestClient, HttpFileRestClient>()
            .AddHttpMessageHandler<TimeoutHandler>()
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);
        builder.Services.AddSingleton<IMediaAutoUploadService, MediaAutoUploadService>();

        // ── Platform services ─────────────────────────────────────────
        builder.Services.AddSingleton<IBatteryOptimizationService, AndroidBatteryOptimizationService>();

        // ── Foreground tracking & mute state ──────────────────────────
        builder.Services.AddSingleton<IAppForegroundService, AppForegroundService>();
        builder.Services.AddSingleton<IChannelMuteStateService, ChannelMuteStateService>();

        // ── Update services ───────────────────────────────────────────
        builder.Services.AddHttpClient<IClientUpdateService, ClientUpdateService>()
            .AddHttpMessageHandler<TimeoutHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);
        builder.Services.AddSingleton<IAndroidUpdateService, AndroidUpdateService>();

        // ── Music ─────────────────────────────────────────────────────────
        builder.Services.AddHttpClient<IMusicRestClient, HttpMusicRestClient>()
            .AddHttpMessageHandler<TimeoutHandler>()
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);

        builder.Services.AddHttpClient<IAlbumArtCache, AlbumArtCache>()
            .AddHttpMessageHandler<TimeoutHandler>()
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);

        // Thumbnail cache
        builder.Services.AddHttpClient<IThumbnailCache, ThumbnailCache>()
            .AddHttpMessageHandler<TimeoutHandler>()
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);

        builder.Services.AddSingleton<IMusicPlayerService, MusicPlayerService>();
        builder.Services.AddSingleton<IEqualizerService, AndroidEqualizerService>();

        // ── Calendar ────────────────────────────────────────────────────
        builder.Services.AddHttpClient<ICalendarRestClient, HttpCalendarRestClient>()
            .AddHttpMessageHandler<TimeoutHandler>()
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);
        builder.Services.AddSingleton<ICalendarReminderScheduler, CalendarReminderScheduler>();
        builder.Services.AddSingleton<IExactAlarmPermissionService, AndroidExactAlarmPermissionService>();
        builder.Services.AddSingleton<INotificationPermissionService, AndroidNotificationPermissionService>();

        // ── Notes ───────────────────────────────────────────────────────
        builder.Services.AddHttpClient<INotesRestClient, HttpNotesRestClient>()
            .AddHttpMessageHandler<TimeoutHandler>()
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);

        // ── ViewModels ────────────────────────────────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<ChannelListViewModel>();
        builder.Services.AddTransient<MessageListViewModel>();
        builder.Services.AddTransient<ChannelDetailsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<FileBrowserViewModel>();
        builder.Services.AddTransient<MusicViewModel>();
        builder.Services.AddTransient<CalendarViewModel>();
        builder.Services.AddTransient<EventDetailViewModel>();
        builder.Services.AddTransient<EventEditViewModel>();
        builder.Services.AddTransient<ImageViewerViewModel>();
        builder.Services.AddTransient<NotesViewModel>();
        builder.Services.AddTransient<NoteEditViewModel>();

        // ── Pages ─────────────────────────────────────────────────────
        builder.Services.AddTransient<LandingPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ChannelListPage>();
        builder.Services.AddTransient<MessageListPage>();
        builder.Services.AddTransient<ChannelDetailsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<FileBrowserPage>();
        builder.Services.AddTransient<MusicPage>();
        builder.Services.AddTransient<CalendarPage>();
        builder.Services.AddTransient<EventDetailPage>();
        builder.Services.AddTransient<EventEditPage>();
        builder.Services.AddTransient<ImageViewerPage>();
        builder.Services.AddTransient<NotesPage>();
        builder.Services.AddTransient<NoteEditPage>();
        builder.Services.AddTransient<DmUserPickerPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Expose the service provider globally via CommunityToolkit.Mvvm
        Ioc.Default.ConfigureServices(app.Services);

        return app;
    }
}

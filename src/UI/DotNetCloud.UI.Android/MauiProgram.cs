using Microsoft.Extensions.Logging;

namespace DotNetCloud.UI.Android;

/// <summary>
/// MAUI Blazor Hybrid application entry point for the Android client.
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Creates and configures the MAUI app builder.
    /// </summary>
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

        // Blazor WebView
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Chat services
        builder.Services.AddSingleton<Services.IChatApiClient, Services.ChatApiClient>();
        builder.Services.AddSingleton<Services.ISignalRChatService, Services.SignalRChatService>();
        builder.Services.AddSingleton<Services.IOfflineStorageService, Services.OfflineStorageService>();
        builder.Services.AddSingleton<Services.IAuthenticationService, Services.AuthenticationService>();

        // HTTP client for API calls
        builder.Services.AddHttpClient("ChatApi", client =>
        {
            client.BaseAddress = new Uri("https://api.dotnetcloud.example/");
        });

        return builder.Build();
    }
}

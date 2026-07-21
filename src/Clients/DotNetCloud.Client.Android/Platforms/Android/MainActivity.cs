using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Activity;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Primary Android activity. Handles launch, orientation changes, and serves as the MAUI host.
/// </summary>
[Activity(
    Label = "@string/app_name",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        try
        {
            Ioc.Default.GetService<IAppForegroundService>()?.SetForeground(true);
        }
        catch { /* Best effort */ }

        // Handle deep-link from notification tap (both cold start and resume)
        HandleCalendarDeepLink();
    }

    /// <inheritdoc />
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent is not null)
            Intent = intent; // Ensure the latest intent is used for deep-link handling
    }

    /// <inheritdoc />
    protected override void OnPause()
    {
        base.OnPause();
        try
        {
            Ioc.Default.GetService<IAppForegroundService>()?.SetForeground(false);
        }
        catch { /* Best effort */ }
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        base.OnDestroy();
        try
        {
            CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default
                .GetService<DotNetCloud.Client.Android.Services.IMusicPlayerService>()?.Stop();
        }
        catch
        {
            // Best effort — process is shutting down
        }
    }

    // ── Calendar notification deep-link ─────────────────────────────────────

    private void HandleCalendarDeepLink()
    {
        var eventId = Intent?.GetStringExtra("eventId");
        if (string.IsNullOrWhiteSpace(eventId))
            return;

        // Clear the extra so we don't re-navigate on subsequent OnResume calls
        Intent?.RemoveExtra("eventId");

        _ = Shell.Current?.GoToAsync($"EventDetail?EventId={eventId}");
    }
}

using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

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
}

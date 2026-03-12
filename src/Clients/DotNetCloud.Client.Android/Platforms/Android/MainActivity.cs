using Android.App;
using Android.Content.PM;
using Android.OS;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Primary Android activity. Handles launch, orientation changes, and serves as the MAUI host.
/// </summary>
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}

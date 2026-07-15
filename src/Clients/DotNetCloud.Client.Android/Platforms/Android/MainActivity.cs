using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Activity;

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
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Intercept back button: pop Shell navigation first, only minimize if nothing to pop
        OnBackPressedDispatcher.AddCallback(this, new BackPressHandler(this));
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

    /// <summary>
    /// Custom back handler that checks Shell navigation stack before minimizing.
    /// </summary>
    private sealed class BackPressHandler : OnBackPressedCallback
    {
        private readonly Activity _activity;

        public BackPressHandler(Activity activity)
            : base(true /* enabled */)
        {
            _activity = activity;
        }

        public override void HandleOnBackPressed()
        {
            if (Shell.Current is not null)
            {
                // Try to pop the Shell navigation stack first
                var navStack = Shell.Current.Navigation;
                if (navStack.NavigationStack.Count > 0)
                {
                    Shell.Current.GoToAsync("..");
                    return;
                }
            }

            // Nothing to pop — minimize the app instead of closing it
            _activity.MoveTaskToBack(true);
        }
    }
}

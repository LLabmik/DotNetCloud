using Android.Content;
using Android.OS;
using Android.Util;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace DotNetCloud.Client.Android.Platforms.Android;

/// <summary>
/// Android implementation of <see cref="INotificationPermissionService"/>.
/// Checks <c>POST_NOTIFICATIONS</c> runtime permission (API 33+) and opens
/// the system app notification settings page if denied.
/// </summary>
internal sealed class AndroidNotificationPermissionService : INotificationPermissionService
{
    private readonly ILogger<AndroidNotificationPermissionService> _logger;

    /// <summary>Initializes a new <see cref="AndroidNotificationPermissionService"/>.</summary>
    public AndroidNotificationPermissionService(ILogger<AndroidNotificationPermissionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool HasNotificationPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return true; // Pre-Android 13: no runtime permission needed

        // Use MAUI Permissions API which handles the cross-platform abstraction
        try
        {
            var status = Permissions.CheckStatusAsync<Permissions.PostNotifications>()
                .GetAwaiter().GetResult();
            Log.Info("DotNetCloud", $"Notification permission status: {status}");
            return status == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"Error checking notification permission: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public void OpenNotificationSettings()
    {
        if (HasNotificationPermission())
        {
            _logger.LogDebug("Notification permission already granted; skipping prompt.");
            return;
        }

        try
        {
            var context = Application.Context;

            // On Android 13+, open the app notification settings page
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                var intent = new Intent(
                    global::Android.Provider.Settings.ActionAppNotificationSettings)
                    .PutExtra(global::Android.Provider.Settings.ExtraAppPackage, context.PackageName)
                    .AddFlags(ActivityFlags.NewTask);

                context.StartActivity(intent);
                _logger.LogInformation("Opened notification settings page.");
            }
            else
            {
                // Fallback to app details settings for older Android
                var intent = new Intent(
                    global::Android.Provider.Settings.ActionApplicationDetailsSettings,
                    global::Android.Net.Uri.FromParts("package", context.PackageName, null))
                    .AddFlags(ActivityFlags.NewTask);

                context.StartActivity(intent);
                _logger.LogInformation("Opened app details settings (fallback).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open notification permission settings.");
        }
    }
}

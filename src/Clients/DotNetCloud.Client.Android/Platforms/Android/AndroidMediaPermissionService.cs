using Android.Content;
using Android.OS;
using Android.Util;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace DotNetCloud.Client.Android.Platforms.Android;

/// <summary>
/// Android implementation of <see cref="IMediaPermissionService"/>. Checks the Android 13+
/// <c>READ_MEDIA_IMAGES</c> / <c>READ_MEDIA_VIDEO</c> runtime permissions (and
/// <c>READ_EXTERNAL_STORAGE</c> on older APIs) and requests them via the MAUI Permissions
/// abstraction so the system grant dialog is shown.
/// </summary>
internal sealed class AndroidMediaPermissionService : IMediaPermissionService
{
    private readonly ILogger<AndroidMediaPermissionService> _logger;

    /// <summary>Initializes a new <see cref="AndroidMediaPermissionService"/>.</summary>
    public AndroidMediaPermissionService(ILogger<AndroidMediaPermissionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool HasMediaReadPermission()
    {
        try
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            {
                var status = Permissions.CheckStatusAsync<Permissions.StorageRead>()
                    .GetAwaiter().GetResult();
                Log.Info("DotNetCloud", $"Media read permission (pre-13) status: {status}");
                return status == PermissionStatus.Granted;
            }

            var context = Application.Context;
            // Guarded by the Tiramisu SDK check above — READ_MEDIA_* only exist on API 33+.
#pragma warning disable CA1416
            var imagesGranted = AndroidX.Core.Content.ContextCompat.CheckSelfPermission(
                context, global::Android.Manifest.Permission.ReadMediaImages)
                == global::Android.Content.PM.Permission.Granted;
            var videoGranted = AndroidX.Core.Content.ContextCompat.CheckSelfPermission(
                context, global::Android.Manifest.Permission.ReadMediaVideo)
                == global::Android.Content.PM.Permission.Granted;
#pragma warning restore CA1416
            Log.Info("DotNetCloud", $"Media read permission status: images={imagesGranted} video={videoGranted}");
            return imagesGranted && videoGranted;
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"Error checking media read permission: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> EnsureGrantedAsync(CancellationToken ct = default)
    {
        if (HasMediaReadPermission())
            return true;

        try
        {
            var status = await Permissions.RequestAsync<MediaLibraryReadPermission>().WaitAsync(ct);
            Log.Info("DotNetCloud", $"Media read permission request result: {status}");
            return status == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"Media read permission request failed: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public void OpenMediaPermissionSettings()
    {
        if (HasMediaReadPermission())
        {
            _logger.LogDebug("Media read permission already granted; skipping settings prompt.");
            return;
        }

        try
        {
            var context = Application.Context;
            var intent = new Intent(
                global::Android.Provider.Settings.ActionApplicationDetailsSettings,
                global::Android.Net.Uri.FromParts("package", context.PackageName, null))
                .AddFlags(ActivityFlags.NewTask);

            context.StartActivity(intent);
            _logger.LogInformation("Opened app details settings for media permission.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open media permission settings.");
        }
    }
}

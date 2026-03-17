using Android.Content;
using Android.OS;
using Android.Provider;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace DotNetCloud.Client.Android.Platforms.Android;

/// <summary>
/// Android implementation of <see cref="IBatteryOptimizationService"/>. Uses the system
/// <see cref="PowerManager"/> to check exemption status and launches
/// <see cref="Settings.ActionRequestIgnoreBatteryOptimizations"/> to prompt the user.
/// </summary>
internal sealed class AndroidBatteryOptimizationService : IBatteryOptimizationService
{
    private readonly ILogger<AndroidBatteryOptimizationService> _logger;

    /// <summary>Initializes a new <see cref="AndroidBatteryOptimizationService"/>.</summary>
    public AndroidBatteryOptimizationService(ILogger<AndroidBatteryOptimizationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsIgnoringBatteryOptimizations()
    {
        var context = Application.Context;
        var pm = context.GetSystemService(Context.PowerService) as PowerManager;
        if (pm is null)
            return false;

        var packageName = context.PackageName;
        if (string.IsNullOrEmpty(packageName))
            return false;

        return pm.IsIgnoringBatteryOptimizations(packageName);
    }

    /// <inheritdoc />
    public Task RequestExemptionAsync()
    {
        if (IsIgnoringBatteryOptimizations())
        {
            _logger.LogDebug("Battery optimization already exempt; skipping prompt.");
            return Task.CompletedTask;
        }

        try
        {
            var context = Platform.CurrentActivity ?? Application.Context;
            var packageName = context.PackageName;

            var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations);
            intent.SetData(global::Android.Net.Uri.Parse($"package:{packageName}"));
            intent.AddFlags(ActivityFlags.NewTask);

            context.StartActivity(intent);
            _logger.LogInformation("Launched battery optimization exemption dialog.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to launch battery optimization dialog.");
        }

        return Task.CompletedTask;
    }
}

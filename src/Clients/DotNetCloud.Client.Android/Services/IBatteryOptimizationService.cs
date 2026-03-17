namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Checks and prompts the user to exempt this app from Android battery optimization
/// so that background file sync and media upload services run reliably.
/// </summary>
public interface IBatteryOptimizationService
{
    /// <summary>Returns <c>true</c> when the app is already exempt from battery optimization.</summary>
    bool IsIgnoringBatteryOptimizations();

    /// <summary>
    /// Opens the system dialog asking the user to exempt this app from battery optimization.
    /// No-op if already exempt or if the platform does not support it.
    /// </summary>
    Task RequestExemptionAsync();
}

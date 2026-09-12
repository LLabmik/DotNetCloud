using Android.Content;

namespace DotNetCloud.Client.Android;

/// <summary>
/// TEMPORARY (2026-09-09) kill-switch for Android foreground-service promotion.
/// </summary>
/// <remarks>
/// <para>
/// Android 15/16 (API 35/36) caps <c>dataSync</c> foreground services to a rolling daily budget.
/// Once that budget is exhausted, promoting <see cref="ChatConnectionService"/> to the foreground
/// throws <c>android.app.RemoteServiceException$ForegroundServiceDidNotStopInTimeException</c> and
/// crash-kills the app in a restart loop.
/// </para>
/// <para>
/// Until the proper foreground-service-type fix lands (separate branch, operator 2026-09-09),
/// background services are started <b>without</b> foreground promotion so Android stops killing the
/// app. Trade-off while disabled: the persistent "connection" notification is not shown and the
/// services may be reclaimed when the app is backgrounded (background message delivery already goes
/// through FCM / UnifiedPush). Set <see cref="UseForegroundServices"/> back to <c>true</c> — or remove
/// this class and restore the <c>StartForegroundService</c> call sites — once the real fix ships.
/// </para>
/// </remarks>
internal static class AndroidForegroundServicePolicy
{
    /// <summary>
    /// When <c>false</c> (current temporary state), services are started as plain started services
    /// (no foreground promotion, so no <c>dataSync</c> deadline and no system kill).
    /// </summary>
    internal static readonly bool UseForegroundServices = false;

    /// <summary>
    /// Starts a background service, using foreground promotion only when the policy enables it.
    /// </summary>
    /// <param name="context">The Android context used to start the service.</param>
    /// <param name="intent">The service intent (start/stop action).</param>
    internal static void StartService(Context context, Intent intent)
    {
        if (UseForegroundServices)
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }
    }
}

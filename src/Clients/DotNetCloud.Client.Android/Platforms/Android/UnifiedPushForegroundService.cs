using Android.App;
using Android.Content;
using Android.OS;
using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android;

/// <summary>
/// The service a UnifiedPush distributor may bind to in order to raise this app to foreground
/// importance for a few seconds while it hands over a message.
/// </summary>
/// <remarks>
/// This is the specification's sanctioned wake-up path and is deliberately <b>not</b> a
/// foreground service: it never calls <c>StartForeground</c>, shows no notification and consumes
/// none of the Android 15/16 <c>dataSync</c> budget that was removed from this app permanently.
/// </remarks>
[Service(Name = "net.dotnetcloud.client.UnifiedPushForegroundService", Exported = true)]
[IntentFilter([UnifiedPushProtocol.ActionRaiseToForeground])]
internal sealed class UnifiedPushForegroundService : Service
{
    private readonly IBinder _binder = new LocalBinder();

    /// <inheritdoc />
    public override IBinder? OnBind(Intent? intent) => _binder;

    /// <inheritdoc />
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId) =>
        StartCommandResult.NotSticky;

    /// <summary>The binding channel; the specification requires no calls on it.</summary>
    private sealed class LocalBinder : Binder
    {
    }
}

using Android.App;
using Android.Content;
using Android.OS;
using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Starts the UnifiedPush link activity (<c>unifiedpush://link</c>) so the user can pick a
/// distributor with the system UI, and reports the chosen package back to the caller.
/// </summary>
/// <remarks>
/// The specification requires the link to be started <i>for a result</i>: the distributor returns
/// an immutable <c>pi</c> pending intent whose creator identifies it. Applications must not rely
/// on the default app opening the link, so this dedicated activity exists to own that exchange.
/// </remarks>
[Activity(
    Name = "net.dotnetcloud.client.DistributorLinkActivity",
    Exported = false,
    NoHistory = true,
    Theme = "@android:style/Theme.Translucent.NoTitleBar")]
internal sealed class DistributorLinkActivity : Activity
{
    private const string LinkUri = "unifiedpush://link";
    private const int LinkRequestCode = 0x5550;

    private static TaskCompletionSource<string?>? _pending;

    /// <summary>
    /// Opens the system distributor picker and returns the chosen package name.
    /// </summary>
    /// <param name="host">Activity used to launch the picker.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The distributor package name, or null when the user cancelled or none is installed.</returns>
    public static async Task<string?> SelectAsync(Activity host, CancellationToken ct = default)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = completion;

        try
        {
            host.StartActivity(new Intent(host, typeof(DistributorLinkActivity)));
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("DotNetCloud", $"Could not open the distributor picker: {ex.Message}");
            _pending = null;
            return null;
        }

        var timeout = Task.Delay(TimeSpan.FromMinutes(2), ct);
        var finished = await Task.WhenAny(completion.Task, timeout).ConfigureAwait(false);
        if (finished != completion.Task)
        {
            _pending = null;
            return null;
        }

        return await completion.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        try
        {
            var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(LinkUri));
            StartActivityForResult(intent, LinkRequestCode);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("DotNetCloud", $"No UnifiedPush distributor answered {LinkUri}: {ex.Message}");
            Complete(null);
        }
    }

    /// <inheritdoc />
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode != LinkRequestCode)
            return;

        Complete(resultCode == Result.Ok ? ExtractDistributorPackage(data) : null);
    }

    private static string? ExtractDistributorPackage(Intent? data)
    {
        if (data is null)
            return null;

        try
        {
#pragma warning disable CS0618, CA1422 // The class-typed overload requires API 33; minSdk here is 26.
            var pendingIntent = data.GetParcelableExtra(UnifiedPushProtocol.ExtraPendingIntent) as PendingIntent;
#pragma warning restore CS0618, CA1422

            if (pendingIntent is null)
                return null;

            // Sending it first is what makes the creator identity available.
            pendingIntent.Send();

            var package = pendingIntent.CreatorPackage;
            if (!string.IsNullOrWhiteSpace(package))
                return package;

            global::Android.Util.Log.Warn("DotNetCloud", "The distributor returned a pending intent with no creator package.");
            return null;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("DotNetCloud", $"Reading the distributor identity failed: {ex.Message}");
            return null;
        }
    }

    private void Complete(string? package)
    {
        _pending?.TrySetResult(package);
        _pending = null;
        Finish();
    }
}

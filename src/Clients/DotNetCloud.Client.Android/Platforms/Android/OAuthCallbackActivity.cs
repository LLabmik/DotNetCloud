using Android.App;
using Android.Content;
using Android.OS;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Transparent activity that receives the OAuth2 redirect URI and forwards the
/// callback URL to the waiting <see cref="DotNetCloud.Client.Android.Auth.MauiOAuth2Service"/> via a static task.
/// Registered via activity attributes for <c>net.dotnetcloud.client://oauth2redirect</c>.
/// </summary>
[Activity(Label = "@string/app_name", NoHistory = true, LaunchMode = global::Android.Content.PM.LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "net.dotnetcloud.client",
    DataHost = "oauth2redirect")]
public class OAuthCallbackActivity : global::Android.App.Activity
{
    private static TaskCompletionSource<string>? _tcs;

    /// <summary>Waits for the OAuth2 redirect callback and returns the full callback URL.</summary>
    public static Task<string> WaitForCallbackAsync(CancellationToken ct)
    {
        _tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _tcs.TrySetCanceled(ct));
        return _tcs.Task;
    }

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleIntent(Intent);
    }

    /// <inheritdoc />
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleIntent(intent);
    }

    private void HandleIntent(Intent? intent)
    {
        var callbackUrl = intent?.Data?.ToString();
        if (callbackUrl is not null)
            _tcs?.TrySetResult(callbackUrl);

        Finish();
    }
}

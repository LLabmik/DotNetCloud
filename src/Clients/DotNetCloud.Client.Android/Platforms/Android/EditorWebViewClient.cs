using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace DotNetCloud.Client.Android.Platforms;

/// <summary>
/// WebView client for the Collabora editor that also reports main-frame HTTP error responses.
/// </summary>
/// <remarks>
/// MAUI's own client only reports transport-level failures (<c>OnReceivedError</c>), so an HTTP error
/// from the Collabora reverse proxy — for example <c>502</c> while coolwsd is unavailable — would
/// leave the page showing Chromium's raw error document with no way back into the app's retry UI.
/// </remarks>
internal sealed class EditorWebViewClient : MauiWebViewClient
{
    private readonly Action<int> _onHttpError;

    /// <summary>Initializes a new <see cref="EditorWebViewClient"/>.</summary>
    /// <param name="handler">The WebView handler this client belongs to.</param>
    /// <param name="onHttpError">
    /// Invoked with the HTTP status code when the main document fails with an error response.
    /// </param>
    public EditorWebViewClient(WebViewHandler handler, Action<int> onHttpError)
        : base(handler)
    {
        _onHttpError = onHttpError;
    }

    /// <inheritdoc />
    public override void OnReceivedHttpError(
        global::Android.Webkit.WebView? view,
        global::Android.Webkit.IWebResourceRequest? request,
        global::Android.Webkit.WebResourceResponse? errorResponse)
    {
        // Sub-resource failures (images, fonts, websocket upgrades) are not a failed editor load.
        if (request?.IsForMainFrame == true)
        {
            var statusCode = errorResponse?.StatusCode ?? 0;
            if (statusCode >= 400)
                _onHttpError(statusCode);
        }

        base.OnReceivedHttpError(view, request, errorResponse);
    }
}

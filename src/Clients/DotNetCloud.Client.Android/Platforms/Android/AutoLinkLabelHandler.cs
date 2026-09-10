using DotNetCloud.Client.Android.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace DotNetCloud.Client.Android.Platforms.Android;

/// <summary>
/// Android handler for <see cref="AutoLinkLabel"/>: enables Android's built-in URL
/// auto-linking so http(s) URLs in the label text become tappable links that open
/// in the system browser when tapped.
/// </summary>
public sealed class AutoLinkLabelHandler : LabelHandler
{
    /// <inheritdoc />
    protected override MauiTextView CreatePlatformView()
    {
        // LabelHandler's base returns AppCompatTextView, but the concrete view is a MauiTextView.
        var textView = (MauiTextView)base.CreatePlatformView();

        // Auto-link web URLs (https://, http://, www.) and make them tappable.
        textView.AutoLinkMask = global::Android.Text.Util.MatchOptions.WebUrls;
        textView.LinksClickable = true;
        textView.MovementMethod = global::Android.Text.Method.LinkMovementMethod.Instance;

        return textView;
    }
}

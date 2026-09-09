namespace DotNetCloud.Client.Android.Controls;

/// <summary>
/// A <see cref="Label"/> whose text renders http(s) URLs as native, tappable links.
/// URLs are shown as colored, underlined links; tapping one opens it in the system browser.
/// </summary>
public sealed class AutoLinkLabel : Label
{
    /// <summary>Backing store for <see cref="LinkColor"/>.</summary>
    public static readonly BindableProperty LinkColorProperty = BindableProperty.Create(
        nameof(LinkColor),
        typeof(Color),
        typeof(AutoLinkLabel),
        Color.FromArgb("#7DD3FC"),
        propertyChanged: static (bindable, _, _) =>
        {
            if (bindable is AutoLinkLabel label)
            {
                label.UpdateLinkColor();
            }
        });

    /// <summary>Initializes a new instance of the <see cref="AutoLinkLabel"/> class.</summary>
    public AutoLinkLabel()
    {
        // Re-apply the link color whenever the platform view is (re)created — e.g. when a
        // recycled message row re-binds to a different sender's message via a DataTrigger.
        HandlerChanged += (_, _) => UpdateLinkColor();
    }

    /// <summary>Color used for the auto-detected URL links.</summary>
    public Color LinkColor
    {
        get => (Color)GetValue(LinkColorProperty);
        set => SetValue(LinkColorProperty, value);
    }

    private void UpdateLinkColor()
    {
#if ANDROID
        if (Handler?.PlatformView is global::Android.Widget.TextView textView && LinkColor is { } color)
        {
            textView.SetLinkTextColor(global::Android.Content.Res.ColorStateList.ValueOf(ToNativeColor(color)));
        }
#endif
    }

#if ANDROID
    private static global::Android.Graphics.Color ToNativeColor(Color color) =>
        new(
            (int)(color.Red * 255f),
            (int)(color.Green * 255f),
            (int)(color.Blue * 255f));
#endif
}

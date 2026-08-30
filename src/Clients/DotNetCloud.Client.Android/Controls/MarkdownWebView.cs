using System.Globalization;
using DotNetCloud.Client.Android.Ai;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace DotNetCloud.Client.Android.Controls;

/// <summary>
/// A <see cref="WebView"/> that renders Markdown as styled, auto-sized HTML. Web links open in the
/// system browser; all other navigation (including script/data/file schemes) is blocked.
/// </summary>
public sealed class MarkdownWebView : WebView
{
    private const double InitialHeight = 88;
    private const double HeightPadding = 20;
    private const string MeasureScript =
        "(function(){return Math.max(document.body.scrollHeight,document.body.offsetHeight,document.documentElement.scrollHeight);})()";

    private bool _measuring;

    /// <summary>Creates a new <see cref="MarkdownWebView"/>.</summary>
    public MarkdownWebView()
    {
        HeightRequest = InitialHeight;
        Navigating += OnNavigating;
        Navigated += OnNavigated;
    }

    /// <summary>Backing store for <see cref="Markdown"/>.</summary>
    public static readonly BindableProperty MarkdownProperty = BindableProperty.Create(
        nameof(Markdown), typeof(string), typeof(MarkdownWebView), string.Empty, propertyChanged: OnMarkdownChanged);

    /// <summary>The raw Markdown to render.</summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (MarkdownWebView)bindable;
        var html = MarkdownHtmlFormatter.ToHtmlDocument(newValue as string);
        control.Source = new HtmlWebViewSource { Html = html };
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        var url = e.Url;
        if (string.IsNullOrEmpty(url))
            return;

        // Open web links in the system browser instead of navigating the WebView.
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            _ = OpenInBrowserAsync(url);
            return;
        }

        // Block script/file/data-exfiltration schemes. The inline HTML load uses about:blank,
        // which is allowed by the default branch.
        if (url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("content:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("intent:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
        }
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (_measuring)
            return;

        _measuring = true;
        try
        {
            var result = await EvaluateJavaScriptAsync(MeasureScript);
            if (double.TryParse(result, NumberStyles.Float, CultureInfo.InvariantCulture, out var height) && height > 0)
            {
                var target = height + HeightPadding;
                if (Math.Abs(HeightRequest - target) > 1)
                    HeightRequest = target;
            }
        }
        catch (Exception)
        {
            // Measurement is best-effort; keep the current height.
        }
        finally
        {
            _measuring = false;
        }
    }

    private static async Task OpenInBrowserAsync(string url)
    {
        try
        {
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception)
        {
            // Opening externally is best-effort.
        }
    }
}

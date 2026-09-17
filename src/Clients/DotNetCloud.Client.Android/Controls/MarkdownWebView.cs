using System.Diagnostics;
using System.Globalization;
using DotNetCloud.Client.Android.Ai;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace DotNetCloud.Client.Android.Controls;

/// <summary>
/// Renders Markdown as styled, auto-sized HTML in an embedded <see cref="WebView"/>, with a built-in
/// progress spinner: a WebView paints a blank surface while it converts the generated document, and an
/// empty-looking bubble is easily mistaken for a failed reply. Web links open in the system browser;
/// all other navigation (including script/data/file schemes) is blocked.
/// </summary>
public sealed class MarkdownWebView : ContentView
{
    private const double InitialHeight = 88;
    private const double HeightPadding = 20;
    private const double SpinnerSize = 22;

    /// <summary>Spinner colour — readable on both the assistant (dark) and user (blue) bubbles.</summary>
    private static readonly Color SpinnerColor = Color.FromArgb("#F1F5F9");

    /// <summary>How many times the rendered document is probed before the spinner is released.</summary>
    private const int MaxProbeAttempts = 24;

    /// <summary>Delay before the first probe of the rendered document.</summary>
    private static readonly TimeSpan InitialProbeDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>Upper bound for the growing delay between probes.</summary>
    private static readonly TimeSpan MaxProbeDelay = TimeSpan.FromMilliseconds(800);

    /// <summary>
    /// How long the spinner stays up at minimum, so a fast render reads as progress instead of a
    /// flicker.
    /// </summary>
    private static readonly TimeSpan MinimumSpinnerDuration = TimeSpan.FromMilliseconds(350);

    private readonly WebView _webView;
    private readonly ActivityIndicator _spinner;
    private CancellationTokenSource? _renderCts;

    /// <summary>Creates a new <see cref="MarkdownWebView"/>.</summary>
    public MarkdownWebView()
    {
        _spinner = new ActivityIndicator
        {
            Color = SpinnerColor,
            HeightRequest = SpinnerSize,
            WidthRequest = SpinnerSize,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            IsRunning = false,
            IsVisible = false,
        };

        _webView = new WebView
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        _webView.Navigating += OnNavigating;

        Content = new Grid { Children = { _webView, _spinner } };
        HeightRequest = InitialHeight;

        // Keep the WebView surface in step with the control's background so a document that has not
        // painted yet never flashes a light rectangle.
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BackgroundColor))
                _webView.BackgroundColor = BackgroundColor;
        };
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

    /// <summary>
    /// Re-renders the current Markdown. Call this after the control becomes visible: a render that
    /// happened while the control was hidden can neither paint nor be measured, so the spinner would
    /// never clear and the height would stay at its placeholder.
    /// </summary>
    public void Refresh() => Render(Markdown);

    /// <summary>Backing store key for the read-only <see cref="IsRendering"/> property.</summary>
    private static readonly BindablePropertyKey IsRenderingPropertyKey = BindableProperty.CreateReadOnly(
        nameof(IsRendering), typeof(bool), typeof(MarkdownWebView), false, propertyChanged: OnIsRenderingChanged);

    /// <summary>Bindable backing for <see cref="IsRendering"/>.</summary>
    public static readonly BindableProperty IsRenderingProperty = IsRenderingPropertyKey.BindableProperty;

    /// <summary>
    /// <see langword="true"/> from the moment block-level Markdown is handed to the control until the
    /// generated document has been painted, i.e. while the content would otherwise just be a blank
    /// surface. The control shows its own spinner from this state; hosts can bind it as well.
    /// Inline-only Markdown renders on the host's lightweight path and never sets this flag.
    /// </summary>
    public bool IsRendering
    {
        get => (bool)GetValue(IsRenderingProperty);
        private set => SetValue(IsRenderingPropertyKey, value);
    }

    private static void OnIsRenderingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (MarkdownWebView)bindable;
        var rendering = (bool)newValue;
        control._spinner.IsVisible = rendering;
        control._spinner.IsRunning = rendering;
    }

    private static void OnMarkdownChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((MarkdownWebView)bindable).Render(newValue as string);

    /// <summary>
    /// Loads <paramref name="markdown"/> into the WebView and, on the HTML path, keeps the spinner up
    /// until the rendered document is on screen.
    /// </summary>
    private void Render(string? markdown)
    {
        _renderCts?.Cancel();
        _renderCts?.Dispose();
        _renderCts = null;

        // Only Markdown that needs the HTML renderer pays the WebView start-up cost and therefore
        // only that content shows the spinner.
        var needsHtmlRendering = MarkdownHtmlFormatter.NeedsHtmlRendering(markdown);
        IsRendering = needsHtmlRendering;
        HeightRequest = InitialHeight;

        // The token is embedded in the document so a probe can tell this render's document apart from
        // the previous one (recycled WebViews) and from the blank bootstrap page.
        var documentToken = Guid.NewGuid().ToString("N");
        _webView.Source = new HtmlWebViewSource
        {
            Html = MarkdownHtmlFormatter.ToHtmlDocument(markdown, documentToken)
        };

        if (!needsHtmlRendering)
            return;

        var cts = new CancellationTokenSource();
        _renderCts = cts;
        _ = ProbeRenderedDocumentAsync(documentToken, cts.Token);
    }

    /// <summary>
    /// Waits for the rendered document to be both painted and measurable, sizes the control to its
    /// content, then clears <see cref="IsRendering"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="WebView.Navigated"/> cannot be used here: inline HTML is loaded against
    /// <c>about:blank</c> and MAUI deliberately suppresses the navigated event for blank loads (only
    /// the blocked <c>file:///android_asset/</c> bootstrap shows up, and that fires before the
    /// document exists). The document is probed instead, which also covers the gap between layout and
    /// the WebView's first painted frame.
    /// </remarks>
    private async Task ProbeRenderedDocumentAsync(string documentToken, CancellationToken ct)
    {
        var startedTicks = Stopwatch.GetTimestamp();
        var delay = InitialProbeDelay;

        for (var attempt = 0; attempt < MaxProbeAttempts && !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (ct.IsCancellationRequested)
                return;

            var (painted, height) = await TryProbeDocumentAsync(documentToken);

            if (height > 0)
            {
                var target = height + HeightPadding;
                if (Math.Abs(HeightRequest - target) > 1)
                    HeightRequest = target;
            }

            if (painted && height > 0)
            {
                var remaining = MinimumSpinnerDuration - Stopwatch.GetElapsedTime(startedTicks);
                if (remaining > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(remaining, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }

                if (!ct.IsCancellationRequested)
                    IsRendering = false;

                return;
            }

            delay = TimeSpan.FromMilliseconds(
                Math.Min(MaxProbeDelay.TotalMilliseconds, delay.TotalMilliseconds * 1.6));
        }

        // The document never reported itself painted (failed load, or a control without a platform
        // handler such as a unit test) — never leave a spinner up forever.
        if (!ct.IsCancellationRequested)
            IsRendering = false;
    }

    /// <summary>
    /// One round trip that reports whether this render's document has been painted and how tall it
    /// is, as <c>"painted:height"</c>. Both are read from the same document — an older or blank
    /// document answers <c>"0:0"</c>.
    /// </summary>
    private async Task<(bool Painted, double Height)> TryProbeDocumentAsync(string documentToken)
    {
        if (_webView.Handler is null)
            return (false, 0);

        try
        {
            var result = await MainThread.InvokeOnMainThreadAsync(
                () => _webView.EvaluateJavaScriptAsync(BuildProbeScript(documentToken)));

            var separator = result?.IndexOf(':') ?? -1;
            if (separator > 0)
            {
                var painted = result![..separator] == "1";
                var height = double.TryParse(
                    result[(separator + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0;

                return (painted, height);
            }
        }
        catch (Exception)
        {
            // Not probeable yet (handler still connecting, renderer not ready) — retry later.
        }

        return (false, 0);
    }

    /// <summary>
    /// Builds the probe for <paramref name="documentToken"/>: it reports <c>"painted:height"</c>, where
    /// <c>painted</c> flips once the renderer has committed its first frame of that document (two
    /// animation frames — the standard "this content is now on screen" marker) and <c>height</c> is 0
    /// until the document has been laid out. The paint listener is armed inside the matching document
    /// only, so a stale document can never report itself as painted.
    /// </summary>
    private static string BuildProbeScript(string documentToken) =>
        $$"""
        (function(){
          var w = window, d = document, body = d.body, id = '{{documentToken}}';
          if (!body || body.getAttribute('data-dnc-render') !== id) { return '0:0'; }
          var painter = w.__dncMarkdownPainter;
          if (!painter || painter.id !== id) {
            painter = { id: id, painted: false };
            w.__dncMarkdownPainter = painter;
            var mark = function(){ painter.painted = true; };
            if (typeof w.requestAnimationFrame === 'function') {
              w.requestAnimationFrame(function(){ w.requestAnimationFrame(mark); });
            } else { mark(); }
          }
          var height = 0;
          try {
            height = Math.max(body.scrollHeight, body.offsetHeight, d.documentElement.scrollHeight);
          } catch (e) { height = 0; }
          return (painter.painted ? '1' : '0') + ':' + height;
        })()
        """;

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

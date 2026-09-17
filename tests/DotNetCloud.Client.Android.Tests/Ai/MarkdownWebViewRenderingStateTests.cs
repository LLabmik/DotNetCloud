using DotNetCloud.Client.Android.Controls;
using Microsoft.Maui.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Android.Tests.Ai;

/// <summary>
/// Tests for the rendering state <see cref="MarkdownWebView"/> drives its spinner from. Without it a
/// freshly finished AI answer showed an empty bubble while the WebView converted the Markdown to HTML.
/// </summary>
[TestClass]
public sealed class MarkdownWebViewRenderingStateTests
{
    private static ActivityIndicator SpinnerOf(MarkdownWebView control) =>
        VisualTree(control).OfType<ActivityIndicator>().Single();

    private static Label RawTextOf(MarkdownWebView control) =>
        VisualTree(control).OfType<Label>().Single(l => l.FormattedText is not null);

    private static IEnumerable<Element> VisualTree(Element root)
    {
        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren())
        {
            if (child is not Element element)
                continue;

            yield return element;

            foreach (var descendant in VisualTree(element))
                yield return descendant;
        }
    }

    [TestMethod]
    public void IsRendering_FreshControl_IsFalse()
    {
        var control = new MarkdownWebView();

        Assert.IsFalse(control.IsRendering);
    }

    [TestMethod]
    [DataRow("# Heading")]
    [DataRow("- one\n- two")]
    [DataRow("1. one\n2. two")]
    [DataRow("> quoted")]
    [DataRow("| A | B |\n|---|---|\n| 1 | 2 |")]
    [DataRow("```csharp\nvar x = 1;\n```")]
    public void Markdown_BlockContent_ReportsRendering(string markdown)
    {
        var control = new MarkdownWebView();

        control.Markdown = markdown;

        Assert.IsTrue(control.IsRendering);
    }

    [TestMethod]
    [DataRow("plain text")]
    [DataRow("**bold** and `code` and [a](https://example.com)")]
    [DataRow("")]
    public void Markdown_ContentRenderedByTheLightweightPath_DoesNotReportRendering(string markdown)
    {
        var control = new MarkdownWebView();

        control.Markdown = markdown;

        Assert.IsFalse(control.IsRendering);
    }

    [TestMethod]
    public void Markdown_BlockContent_ShowsRawTextAndRenderingHintInsteadOfAnEmptyBubble()
    {
        var control = new MarkdownWebView();
        var spinner = SpinnerOf(control);
        var rawText = RawTextOf(control);

        Assert.IsFalse(spinner.IsVisible, "The rendering hint must start hidden.");
        Assert.IsFalse(rawText.IsVisible, "The raw-text fallback must start hidden.");

        control.Markdown = "- item";

        Assert.IsTrue(spinner.IsVisible, "The rendering hint must cover the WebView conversion.");
        Assert.IsTrue(spinner.IsRunning);
        Assert.IsTrue(rawText.IsVisible, "The bubble must show the raw text while the HTML is converted.");
        Assert.IsTrue(
            rawText.FormattedText!.Spans.Any(s => (s.Text ?? string.Empty).Contains("item", StringComparison.Ordinal)),
            "The fallback must contain the answer text, so the bubble is never blank.");
    }

    [TestMethod]
    public void Markdown_ContentRenderedByTheLightweightPath_LeavesTheHintHidden()
    {
        var control = new MarkdownWebView();

        control.Markdown = "plain text";

        Assert.IsFalse(SpinnerOf(control).IsVisible);
        Assert.IsFalse(RawTextOf(control).IsVisible);
    }

    [TestMethod]
    public void Content_HasWebViewUnderTheSpinner()
    {
        var control = new MarkdownWebView();

        control.Markdown = "- item";

        var webView = ((Grid)control.Content).Children.OfType<WebView>().Single();
        var source = webView.Source as HtmlWebViewSource;

        Assert.IsNotNull(source, "Expected the control to hand its Markdown to the WebView as HTML.");
        Assert.IsTrue(source.Html!.Contains("<body", StringComparison.Ordinal));
    }
}

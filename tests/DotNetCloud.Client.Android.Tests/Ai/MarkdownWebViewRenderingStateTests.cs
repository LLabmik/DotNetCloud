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
        ((Grid)control.Content).Children.OfType<ActivityIndicator>().Single();

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
    public void Markdown_BlockContent_ShowsAndSpinsTheBuiltInSpinner()
    {
        var control = new MarkdownWebView();
        var spinner = SpinnerOf(control);

        Assert.IsFalse(spinner.IsVisible, "The spinner must start hidden.");
        Assert.IsFalse(spinner.IsRunning);

        control.Markdown = "- item";

        Assert.IsTrue(spinner.IsVisible, "The spinner must cover the WebView conversion.");
        Assert.IsTrue(spinner.IsRunning);
    }

    [TestMethod]
    public void Markdown_ContentRenderedByTheLightweightPath_LeavesTheSpinnerHidden()
    {
        var control = new MarkdownWebView();

        control.Markdown = "plain text";

        var spinner = SpinnerOf(control);
        Assert.IsFalse(spinner.IsVisible);
        Assert.IsFalse(spinner.IsRunning);
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

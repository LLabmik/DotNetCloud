using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="LinkPreviewService"/>: URL detection and HTML metadata extraction.
/// </summary>
[TestClass]
public class LinkPreviewServiceTests
{
    private static LinkPreviewService CreateService() =>
        new(new SafeUrlFetcher(NullLogger<SafeUrlFetcher>.Instance), NullLogger<LinkPreviewService>.Instance);

    [TestMethod]
    public void FindFirstUrl_WithBareUrl_ReturnsUri()
    {
        var svc = CreateService();
        var uri = svc.FindFirstUrl("Check out https://example.com/page for details");
        Assert.IsNotNull(uri);
        Assert.AreEqual("https://example.com/page", uri!.ToString());
    }

    [TestMethod]
    public void FindFirstUrl_WithoutUrl_ReturnsNull()
    {
        var svc = CreateService();
        Assert.IsNull(svc.FindFirstUrl("Just some text, no links here."));
        Assert.IsNull(svc.FindFirstUrl(string.Empty));
        Assert.IsNull(svc.FindFirstUrl(null));
    }

    [TestMethod]
    public void FindFirstUrl_WithMultipleUrls_ReturnsFirst()
    {
        var svc = CreateService();
        var uri = svc.FindFirstUrl("First https://one.example.com/a then http://two.example.com/x");
        Assert.IsNotNull(uri);
        Assert.AreEqual("https://one.example.com/a", uri!.AbsoluteUri);
    }

    [TestMethod]
    public void FindFirstUrl_WithTrailingPunctuation_StripsIt()
    {
        var svc = CreateService();
        var uri = svc.FindFirstUrl("See https://example.com/page, and also this one!");
        Assert.IsNotNull(uri);
        Assert.AreEqual("https://example.com/page", uri!.ToString());
    }

    [TestMethod]
    public void FindFirstUrl_InsideFencedCodeBlock_ReturnsNull()
    {
        var svc = CreateService();
        const string content = "Some text\n```\nhttps://example.com/in-code\n```\nend";
        Assert.IsNull(svc.FindFirstUrl(content));
    }

    [TestMethod]
    public void FindFirstUrl_InsideIndentedCodeBlock_ReturnsNull()
    {
        var svc = CreateService();
        const string content = "Before\n    https://example.com/indented\nAfter";
        Assert.IsNull(svc.FindFirstUrl(content));
    }

    [TestMethod]
    public void FindFirstUrl_WithMediaUrl_SkipsToNextLinkableUrl()
    {
        var svc = CreateService();
        var uri = svc.FindFirstUrl("Image https://cdn.example.com/pic.png and page https://example.com/read");
        Assert.IsNotNull(uri);
        Assert.AreEqual("https://example.com/read", uri!.ToString());
    }

    [TestMethod]
    public void FindFirstUrl_OnlyNonHttpSchemes_ReturnsNull()
    {
        var svc = CreateService();
        Assert.IsNull(svc.FindFirstUrl("ftp://example.com/file"));
    }

    [TestMethod]
    public void ParsePreviewHtml_ExtractsOpenGraphMetadata()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
              <head>
                <meta property="og:title" content="Open Graph Title" />
                <meta property="og:description" content="Open Graph Description" />
                <meta property="og:image" content="/images/hero.png" />
                <meta property="og:site_name" content="Example Site" />
                <link rel="icon" href="/favicon.ico" />
                <title>HTML Title (should not win)</title>
              </head>
              <body></body>
            </html>
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(html));
        var result = LinkPreviewService.ParsePreviewHtml(stream, new Uri("https://example.com/article"), null);

        Assert.IsNotNull(result);
        Assert.AreEqual("Open Graph Title", result!.Title);
        Assert.AreEqual("Open Graph Description", result.Description);
        Assert.AreEqual("https://example.com/images/hero.png", result.ImageUrl);
        Assert.AreEqual("Example Site", result.SiteName);
    }

    [TestMethod]
    public void ParsePreviewHtml_FallsBackToHtmlTitleAndMetaDescription()
    {
        const string html = """
            <html>
              <head>
                <title>   Plain HTML Title  </title>
                <meta name="description" content="Plain description" />
              </head>
              <body></body>
            </html>
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(html));
        var result = LinkPreviewService.ParsePreviewHtml(stream, new Uri("https://example.com/page"), "https://example.com/page");

        Assert.IsNotNull(result);
        Assert.AreEqual("Plain HTML Title", result!.Title);
        Assert.AreEqual("Plain description", result.Description);
        Assert.AreEqual("https://example.com/favicon.ico", result.FaviconUrl);
    }

    [TestMethod]
    public void ParsePreviewHtml_UsesFinalUriAsBaseForRelativeUrls()
    {
        const string html = """
            <html>
              <head>
                <meta property="og:title" content="Redirected" />
                <meta property="og:image" content="img.jpg" />
              </head>
              <body></body>
            </html>
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(html));
        var result = LinkPreviewService.ParsePreviewHtml(
            stream, new Uri("https://short.example/x"), "https://long.example/path/landed");

        Assert.IsNotNull(result);
        Assert.AreEqual("https://long.example/path/img.jpg", result!.ImageUrl);
    }

    [TestMethod]
    public void ParsePreviewHtml_WithNoMetadata_ReturnsNull()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<html><head></head><body></body></html>"));
        var result = LinkPreviewService.ParsePreviewHtml(stream, new Uri("https://example.com/blank"), null);
        Assert.IsNull(result);
    }
}

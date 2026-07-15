using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Tests.Notes;

[TestClass]
public sealed class NotesViewModelTests
{
    [TestMethod]
    public void WrapHtmlWithDarkTheme_WrapsInFullHtmlDocument()
    {
        var input = "<p>Hello world</p>";
        var result = NotesViewModel.WrapHtmlWithDarkTheme(input);

        Assert.IsTrue(result.StartsWith("<!DOCTYPE html>"), "Should start with DOCTYPE");
        Assert.IsTrue(result.Contains("<html>"), "Should contain html tag");
        Assert.IsTrue(result.Contains("<head>"), "Should contain head tag");
        Assert.IsTrue(result.Contains("<style>"), "Should contain style tag");
        Assert.IsTrue(result.Contains("</style>"), "Should close style tag");
        Assert.IsTrue(result.Contains("<body>"), "Should contain body tag");
        Assert.IsTrue(result.Contains("</body>"), "Should close body tag");
        Assert.IsTrue(result.Contains("</html>"), "Should close html tag");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_IncludesInputHtml()
    {
        var input = "<p>Hello world</p>";
        var result = NotesViewModel.WrapHtmlWithDarkTheme(input);

        Assert.IsTrue(result.Contains(input), "Should contain original HTML");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasDarkThemeCss()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("#0F172A"), "Should have dark background color");
        Assert.IsTrue(result.Contains("#E2E8F0"), "Should have light text color");
        Assert.IsTrue(result.Contains("!important"), "Should use !important overrides");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HandlesEmptyHtml()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme(string.Empty);

        Assert.IsTrue(result.Contains("<body></body>"), "Should handle empty body");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasColorSchemeDark()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("color-scheme: dark"), "Should have dark color-scheme");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasViewportMeta()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("viewport"), "Should have viewport meta tag");
        Assert.IsTrue(result.Contains("width=device-width"), "Should have responsive viewport");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasLinkStyles()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("a {"), "Should have link styles");
        Assert.IsTrue(result.Contains("#38BDF8"), "Should have light blue link color");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasCodeStyles()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("code {"), "Should have code styles");
        Assert.IsTrue(result.Contains("#F472B6"), "Should have pink code color");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasTableStyles()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("table {"), "Should have table styles");
        Assert.IsTrue(result.Contains("border-collapse"), "Should collapse table borders");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasParagraphStyle()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("p {"), "Should have paragraph style");
        Assert.IsTrue(result.Contains("#E2E8F0"), "Should have light paragraph text");
    }
}

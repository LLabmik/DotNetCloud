using DotNetCloud.Client.Android.Converters;
using Microsoft.Maui.Controls;

namespace DotNetCloud.Client.Android.Tests.Converters;

[TestClass]
public sealed class MarkdownConverterTests
{
    // ── Plain text ──────────────────────────────────────────────────

    [TestMethod]
    public void Format_NullOrEmpty_ReturnsEmptyFormattedString()
    {
        Assert.AreEqual(0, MarkdownFormatter.Format(null).Spans.Count);
        Assert.AreEqual(0, MarkdownFormatter.Format("").Spans.Count);
    }

    [TestMethod]
    public void Format_PlainText_ReturnsSingleSpan()
    {
        var result = MarkdownFormatter.Format("Hello world");
        Assert.AreEqual(1, result.Spans.Count);
        Assert.AreEqual("Hello world", result.Spans[0].Text);
    }

    // ── Bold ────────────────────────────────────────────────────────

    [TestMethod]
    public void Format_Bold_ProducesBoldSpan()
    {
        var result = MarkdownFormatter.Format("a **bold** word");
        Assert.AreEqual(3, result.Spans.Count);
        Assert.AreEqual("bold", result.Spans[1].Text);
        Assert.AreEqual(FontAttributes.Bold, result.Spans[1].FontAttributes);
    }

    // ── Italic ──────────────────────────────────────────────────────

    [TestMethod]
    public void Format_Italic_ProducesItalicSpan()
    {
        var result = MarkdownFormatter.Format("*emphasis*");
        Assert.AreEqual(1, result.Spans.Count);
        Assert.AreEqual("emphasis", result.Spans[0].Text);
        Assert.AreEqual(FontAttributes.Italic, result.Spans[0].FontAttributes);
    }

    // ── Inline code ─────────────────────────────────────────────────

    [TestMethod]
    public void Format_InlineCode_ProducesMonospaceSpan()
    {
        var result = MarkdownFormatter.Format("run `dotnet build` now");
        Assert.AreEqual(3, result.Spans.Count);
        Assert.AreEqual("dotnet build", result.Spans[1].Text);
        Assert.AreEqual("monospace", result.Spans[1].FontFamily);
    }

    // ── Links ───────────────────────────────────────────────────────

    [TestMethod]
    public void Format_Link_ProducesUnderlinedColoredSpan()
    {
        var result = MarkdownFormatter.Format("[docs](https://example.com/docs)");
        Assert.AreEqual(1, result.Spans.Count);
        Assert.AreEqual("docs", result.Spans[0].Text);
        Assert.AreEqual(TextDecorations.Underline, result.Spans[0].TextDecorations);
        Assert.IsNotNull(result.Spans[0].TextColor);
    }

    // ── Fenced code blocks ──────────────────────────────────────────

    [TestMethod]
    public void Format_FencedCodeBlock_ProducesMonospaceSpan()
    {
        var result = MarkdownFormatter.Format("```csharp\nvar x = 1;\n```");
        Assert.AreEqual(1, result.Spans.Count);
        Assert.AreEqual("var x = 1;", result.Spans[0].Text);
        Assert.AreEqual("monospace", result.Spans[0].FontFamily);
    }

    [TestMethod]
    public void Format_FencedCodeBlock_IgnoresMarkdownInside()
    {
        // **bold** inside a code fence must NOT become bold.
        var result = MarkdownFormatter.Format("```\n**not bold**\n```");
        Assert.AreEqual(1, result.Spans.Count);
        Assert.AreEqual("**not bold**", result.Spans[0].Text);
        Assert.AreNotEqual(FontAttributes.Bold, result.Spans[0].FontAttributes);
    }

    // ── Converter wrapper ───────────────────────────────────────────

    [TestMethod]
    public void Converter_Convert_ReturnsFormattedString()
    {
        var converter = new MarkdownConverter();
        var value = converter.Convert("**hi**", typeof(FormattedString), null, System.Globalization.CultureInfo.InvariantCulture);
        var fs = Assert.IsInstanceOfType<FormattedString>(value);
        Assert.AreEqual("hi", fs.Spans[0].Text);
    }
}

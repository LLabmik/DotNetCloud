using DotNetCloud.Modules.Search.Extractors;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="RtfContentExtractor"/>.
/// </summary>
[TestClass]
public class RtfContentExtractorTests
{
    private RtfContentExtractor _extractor = null!;

    [TestInitialize]
    public void Setup()
    {
        _extractor = new RtfContentExtractor();
    }

    [TestMethod]
    public void CanExtract_ApplicationRtf_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("application/rtf"));
    }

    [TestMethod]
    public void CanExtract_TextRtf_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("text/rtf"));
    }

    [TestMethod]
    public void CanExtract_CaseInsensitive_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("APPLICATION/RTF"));
    }

    [TestMethod]
    public void CanExtract_TextPlain_ReturnsFalse()
    {
        Assert.IsFalse(_extractor.CanExtract("text/plain"));
    }

    [TestMethod]
    public async Task ExtractAsync_SimpleRtf_ExtractsText()
    {
        var rtf = @"{\rtf1\ansi{\fonttbl\f0 Times New Roman;}\f0\fs24 Hello World!\par This is a test.}";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtf));

        var result = await _extractor.ExtractAsync(stream, "application/rtf");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Hello World!"));
        Assert.IsTrue(result.Text.Contains("This is a test."));
    }

    [TestMethod]
    public void StripRtf_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, RtfContentExtractor.StripRtf(string.Empty));
    }

    [TestMethod]
    public void StripRtf_EscapedCharacters_Preserved()
    {
        var rtf = @"{\rtf1 Open brace \{ Close brace \} Backslash \\}";
        var result = RtfContentExtractor.StripRtf(rtf);
        Assert.IsTrue(result.Contains("{"));
        Assert.IsTrue(result.Contains("}"));
        Assert.IsTrue(result.Contains("\\"));
    }

    [TestMethod]
    public void StripRtf_HexEncodedChars_Decoded()
    {
        // \'e9 = é (Latin Small Letter E with Acute)
        var rtf = @"{\rtf1 caf\'e9}";
        var result = RtfContentExtractor.StripRtf(rtf);
        Assert.IsTrue(result.Contains("café"));
    }
}

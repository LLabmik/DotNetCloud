using DotNetCloud.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Core.Tests.Logging;

/// <summary>
/// Tests for <see cref="LogSanitizer"/>.
/// </summary>
[TestClass]
public sealed class LogSanitizerTests
{
    [TestMethod]
    public void Sanitize_NullInput_ReturnsNullPlaceholder()
    {
        Assert.AreEqual("(null)", LogSanitizer.Sanitize(null));
    }

    [TestMethod]
    public void Sanitize_EmptyInput_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, LogSanitizer.Sanitize(string.Empty));
    }

    [TestMethod]
    public void Sanitize_CleanInput_ReturnsSameContent()
    {
        const string input = "Alice's display name";

        var result = LogSanitizer.Sanitize(input);

        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public void Sanitize_EmbeddedLineEndings_ReplacedWithSingleSpaces()
    {
        var result = LogSanitizer.Sanitize("a\r\nb\nc\rd");

        Assert.AreEqual("a b c d", result);
    }

    [TestMethod]
    public void Sanitize_MixedCrLfAndLf_ReplacedWithSpaces()
    {
        var result = LogSanitizer.Sanitize("first line\r\nsecond line\nthird line");

        Assert.AreEqual("first line second line third line", result);
    }

    [TestMethod]
    public void Sanitize_ControlCharacters_ReplacedWithSpaces()
    {
        var result = LogSanitizer.Sanitize("a\u0000\u0001b");

        Assert.AreEqual("a  b", result);
    }

    [TestMethod]
    public void Sanitize_TabCharacter_IsPreserved()
    {
        var result = LogSanitizer.Sanitize("a\tb");

        Assert.AreEqual("a\tb", result);
    }

    [TestMethod]
    public void Sanitize_OverMaxLength_TruncatesToLimit()
    {
        var input = new string('x', 10_001);

        var result = LogSanitizer.Sanitize(input);

        Assert.AreEqual(10_000, result.Length);
        Assert.AreEqual(new string('x', 10_000), result);
    }

    [TestMethod]
    public void Sanitize_NewlineBeyondTruncationPoint_IsRemoved()
    {
        // A newline past the 10k cap must not survive truncation into the output.
        var input = new string('x', 10_000) + "\npayload";

        var result = LogSanitizer.Sanitize(input);

        Assert.AreEqual(new string('x', 10_000), result);
        Assert.IsFalse(result.Contains('\n'));
    }
}

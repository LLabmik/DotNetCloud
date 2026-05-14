using DotNetCloud.Core.ServiceDefaults.Logging;

namespace DotNetCloud.Core.Tests.Logging;

/// <summary>
/// Tests for <see cref="SafeStringDestructuringPolicy"/>.
/// </summary>
[TestClass]
public sealed class SafeStringDestructuringPolicyTests
{
    [TestMethod]
    public void Sanitize_NullInput_ReturnsNull()
    {
        Assert.IsNull(SafeStringDestructuringPolicy.Sanitize(null!));
    }

    [TestMethod]
    public void Sanitize_EmptyInput_ReturnsEmpty()
    {
        var result = SafeStringDestructuringPolicy.Sanitize(string.Empty);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void Sanitize_CleanString_ReturnsSame()
    {
        const string input = "Hello, world! This is a normal log message.";
        var result = SafeStringDestructuringPolicy.Sanitize(input);
        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public void Sanitize_Newlines_ReplacedWithSpace()
    {
        var result = SafeStringDestructuringPolicy.Sanitize("line1\nline2\r\nline3\rline4");
        Assert.AreEqual("line1 line2 line3 line4", result);
    }

    [TestMethod]
    public void Sanitize_ControlCharacters_ReplacedWithSpace()
    {
        var result = SafeStringDestructuringPolicy.Sanitize("a\0b\u0001c");
        Assert.AreEqual("a b c", result);
    }

    [TestMethod]
    public void Sanitize_TabChar_Preserved()
    {
        const string input = "col1\tcol2\tcol3";
        var result = SafeStringDestructuringPolicy.Sanitize(input);
        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public void Sanitize_VeryLongString_Truncated()
    {
        var input = new string('a', 15_000);
        var result = SafeStringDestructuringPolicy.Sanitize(input);
        Assert.AreEqual(10_000, result.Length);
    }

    [TestMethod]
    public void Sanitize_LogForgingAttempt_Neutralized()
    {
        const string input = "user@example.com\n[INFO] System compromised";
        var result = SafeStringDestructuringPolicy.Sanitize(input);
        Assert.IsFalse(result.Contains('\n'));
        Assert.IsFalse(result.Contains("\r"));
        Assert.AreEqual("user@example.com [INFO] System compromised", result);
    }
}

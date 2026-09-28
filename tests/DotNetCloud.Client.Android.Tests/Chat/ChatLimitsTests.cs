using DotNetCloud.Client.Android.Chat;

namespace DotNetCloud.Client.Android.Tests.Chat;

/// <summary>
/// Tests for <see cref="ChatLimits"/> — the administrator-configured chat limits the composer and
/// attachment picker enforce client-side.
/// </summary>
[TestClass]
public sealed class ChatLimitsTests
{
    [TestMethod]
    public void Defaults_MatchTheServerBuiltInValues()
    {
        Assert.AreEqual(10000, ChatLimits.Defaults.MaxMessageLength);
        Assert.AreEqual(10, ChatLimits.Defaults.MaxAttachmentsPerMessage);
        Assert.AreEqual(10, ChatLimits.Defaults.MaxAttachmentSizeMb);
        Assert.IsTrue(ChatLimits.Defaults.HasMessageLengthLimit);
        Assert.IsTrue(ChatLimits.Defaults.HasAttachmentSizeLimit);
    }

    [TestMethod]
    public void MaxAttachmentBytes_ConvertsMegabytes()
    {
        Assert.AreEqual(2L * 1024 * 1024, new ChatLimits(100, 10, 2).MaxAttachmentBytes);
    }

    [TestMethod]
    public void MaxAttachmentBytes_Zero_IsUnlimited()
    {
        Assert.AreEqual(0L, new ChatLimits(100, 10, 0).MaxAttachmentBytes);
        Assert.IsFalse(new ChatLimits(100, 10, 0).HasAttachmentSizeLimit);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("12345")]
    public void IsOverMessageLength_TextAtOrBelowTheLimit_IsFalse(string? text)
    {
        Assert.IsFalse(new ChatLimits(5, 10, 10).IsOverMessageLength(text));
    }

    [TestMethod]
    public void IsOverMessageLength_TextAboveTheLimit_IsTrue()
    {
        Assert.IsTrue(new ChatLimits(5, 10, 10).IsOverMessageLength("123456"));
    }

    [TestMethod]
    public void IsOverMessageLength_NoLimit_IsAlwaysFalse()
    {
        Assert.IsFalse(new ChatLimits(0, 10, 10).IsOverMessageLength(new string('x', 5000)));
        Assert.IsFalse(new ChatLimits(0, 10, 10).HasMessageLengthLimit);
    }

    [TestMethod]
    public void ClampMessageText_TextWithinTheLimit_IsUnchanged()
    {
        Assert.AreEqual("12345", new ChatLimits(5, 10, 10).ClampMessageText("12345"));
    }

    [TestMethod]
    public void ClampMessageText_TextAboveTheLimit_IsTruncated()
    {
        Assert.AreEqual("12345", new ChatLimits(5, 10, 10).ClampMessageText("1234567890"));
    }

    [TestMethod]
    public void ClampMessageText_NoLimit_IsUnchanged()
    {
        var text = new string('x', 500);
        Assert.AreEqual(text, new ChatLimits(0, 10, 10).ClampMessageText(text));
    }

    [TestMethod]
    public void ClampMessageText_ClampAtASurrogateBoundary_KeepsBothHalvesOut()
    {
        // "abcd" + emoji = 6 UTF-16 units; clamping to 5 must not keep a lone high surrogate.
        var clamped = new ChatLimits(5, 10, 10).ClampMessageText("abcd😀");

        Assert.AreEqual("abcd", clamped);
    }
}

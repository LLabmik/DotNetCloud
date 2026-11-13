using DotNetCloud.Core.Modules;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="ModuleNotificationLimits"/>: module-supplied notification content is
/// untrusted, and the action link is navigated to by the user's browser.
/// </summary>
[TestClass]
public class ModuleNotificationLimitsTests
{
    [TestMethod]
    [DataRow("/calendar")]
    [DataRow("/files/folder/abc?tab=1")]
    [DataRow("/")]
    public void IsValidLink_SiteRelativePath_IsAccepted(string link)
    {
        Assert.IsTrue(ModuleNotificationLimits.IsValidLink(link));
    }

    [TestMethod]
    [DataRow("https://evil.example")]
    [DataRow("http://evil.example/path")]
    [DataRow("javascript:alert(1)")]
    [DataRow("data:text/html,<script>x</script>")]
    [DataRow("//evil.example/path")]
    [DataRow("/\\evil.example/path")]
    [DataRow("calendar")]
    [DataRow("")]
    public void IsValidLink_UnsafeOrNonRelativeValue_IsRejected(string? link)
    {
        if (string.IsNullOrEmpty(link))
        {
            Assert.IsTrue(ModuleNotificationLimits.IsValidLink(link),
                "An absent link is allowed; only a supplied non-relative link is rejected.");
            return;
        }

        Assert.IsFalse(ModuleNotificationLimits.IsValidLink(link));
    }

    [TestMethod]
    public void IsValidLink_ControlCharacters_IsRejected()
    {
        Assert.IsFalse(ModuleNotificationLimits.IsValidLink("/calendar\nSet-Cookie: x"));
    }

    [TestMethod]
    public void IsValidLink_OverlongLink_IsRejected()
    {
        var link = "/" + new string('a', ModuleNotificationLimits.MaxLinkLength);
        Assert.IsFalse(ModuleNotificationLimits.IsValidLink(link));
    }

    [TestMethod]
    public void IsValidTitle_AtLimit_IsAccepted()
    {
        var title = new string('a', ModuleNotificationLimits.MaxTitleLength);
        Assert.IsTrue(ModuleNotificationLimits.IsValidTitle(title));
    }

    [TestMethod]
    public void IsValidTitle_OverLimit_IsRejected()
    {
        var title = new string('a', ModuleNotificationLimits.MaxTitleLength + 1);
        Assert.IsFalse(ModuleNotificationLimits.IsValidTitle(title));
    }

    [TestMethod]
    public void IsValidBody_AbsentOrAtLimit_IsAccepted()
    {
        Assert.IsTrue(ModuleNotificationLimits.IsValidBody(null));
        Assert.IsTrue(ModuleNotificationLimits.IsValidBody(string.Empty));
        Assert.IsTrue(ModuleNotificationLimits.IsValidBody(new string('a', ModuleNotificationLimits.MaxBodyLength)));
    }

    [TestMethod]
    public void IsValidBody_OverLimit_IsRejected()
    {
        Assert.IsFalse(ModuleNotificationLimits.IsValidBody(
            new string('a', ModuleNotificationLimits.MaxBodyLength + 1)));
    }
}

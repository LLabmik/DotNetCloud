using System.Net;
using System.Text.Json;
using DotNetCloud.Core.Server.Services;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public class GitHubUpdateServiceTests
{
    // -----------------------------------------------------------------------
    // Version comparison tests
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow("0.1.0", "0.2.0", true)]
    [DataRow("0.1.0", "0.1.1", true)]
    [DataRow("0.1.0", "1.0.0", true)]
    [DataRow("0.1.7", "0.2.0", true)]
    [DataRow("0.1.7-alpha", "0.1.7", true)]    // pre-release → release is newer
    [DataRow("0.1.7-alpha", "0.2.0", true)]
    [DataRow("0.2.0", "0.1.0", false)]
    [DataRow("0.1.1", "0.1.0", false)]
    [DataRow("1.0.0", "0.9.9", false)]
    [DataRow("0.1.7", "0.1.7", false)]          // same version
    [DataRow("0.1.7", "0.1.7-alpha", false)]    // release → pre-release is not newer
    [DataRow("0.1.7-alpha", "0.1.7-alpha", false)] // same pre-release
    [DataRow("0.1.7-alpha", "0.1.7-beta", false)]  // same base, both pre-release
    public void IsNewerVersion_ReturnsExpected(string current, string latest, bool expected)
    {
        var result = GitHubUpdateService.IsNewerVersion(current, latest);
        Assert.AreEqual(expected, result, $"Expected IsNewerVersion({current}, {latest}) = {expected}");
    }

    [TestMethod]
    [DataRow("invalid", "0.1.0", false)]
    [DataRow("0.1.0", "invalid", false)]
    [DataRow("", "", false)]
    public void IsNewerVersion_WithInvalidVersions_ReturnsFalse(string current, string latest, bool expected)
    {
        var result = GitHubUpdateService.IsNewerVersion(current, latest);
        Assert.AreEqual(expected, result);
    }

    // -----------------------------------------------------------------------
    // Platform inference tests
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow("dotnetcloud-0.2.0-linux-x64.tar.gz", "linux-x64")]
    [DataRow("dotnetcloud-0.2.0-linux-arm64.tar.gz", "linux-arm64")]
    [DataRow("dotnetcloud-0.2.0-win-x64.zip", "win-x64")]
    [DataRow("dotnetcloud-0.2.0-win-arm64.zip", "win-arm64")]
    [DataRow("dotnetcloud-0.2.0-osx-x64.tar.gz", "osx-x64")]
    [DataRow("dotnetcloud-0.2.0-osx-arm64.tar.gz", "osx-arm64")]
    [DataRow("dotnetcloud-0.2.0-android.apk", "android")]
    [DataRow("dotnetcloud-0.2.0-linux-x64.tar.gz.sha256", "linux-x64")]
    [DataRow("random-file.txt", null)]
    [DataRow("CHANGELOG.md", null)]
    public void InferPlatform_ReturnsExpected(string fileName, string? expected)
    {
        var result = GitHubUpdateService.InferPlatform(fileName);
        Assert.AreEqual(expected, result, $"InferPlatform({fileName})");
    }
}

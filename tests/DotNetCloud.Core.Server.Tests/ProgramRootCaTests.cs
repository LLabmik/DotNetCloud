using DotNetCloud.Core.Server;

namespace DotNetCloud.Core.Server.Tests;

[TestClass]
public class ProgramRootCaTests
{
    [TestMethod]
    public void GetRootCaPath_NullPath_ReturnsNull()
    {
        Assert.IsNull(Program.GetRootCaPath(null));
    }

    [TestMethod]
    public void GetRootCaPath_WhitespacePath_ReturnsNull()
    {
        Assert.IsNull(Program.GetRootCaPath("   "));
    }

    [TestMethod]
    public void GetRootCaPath_RelativeFileWithoutDirectory_ReturnsSiblingRootCa()
    {
        var result = Program.GetRootCaPath("dotnetcloud-selfsigned.pfx");

        Assert.AreEqual(Path.Combine(".", "dotnetcloud-root-ca.crt"), result);
    }

    [TestMethod]
    public void GetRootCaPath_AbsolutePath_ReturnsSiblingRootCa()
    {
        // An absolute path is what a system install configures (`/etc/dotnetcloud/certs/...` on
        // Linux). The host builds it here rather than pasting the POSIX literal: Windows rewrites
        // that literal's separators in Path.GetDirectoryName, so the literal would assert the host's
        // separator handling instead of "the root CA sits beside the certificate".
        var certificatePath = Path.Combine(
            Path.GetTempPath(), "dotnetcloud", "certs", "dotnetcloud-selfsigned.pfx");

        var result = Program.GetRootCaPath(certificatePath);

        Assert.AreEqual(
            Path.Combine(Path.GetDirectoryName(certificatePath)!, "dotnetcloud-root-ca.crt"),
            result);
        Assert.IsTrue(Path.IsPathRooted(result), "the derived path must stay absolute");
    }
}

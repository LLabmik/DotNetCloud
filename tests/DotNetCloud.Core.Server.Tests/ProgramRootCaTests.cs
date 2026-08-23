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
        var result = Program.GetRootCaPath("/etc/dotnetcloud/certs/dotnetcloud-selfsigned.pfx");

        Assert.AreEqual("/etc/dotnetcloud/certs/dotnetcloud-root-ca.crt", result);
    }
}

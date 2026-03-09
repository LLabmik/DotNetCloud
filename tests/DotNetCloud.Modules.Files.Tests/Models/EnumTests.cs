using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Tests.Models;

/// <summary>
/// Tests for Files module enums verifying values and member counts.
/// </summary>
[TestClass]
public class EnumTests
{
#pragma warning disable MSTEST0032 // Canary tests guard compile-time constant enum values

    [TestMethod]
    public void WhenFileNodeTypeCheckedThenHasThreeMembers()
    {
        var values = Enum.GetValues<FileNodeType>();

        Assert.AreEqual(3, values.Length);
        Assert.AreEqual(0, (int)FileNodeType.File);
        Assert.AreEqual(1, (int)FileNodeType.Folder);
        Assert.AreEqual(2, (int)FileNodeType.SymbolicLink);
    }

    [TestMethod]
    public void WhenShareTypeCheckedThenHasFourMembers()
    {
        var values = Enum.GetValues<ShareType>();

        Assert.AreEqual(4, values.Length);
        Assert.AreEqual(0, (int)ShareType.User);
        Assert.AreEqual(1, (int)ShareType.Team);
        Assert.AreEqual(2, (int)ShareType.Group);
        Assert.AreEqual(3, (int)ShareType.PublicLink);
    }

    [TestMethod]
    public void WhenSharePermissionCheckedThenHasThreeMembers()
    {
        var values = Enum.GetValues<SharePermission>();

        Assert.AreEqual(3, values.Length);
        Assert.AreEqual(0, (int)SharePermission.Read);
        Assert.AreEqual(1, (int)SharePermission.ReadWrite);
        Assert.AreEqual(2, (int)SharePermission.Full);
    }

    [TestMethod]
    public void WhenUploadSessionStatusCheckedThenHasFiveMembers()
    {
        var values = Enum.GetValues<UploadSessionStatus>();

        Assert.AreEqual(5, values.Length);
        Assert.AreEqual(0, (int)UploadSessionStatus.InProgress);
        Assert.AreEqual(1, (int)UploadSessionStatus.Completed);
        Assert.AreEqual(2, (int)UploadSessionStatus.Cancelled);
        Assert.AreEqual(3, (int)UploadSessionStatus.Expired);
        Assert.AreEqual(4, (int)UploadSessionStatus.Failed);
    }

#pragma warning restore MSTEST0032
}

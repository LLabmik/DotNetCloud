using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.UI;
using DotNetCloud.UI.Shared.Components.DataDisplay;

namespace DotNetCloud.Modules.Files.Tests.UI;

/// <summary>
/// Tests for <see cref="VirtualNodeSourceKinds"/> and <see cref="VirtualNodeOrdering"/> — the
/// single source of truth that pins the virtual <c>_DotNetCloud</c> root above every other entry
/// in the Files browser and in the Photos / Music / Video media-source browsers.
/// </summary>
[TestClass]
public class VirtualNodeOrderingTests
{
    private static FileNodeDto Folder(string name, string? virtualSourceKind = null)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NodeType = "Folder",
            VirtualSourceKind = virtualSourceKind
        };

    private static FileNodeViewModel ViewModelFolder(string name, string? virtualSourceKind = null)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NodeType = "Folder",
            VirtualSourceKind = virtualSourceKind
        };

    [TestMethod]
    public void IsDotNetCloudRoot_WithDotNetCloudSourceKind_ReturnsTrue()
    {
        Assert.IsTrue(VirtualNodeSourceKinds.IsDotNetCloudRoot("Some Name", VirtualNodeSourceKinds.DotNetCloudRoot));
    }

    [TestMethod]
    public void IsDotNetCloudRoot_WithDotNetCloudDisplayName_ReturnsTrue()
    {
        Assert.IsTrue(VirtualNodeSourceKinds.IsDotNetCloudRoot("_DotNetCloud", null));
    }

    [TestMethod]
    public void IsDotNetCloudRoot_WithUnrelatedNode_ReturnsFalse()
    {
        Assert.IsFalse(VirtualNodeSourceKinds.IsDotNetCloudRoot("Documents", null));
    }

    [TestMethod]
    public void IsDotNetCloudRoot_WithDifferentlyCasedName_ReturnsFalse()
    {
        Assert.IsFalse(VirtualNodeSourceKinds.IsDotNetCloudRoot("_dotnetcloud", null));
    }

    [TestMethod]
    public void PinDotNetCloudRootFirst_DotNetCloudNotFirst_MovesItToTop()
    {
        var nodes = new[] { Folder("Alpha"), Folder("_DotNetCloud"), Folder("Zeta") };

        var ordered = nodes.PinDotNetCloudRootFirst(n => n.Name, n => n.VirtualSourceKind).ToList();

        Assert.AreEqual("_DotNetCloud", ordered[0].Name);
        CollectionAssert.AreEqual(
            new[] { "_DotNetCloud", "Alpha", "Zeta" },
            ordered.Select(n => n.Name).ToArray());
    }

    [TestMethod]
    public void PinDotNetCloudRootFirst_MatchesVirtualSourceKind()
    {
        var nodes = new[]
        {
            Folder("Alpha"),
            Folder("Renamed Shared Root", VirtualNodeSourceKinds.DotNetCloudRoot)
        };

        var ordered = nodes.PinDotNetCloudRootFirst(n => n.Name, n => n.VirtualSourceKind).ToList();

        Assert.AreEqual("Renamed Shared Root", ordered[0].Name);
    }

    [TestMethod]
    public void PinDotNetCloudRootFirst_WithoutDotNetCloud_PreservesOrder()
    {
        var nodes = new[] { Folder("Beta"), Folder("Alpha") };

        var ordered = nodes.PinDotNetCloudRootFirst(n => n.Name, n => n.VirtualSourceKind).ToList();

        CollectionAssert.AreEqual(
            new[] { "Beta", "Alpha" },
            ordered.Select(n => n.Name).ToArray());
    }

    [TestMethod]
    public void PinDotNetCloudRootFirst_ThenByName_PinsRootAndSortsRemainingAlphabetically()
    {
        // Mirrors the Photos / Music / Video media-source browser composition.
        var nodes = new[]
        {
            Folder("Zeta"),
            Folder("Alpha"),
            Folder("_DotNetCloud")
        };

        var ordered = nodes
            .PinDotNetCloudRootFirst(n => n.Name, n => n.VirtualSourceKind)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        CollectionAssert.AreEqual(
            new[] { "_DotNetCloud", "Alpha", "Zeta" },
            ordered.Select(n => n.Name).ToArray());
    }

    [TestMethod]
    public void PinDotNetCloudRootFirst_WorksOnViewModels()
    {
        // Mirrors the Files browser (SortedNodes) usage over view models.
        var nodes = new[] { ViewModelFolder("Reports"), ViewModelFolder("_DotNetCloud") };

        var ordered = nodes.PinDotNetCloudRootFirst(n => n.Name, n => n.VirtualSourceKind).ToList();

        Assert.AreEqual("_DotNetCloud", ordered[0].Name);
        Assert.AreEqual("Reports", ordered[1].Name);
    }

    [TestMethod]
    public void DotNetCloudRootIcon_IsRegisteredInSvgRegistry()
    {
        // MaterialIcon renders an unregistered name as literal text (e.g. the words "cloud_sync"),
        // so the virtual root's icon must exist in the SVG registry to actually render.
        Assert.IsTrue(
            MaterialSvgIcons.HasPath(VirtualNodeSourceKinds.DotNetCloudRootIcon),
            $"'{VirtualNodeSourceKinds.DotNetCloudRootIcon}' is not registered in MaterialSvgIcons.");
    }
}

using DotNetCloud.Core.Server.Supervisor;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

/// <summary>
/// Tests for <see cref="PathContainment"/>.
/// </summary>
[TestClass]
public class PathContainmentTests
{
    private static string Root => Path.Combine(Path.GetTempPath(), "dnc-modules-root");

    [TestMethod]
    public void TryResolveWithin_ChildPath_IsContained()
    {
        var candidate = Path.Combine(Root, "dotnetcloud.files", "dotnetcloud.files.dll");

        var contained = PathContainment.TryResolveWithin(Root, candidate, out var resolved);

        Assert.IsTrue(contained);
        Assert.AreEqual(Path.GetFullPath(candidate), resolved);
    }

    [TestMethod]
    public void TryResolveWithin_SiblingPrefixDirectory_IsRejected()
    {
        // Regression for the CodeQL path-injection finding: a naive
        // StartsWith("<root>") check accepts "<root>-evil/..." as contained.
        var siblingRoot = Root + "-evil";
        var candidate = Path.Combine(siblingRoot, "dotnetcloud.evil.dll");

        var contained = PathContainment.TryResolveWithin(Root, candidate, out var resolved);

        Assert.IsFalse(contained);
        Assert.AreEqual(string.Empty, resolved);
    }

    [TestMethod]
    public void TryResolveWithin_ParentTraversal_IsRejected()
    {
        var candidate = Path.Combine(Root, "..", "outside", "payload.dll");

        var contained = PathContainment.TryResolveWithin(Root, candidate, out var resolved);

        Assert.IsFalse(contained);
        Assert.AreEqual(string.Empty, resolved);
    }

    [TestMethod]
    public void TryResolveWithin_RootItself_IsContained()
    {
        var contained = PathContainment.TryResolveWithin(Root, Root, out var resolved);

        Assert.IsTrue(contained);
        Assert.AreEqual(Path.GetFullPath(Root), resolved);
    }

    [TestMethod]
    [DataRow(null, "child")]
    [DataRow("root", null)]
    [DataRow("", "child")]
    [DataRow("root", "")]
    public void TryResolveWithin_MissingPath_IsRejected(string? root, string? candidate)
    {
        var contained = PathContainment.TryResolveWithin(root!, candidate!, out var resolved);

        Assert.IsFalse(contained);
        Assert.AreEqual(string.Empty, resolved);
    }
}

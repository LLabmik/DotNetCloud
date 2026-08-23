using DotNetCloud.Client.Core.Sync;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.Sync;

[TestClass]
public class SyncFolderOverlapGuardTests
{
    private static string Root(string name) => Path.Combine(Path.GetTempPath(), name);

    [TestMethod]
    public void PathsOverlap_SamePath_True()
    {
        var p = Root("docs");
        Assert.IsTrue(SyncFolderOverlapGuard.PathsOverlap(p, p));
    }

    [TestMethod]
    public void PathsOverlap_ChildInsideParent_True()
    {
        var parent = Root("docs");
        var child = Path.Combine(parent, "sub");
        Assert.IsTrue(SyncFolderOverlapGuard.PathsOverlap(parent, child));
    }

    [TestMethod]
    public void PathsOverlap_ParentContainsChild_True()
    {
        var parent = Root("docs");
        var child = Path.Combine(parent, "sub");
        Assert.IsTrue(SyncFolderOverlapGuard.PathsOverlap(child, parent));
    }

    [TestMethod]
    public void PathsOverlap_SiblingPaths_False()
    {
        var a = Root("docs");
        var b = Root("photos");
        Assert.IsFalse(SyncFolderOverlapGuard.PathsOverlap(a, b));
    }

    [TestMethod]
    public void PathsOverlap_TrailingSeparator_TreatedAsSame()
    {
        var p = Root("docs");
        var pWithSep = p + Path.DirectorySeparatorChar;
        Assert.IsTrue(SyncFolderOverlapGuard.PathsOverlap(p, pWithSep));
    }

    [TestMethod]
    public void PathsOverlap_SamePrefixDifferentFolder_False()
    {
        var docs = Root("docs");
        var docs2 = Root("docs2");
        Assert.IsFalse(SyncFolderOverlapGuard.PathsOverlap(docs, docs2));
    }
}

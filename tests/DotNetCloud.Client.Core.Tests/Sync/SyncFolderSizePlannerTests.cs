using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Sync;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.Sync;

[TestClass]
public class SyncFolderSizePlannerTests
{
    private const long Limit = 250L * 1024 * 1024; // 250 MiB

    private static SyncTreeNodeResponse Folder(string name, params SyncTreeNodeResponse[] children) =>
        new() { NodeId = Guid.CreateVersion7(), Name = name, NodeType = "Folder", Children = children };

    private static SyncTreeNodeResponse File(string name, long size) =>
        new() { NodeId = Guid.CreateVersion7(), Name = name, NodeType = "File", Size = size };

    [TestMethod]
    public void Plan_AllUnderLimit_NoExclusions()
    {
        var tree = Folder("root",
            File("a.txt", 10),
            Folder("small", File("b.txt", 20)));

        var exclusions = SyncFolderSizePlanner.PlanExclusions(tree, Limit);
        Assert.AreEqual(0, exclusions.Count);
    }

    [TestMethod]
    public void Plan_RootOverLimitSingleBigChild_ExcludesOnlyThatChild()
    {
        // The classic case: /Documents contains bigfiles (over limit) and other small files.
        // Only bigfiles is excluded; /Documents and the rest stay synced.
        var tree = Folder("root",
            File("ok.txt", 10),
            Folder("bigfiles",
                File("huge.bin", Limit + 1),
                File("tiny.txt", 5)));

        var exclusions = SyncFolderSizePlanner.PlanExclusions(tree, Limit);
        Assert.AreEqual(1, exclusions.Count);
        Assert.AreEqual("bigfiles", exclusions[0]);
    }

    [TestMethod]
    public void Plan_FolderOverLimitNoSingleChildOver_ExcludesWholeFolder()
    {
        // Each file is small, but the folder's aggregate exceeds the limit → the folder is the unit.
        var tree = Folder("root",
            Folder("docs",
                File("a.bin", Limit / 2 + 1),
                File("b.bin", Limit / 2 + 1)));

        var exclusions = SyncFolderSizePlanner.PlanExclusions(tree, Limit);
        Assert.AreEqual(1, exclusions.Count);
        Assert.AreEqual("docs", exclusions[0]);
    }

    [TestMethod]
    public void Plan_LeafOverLimitDueToOneHugeFile_ExcludesWholeFolder()
    {
        // Confirmed decision: a leaf folder over limit only because of one huge file is
        // excluded as a whole.
        var tree = Folder("root",
            Folder("data", File("huge.iso", Limit * 2)));

        var exclusions = SyncFolderSizePlanner.PlanExclusions(tree, Limit);
        Assert.AreEqual(1, exclusions.Count);
        Assert.AreEqual("data", exclusions[0]);
    }

    [TestMethod]
    public void Plan_NestedBigChild_ExcludesDeepestOverLimitFolder()
    {
        var tree = Folder("root",
            Folder("a",
                Folder("b",
                    Folder("c",
                        File("huge.bin", Limit + 1)))));

        var exclusions = SyncFolderSizePlanner.PlanExclusions(tree, Limit);
        Assert.AreEqual(1, exclusions.Count);
        Assert.AreEqual("a/b/c", exclusions[0]);
    }

    [TestMethod]
    public void Plan_MultipleOverLimitChildren_ExcludesEach()
    {
        var tree = Folder("root",
            Folder("videos", File("movie.mkv", Limit + 1)),
            Folder("photos", File("pic.bin", Limit + 1)));

        var exclusions = SyncFolderSizePlanner.PlanExclusions(tree, Limit);
        Assert.AreEqual(2, exclusions.Count);
        CollectionAssert.Contains((System.Collections.ICollection)exclusions, "videos");
        CollectionAssert.Contains((System.Collections.ICollection)exclusions, "photos");
    }

    [TestMethod]
    public void Plan_RootItselfOverLimit_NotExcluded()
    {
        var tree = Folder("root", File("huge.bin", Limit * 3));

        // The sync root itself is never excluded (it is the mapped folder).
        var exclusions = SyncFolderSizePlanner.PlanExclusions(tree, Limit);
        Assert.AreEqual(0, exclusions.Count);
    }

    [TestMethod]
    public void RecursiveSize_FolderAggregatesDescendantFileSizes()
    {
        var tree = Folder("root",
            File("a.txt", 100),
            Folder("sub", File("b.txt", 200)));

        // Root total = 300 (> 150) but the root is not excluded; "sub" = 200 (> 150) → excluded.
        var exclusions = SyncFolderSizePlanner.PlanExclusions(tree, 150);
        Assert.AreEqual(1, exclusions.Count);
        Assert.AreEqual("sub", exclusions[0]);
    }
}

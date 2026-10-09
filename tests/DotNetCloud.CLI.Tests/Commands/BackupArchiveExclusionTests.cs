using DotNetCloud.CLI.Commands;

namespace DotNetCloud.CLI.Tests.Commands;

/// <summary>
/// Tests for the archive exclusions that stop a backup from swallowing its own output and every
/// previous backup.
/// </summary>
[TestClass]
public class BackupArchiveExclusionTests
{
    private static string Root => OperatingSystem.IsWindows() ? @"C:\dnc" : "/dnc";

    private static string BackupDir => Path.Combine(Root, "backups");

    private static IReadOnlyList<string> DefaultExclusions() =>
        BackupCommands.GetArchiveExclusions(BackupDir, Path.Combine(BackupDir, "dotnetcloud-backup-20261009-010203.zip"));

    [TestMethod]
    public void IsExcludedFromArchive_FileInsideBackupDirectory_IsExcluded()
    {
        Assert.IsTrue(BackupCommands.IsExcludedFromArchive(
            Path.Combine(BackupDir, "storage-pre-20260823.tar.gz"), DefaultExclusions()));
    }

    [TestMethod]
    public void IsExcludedFromArchive_DeeplyNestedFileInsideBackupDirectory_IsExcluded()
    {
        Assert.IsTrue(BackupCommands.IsExcludedFromArchive(
            Path.Combine(BackupDir, "nested", "deeper", "old.zip"), DefaultExclusions()));
    }

    [TestMethod]
    public void IsExcludedFromArchive_SiblingSharingTheNamePrefix_IsNotExcluded()
    {
        // "backups-old" must not be caught by a "backups" prefix comparison.
        Assert.IsFalse(BackupCommands.IsExcludedFromArchive(
            Path.Combine(Root, "backups-old", "keepme.zip"), DefaultExclusions()));
    }

    [TestMethod]
    public void IsExcludedFromArchive_TheArchiveBeingWritten_IsExcluded()
    {
        var archive = Path.Combine(Root, "elsewhere", "dotnetcloud-backup-20261009-010203.zip");
        var exclusions = BackupCommands.GetArchiveExclusions(BackupDir, archive);

        Assert.IsTrue(BackupCommands.IsExcludedFromArchive(archive, exclusions));
    }

    [TestMethod]
    public void IsExcludedFromArchive_OrdinaryDataFiles_AreNotExcluded()
    {
        var exclusions = DefaultExclusions();

        Assert.IsFalse(BackupCommands.IsExcludedFromArchive(
            Path.Combine(Root, "storage", "files", "ae", "39", "ae395195dc7daec31457fb9d7787fb0bc9b5af21a49751fa8b9068b3a6f9a0dd"),
            exclusions));
        Assert.IsFalse(BackupCommands.IsExcludedFromArchive(Path.Combine(Root, "config.json"), exclusions));
    }

    [TestMethod]
    public void GetArchiveExclusions_BlankBackupDirectory_StillExcludesTheArchive()
    {
        var archive = Path.Combine(Root, "out.zip");

        var exclusions = BackupCommands.GetArchiveExclusions("   ", archive);

        Assert.AreEqual(1, exclusions.Count);
        Assert.IsTrue(BackupCommands.IsExcludedFromArchive(archive, exclusions));
    }

    [TestMethod]
    public void IsExcludedFromArchive_UnnormalisedPath_IsResolvedBeforeComparing()
    {
        var messy = Path.Combine(BackupDir, "nested", "..", "old.zip");

        Assert.IsTrue(BackupCommands.IsExcludedFromArchive(messy, DefaultExclusions()));
    }

    [TestMethod]
    public void IsExcludedFromArchive_EmptyPath_IsNotExcluded()
    {
        Assert.IsFalse(BackupCommands.IsExcludedFromArchive(string.Empty, DefaultExclusions()));
    }
}

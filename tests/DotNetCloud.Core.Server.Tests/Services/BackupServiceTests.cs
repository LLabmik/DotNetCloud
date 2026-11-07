using System.IO.Compression;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="BackupService"/> path handling.
/// </summary>
/// <remarks>
/// Backup archives are written at an administrator-supplied path. The resolved archive must stay
/// inside the configured backup directory, otherwise an admin (or a forged request) could write a
/// zip over an arbitrary file.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class BackupServiceTests
{
    private string _root = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), $"dnc-backup-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            try
            { Directory.Delete(_root, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private static BackupService CreateService()
        => new(NullLogger<BackupService>.Instance, new ConfigurationBuilder().Build());

    private static BackupOptions FileOnlyOptions(string backupDirectory) => new()
    {
        BackupDirectory = backupDirectory,
        IncludeDatabaseDump = false,
        IncludeFileStorage = false,
        IncludeModuleData = false,
    };

    [TestMethod]
    public async Task CreateBackupAsync_OutputPathOutsideBackupDirectory_ThrowsUnauthorizedAccessException()
    {
        var backupDirectory = Path.Combine(_root, "backups");
        var outsidePath = Path.Combine(_root, "escape.zip");

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => CreateService().CreateBackupAsync(
                outsidePath, FileOnlyOptions(backupDirectory)));
    }

    [TestMethod]
    public async Task CreateBackupAsync_OutputPathWithTraversal_ThrowsUnauthorizedAccessException()
    {
        var backupDirectory = Path.Combine(_root, "backups");
        // "backups/../escape.zip" resolves to a sibling of the backup directory.
        var traversalPath = Path.Combine(backupDirectory, "..", "escape.zip");

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => CreateService().CreateBackupAsync(
                traversalPath, FileOnlyOptions(backupDirectory)));
    }

    [TestMethod]
    public async Task CreateBackupAsync_OutputPathInsideBackupDirectory_CreatesArchive()
    {
        var backupDirectory = Path.Combine(_root, "backups");
        var outputPath = Path.Combine(backupDirectory, "manual.zip");

        var result = await CreateService().CreateBackupAsync(
            outputPath, FileOnlyOptions(backupDirectory));

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual(Path.GetFullPath(outputPath), result.FilePath);
        Assert.IsTrue(File.Exists(outputPath));
        using var archive = ZipFile.OpenRead(outputPath);
        Assert.IsTrue(archive.Entries.Any(e => e.FullName == "config/database-info.txt"));
    }

    /// <summary>
    /// F11: an isolated module's data lives under <c>&lt;dataDir&gt;/modules/&lt;id&gt;/data</c> and
    /// its connection string is encrypted with the DataProtection key ring, which also lives under
    /// the data directory. Both must end up in the archive, otherwise a restore cannot bring a
    /// sandboxed module back.
    /// </summary>
    [TestMethod]
    public async Task CreateBackupAsync_IncludesModuleDataAndDataProtectionKeyRing()
    {
        var backupDirectory = Path.Combine(_root, "backups");
        var dataDirectory = Path.Combine(_root, "data");

        var moduleDataDirectory = Path.Combine(dataDirectory, "modules", "vendor.receipts", "data");
        var keyRingDirectory = Path.Combine(dataDirectory, "data-protection-keys");
        Directory.CreateDirectory(moduleDataDirectory);
        Directory.CreateDirectory(keyRingDirectory);
        File.WriteAllText(Path.Combine(moduleDataDirectory, "receipts.db"), "module payload");
        File.WriteAllText(Path.Combine(keyRingDirectory, "key-1.xml"), "<key />");

        var outputPath = Path.Combine(backupDirectory, "sandbox.zip");
        var options = new BackupOptions
        {
            BackupDirectory = backupDirectory,
            DataDirectory = dataDirectory,
            IncludeDatabaseDump = false,
            IncludeFileStorage = false,
            IncludeModuleData = true,
        };

        var result = await CreateService().CreateBackupAsync(outputPath, options);

        Assert.IsTrue(result.Success, result.ErrorMessage);
        using var archive = ZipFile.OpenRead(outputPath);
        Assert.IsTrue(
            archive.Entries.Any(e => e.FullName == "data/modules/vendor.receipts/data/receipts.db"),
            "the module's own data directory must be in the archive");
        Assert.IsTrue(
            archive.Entries.Any(e => e.FullName == "data/data-protection-keys/key-1.xml"),
            "the DataProtection key ring must be in the archive so the stored connection string stays decryptable");
    }

    /// <summary>
    /// F11: restoring the archive puts the module's data back byte-for-byte, which is what makes a
    /// restored sandboxed module usable without a manual copy step.
    /// </summary>
    [TestMethod]
    public async Task RestoreBackupAsync_RestoresIsolatedModuleData()
    {
        var backupDirectory = Path.Combine(_root, "backups");
        var sourceData = Path.Combine(_root, "data-source");
        var moduleDataDirectory = Path.Combine(sourceData, "modules", "vendor.receipts", "data");
        Directory.CreateDirectory(moduleDataDirectory);
        File.WriteAllText(Path.Combine(moduleDataDirectory, "receipts.db"), "module payload");

        var outputPath = Path.Combine(backupDirectory, "sandbox.zip");
        var backup = await CreateService().CreateBackupAsync(
            outputPath,
            new BackupOptions
            {
                BackupDirectory = backupDirectory,
                DataDirectory = sourceData,
                IncludeDatabaseDump = false,
                IncludeFileStorage = false,
                IncludeModuleData = true,
            });
        Assert.IsTrue(backup.Success, backup.ErrorMessage);

        var restoreTarget = Path.Combine(_root, "data-restored");
        var restore = await CreateService().RestoreBackupAsync(
            outputPath,
            new RestoreOptions { DataDirectory = restoreTarget, RestoreDatabase = false });

        Assert.IsTrue(restore.Success, restore.ErrorMessage);

        var restoredFile = Path.Combine(restoreTarget, "modules", "vendor.receipts", "data", "receipts.db");
        Assert.IsTrue(File.Exists(restoredFile));
        Assert.AreEqual("module payload", await File.ReadAllTextAsync(restoredFile));
    }
}

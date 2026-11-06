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
}

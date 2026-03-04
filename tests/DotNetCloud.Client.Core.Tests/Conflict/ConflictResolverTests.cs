using DotNetCloud.Client.Core.Conflict;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.Conflict;

[TestClass]
public class ConflictResolverTests
{
    private string _tempDir = null!;
    private ConflictResolver _resolver = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _resolver = new ConflictResolver(NullLogger<ConflictResolver>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── ResolveAsync ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ResolveAsync_MovesLocalFileToConflictCopy()
    {
        var localPath = Path.Combine(_tempDir, "report.docx");
        await File.WriteAllTextAsync(localPath, "original content");

        await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
        });

        Assert.IsFalse(File.Exists(localPath), "Original path should be renamed.");
        var conflictFiles = Directory.GetFiles(_tempDir, "*conflict*");
        Assert.AreEqual(1, conflictFiles.Length);
    }

    [TestMethod]
    public async Task ResolveAsync_FiresConflictDetectedEvent()
    {
        var localPath = Path.Combine(_tempDir, "notes.txt");
        await File.WriteAllTextAsync(localPath, "content");

        ConflictDetectedEventArgs? eventArgs = null;
        _resolver.ConflictDetected += (_, args) => eventArgs = args;

        await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
        });

        Assert.IsNotNull(eventArgs);
        Assert.AreEqual(localPath, eventArgs.OriginalPath);
        StringAssert.Contains(eventArgs.ConflictCopyPath, "conflict");
    }

    [TestMethod]
    public async Task ResolveAsync_NonExistentFile_NoOp()
    {
        var localPath = Path.Combine(_tempDir, "missing.txt");

        // Should not throw
        await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
        });
    }

    // ── BuildConflictCopyPath ───────────────────────────────────────────────

    [TestMethod]
    public void BuildConflictCopyPath_ContainsConflictKeyword()
    {
        var path = Path.Combine(_tempDir, "document.docx");

        var result = ConflictResolver.BuildConflictCopyPath(path);

        StringAssert.Contains(result, "conflict");
        StringAssert.Contains(result, ".docx");
    }

    [TestMethod]
    public void BuildConflictCopyPath_PreservesDirectory()
    {
        var path = Path.Combine(_tempDir, "subdir", "file.txt");

        var result = ConflictResolver.BuildConflictCopyPath(path);

        var dir = Path.GetDirectoryName(result);
        Assert.AreEqual(Path.Combine(_tempDir, "subdir"), dir);
    }

    [TestMethod]
    public void BuildConflictCopyPath_WhenConflictExists_Increments()
    {
        // Create the first conflict file
        var path = Path.Combine(_tempDir, "report.pdf");
        var firstConflict = ConflictResolver.BuildConflictCopyPath(path);
        File.WriteAllText(firstConflict, "");

        var secondConflict = ConflictResolver.BuildConflictCopyPath(path);

        Assert.AreNotEqual(firstConflict, secondConflict);
        StringAssert.Contains(secondConflict, "1");
    }
}

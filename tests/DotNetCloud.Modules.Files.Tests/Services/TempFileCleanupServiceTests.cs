using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Files.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for <see cref="TempFileCleanupService"/> per-pattern retention logic.
/// Verifies that album ZIPs get 24-hour retention while other temp files get 1-hour retention.
/// </summary>
[TestClass]
public sealed class TempFileCleanupServiceTests : IDisposable
{
    private string _tmpDir = null!;
    private TempFileCleanupService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), $"test-tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpDir);

        var options = Microsoft.Extensions.Options.Options.Create(new FileUploadOptions { TmpPath = _tmpDir });
        var tracker = new Mock<IBackgroundServiceTracker>();
        _service = new TempFileCleanupService(
            options,
            NullLogger<TempFileCleanupService>.Instance,
            tracker.Object);
    }

    [TestCleanup]
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tmpDir))
                Directory.Delete(_tmpDir, recursive: true);
        }
        catch { /* best-effort */ }
    }

    // ── Album ZIP (24h retention) ────────────────────────────────────

    [TestMethod]
    public void Cleanup_AlbumZip_YoungerThan24h_Kept()
    {
        var file = CreateFile("dotnetcloud-album-a1b2c3d4-20260601-120000.zip", hoursOld: 12);

        _service.Cleanup();

        Assert.IsTrue(File.Exists(file), "Album ZIP younger than 24h should be kept.");
    }

    [TestMethod]
    public void Cleanup_AlbumZip_OlderThan24h_Deleted()
    {
        var file = CreateFile("dotnetcloud-album-e5f6g7h8-20260530-120000.zip", hoursOld: 26);

        _service.Cleanup();

        Assert.IsFalse(File.Exists(file), "Album ZIP older than 24h should be deleted.");
    }

    [TestMethod]
    public void Cleanup_AlbumZip_Exactly24h_Kept()
    {
        // Set file to 23.9 hours old to avoid sub-millisecond clock drift between
        // CreateFile (sets LastWriteTime) and Cleanup (captures DateTime.UtcNow).
        // The requirement is "at least 24 hours" — 23.9h must survive.
        var file = CreateFile("dotnetcloud-album-i9j0k1l2-20260531-120000.zip", hoursOld: 23.9);

        _service.Cleanup();

        Assert.IsTrue(File.Exists(file), "Album ZIP just under 24h old should be kept.");
    }

    // ── Regular ZIP / temp files (1h retention) ──────────────────────

    [TestMethod]
    public void Cleanup_RegularZip_YoungerThan1h_Kept()
    {
        var file = CreateFile("dotnetcloud-zip-m3n4o5p6.zip", hoursOld: 0.5);

        _service.Cleanup();

        Assert.IsTrue(File.Exists(file), "Regular ZIP younger than 1h should be kept.");
    }

    [TestMethod]
    public void Cleanup_RegularZip_OlderThan1h_Deleted()
    {
        var file = CreateFile("dotnetcloud-zip-q7r8s9t0.zip", hoursOld: 2);

        _service.Cleanup();

        Assert.IsFalse(File.Exists(file), "Regular ZIP older than 1h should be deleted.");
    }

    [TestMethod]
    public void Cleanup_UploadTempFile_OlderThan1h_Deleted()
    {
        var file = CreateFile("upload-abc123.tmp", hoursOld: 3);

        _service.Cleanup();

        Assert.IsFalse(File.Exists(file), "Upload temp file older than 1h should be deleted.");
    }

    [TestMethod]
    public void Cleanup_UploadTempFile_YoungerThan1h_Kept()
    {
        var file = CreateFile("upload-def456.tmp", hoursOld: 0.2);

        _service.Cleanup();

        Assert.IsTrue(File.Exists(file), "Upload temp file younger than 1h should be kept.");
    }

    // ── Mixed cleanup ─────────────────────────────────────────────────

    [TestMethod]
    public void Cleanup_Mixed_OnlyDeletesStale()
    {
        var keepAlbum = CreateFile("dotnetcloud-album-u1v2w3x4-20260601-120000.zip", hoursOld: 10);
        var deleteAlbum = CreateFile("dotnetcloud-album-y5z6a7b8-20260530-120000.zip", hoursOld: 30);
        var keepZip = CreateFile("dotnetcloud-zip-c9d0e1f2.zip", hoursOld: 0.5);
        var deleteZip = CreateFile("dotnetcloud-zip-g3h4i5j6.zip", hoursOld: 5);

        _service.Cleanup();

        Assert.IsTrue(File.Exists(keepAlbum), "Album ZIP 10h old should be kept.");
        Assert.IsFalse(File.Exists(deleteAlbum), "Album ZIP 30h old should be deleted.");
        Assert.IsTrue(File.Exists(keepZip), "Regular ZIP 0.5h old should be kept.");
        Assert.IsFalse(File.Exists(deleteZip), "Regular ZIP 5h old should be deleted.");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private string CreateFile(string fileName, double hoursOld)
    {
        var path = Path.Combine(_tmpDir, fileName);
        File.WriteAllText(path, "test-content");
        var lastWrite = DateTime.UtcNow.AddHours(-hoursOld);
        File.SetLastWriteTimeUtc(path, lastWrite);
        return path;
    }
}

using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="LocalChatImageStore"/>.
/// </summary>
[TestClass]
public class LocalChatImageStoreTests
{
    private string _tempDir = null!;
    private LocalChatImageStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "chat-image-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Files:Storage:RootPath"] = _tempDir
            })
            .Build();

        _store = new LocalChatImageStore(config, NullLogger<LocalChatImageStore>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── SaveAsync: Happy Path ──────────────────────────────────────

    [TestMethod]
    public async Task SaveAsync_ValidPng_ReturnResult()
    {
        var data = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic bytes
        var result = await _store.SaveAsync("test.png", "image/png", data);

        Assert.IsNotNull(result);
        Assert.AreEqual("image/png", result.ContentType);
        Assert.AreEqual(4L, result.FileSize);
        Assert.IsTrue(result.Url.StartsWith("/api/v1/chat/uploads/"));
        Assert.IsTrue(result.StoredFileName.EndsWith(".png"));
    }

    [TestMethod]
    public async Task SaveAsync_ValidJpeg_ReturnResult()
    {
        var data = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var result = await _store.SaveAsync("photo.jpg", "image/jpeg", data);

        Assert.AreEqual("image/jpeg", result.ContentType);
        Assert.IsTrue(result.StoredFileName.EndsWith(".jpg"));
    }

    [TestMethod]
    public async Task SaveAsync_ValidGif_ReturnResult()
    {
        var data = new byte[] { 0x47, 0x49, 0x46, 0x38 };
        var result = await _store.SaveAsync("anim.gif", "image/gif", data);

        Assert.AreEqual("image/gif", result.ContentType);
        Assert.IsTrue(result.StoredFileName.EndsWith(".gif"));
    }

    [TestMethod]
    public async Task SaveAsync_ValidWebp_ReturnResult()
    {
        var data = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        var result = await _store.SaveAsync("image.webp", "image/webp", data);

        Assert.AreEqual("image/webp", result.ContentType);
        Assert.IsTrue(result.StoredFileName.EndsWith(".webp"));
    }

    [TestMethod]
    public async Task SaveAsync_ValidSvg_ReturnResult()
    {
        var data = "<svg></svg>"u8.ToArray();
        var result = await _store.SaveAsync("icon.svg", "image/svg+xml", data);

        Assert.AreEqual("image/svg+xml", result.ContentType);
        Assert.IsTrue(result.StoredFileName.EndsWith(".svg"));
    }

    [TestMethod]
    public async Task SaveAsync_ValidBmp_ReturnResult()
    {
        var data = new byte[] { 0x42, 0x4D, 0x00, 0x00 };
        var result = await _store.SaveAsync("bitmap.bmp", "image/bmp", data);

        Assert.AreEqual("image/bmp", result.ContentType);
        Assert.IsTrue(result.StoredFileName.EndsWith(".bmp"));
    }

    [TestMethod]
    public async Task SaveAsync_ValidTiff_ReturnResult()
    {
        var data = new byte[] { 0x49, 0x49, 0x2A, 0x00 };
        var result = await _store.SaveAsync("scan.tiff", "image/tiff", data);

        Assert.AreEqual("image/tiff", result.ContentType);
        Assert.IsTrue(result.StoredFileName.EndsWith(".tiff"));
    }

    [TestMethod]
    public async Task SaveAsync_ValidIco_ReturnResult()
    {
        var data = new byte[] { 0x00, 0x00, 0x01, 0x00 };
        var result = await _store.SaveAsync("favicon.ico", "image/x-icon", data);

        Assert.AreEqual("image/x-icon", result.ContentType);
        Assert.IsTrue(result.StoredFileName.EndsWith(".ico"));
    }

    [TestMethod]
    public async Task SaveAsync_ValidHeic_ReturnResult()
    {
        var data = new byte[] { 0x00, 0x00, 0x00, 0x18 };
        var result = await _store.SaveAsync("photo.heic", "image/heic", data);

        Assert.AreEqual("image/heic", result.ContentType);
        Assert.IsTrue(result.StoredFileName.EndsWith(".heic"));
    }

    [TestMethod]
    public async Task SaveAsync_WritesFileToDisk()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var result = await _store.SaveAsync("test.png", "image/png", data);

        var filePath = Path.Combine(_tempDir, "chat-uploads", result.StoredFileName);
        Assert.IsTrue(File.Exists(filePath));
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(filePath));
    }

    [TestMethod]
    public async Task SaveAsync_GeneratesUniqueFileNames()
    {
        var data = new byte[] { 1, 2, 3 };
        var r1 = await _store.SaveAsync("test.png", "image/png", data);
        var r2 = await _store.SaveAsync("test.png", "image/png", data);

        Assert.AreNotEqual(r1.StoredFileName, r2.StoredFileName);
        Assert.AreNotEqual(r1.Url, r2.Url);
    }

    // ── SaveAsync: MIME Fallback from Extension ────────────────────

    [TestMethod]
    public async Task SaveAsync_UnknownMime_FallsBackToExtension()
    {
        var data = new byte[] { 1, 2, 3 };
        var result = await _store.SaveAsync("photo.jpg", "application/octet-stream", data);

        Assert.AreEqual("image/jpeg", result.ContentType);
        Assert.IsTrue(result.StoredFileName.EndsWith(".jpg"));
    }

    [TestMethod]
    public async Task SaveAsync_EmptyMime_FallsBackToExtension()
    {
        var data = new byte[] { 1, 2, 3 };
        var result = await _store.SaveAsync("photo.png", "", data);

        Assert.AreEqual("image/png", result.ContentType);
    }

    // ── SaveAsync: Validation Failures ─────────────────────────────

    [TestMethod]
    public async Task SaveAsync_EmptyData_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _store.SaveAsync("test.png", "image/png", Array.Empty<byte>()));

        Assert.IsTrue(ex.Message.Contains("empty"));
    }

    [TestMethod]
    public async Task SaveAsync_ExceedsMaxSize_ThrowsArgumentException()
    {
        var data = new byte[10 * 1024 * 1024 + 1]; // 10 MB + 1 byte

        var ex = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _store.SaveAsync("huge.png", "image/png", data));

        Assert.IsTrue(ex.Message.Contains("maximum size"));
    }

    [TestMethod]
    public async Task SaveAsync_AtMaxSize_Succeeds()
    {
        var data = new byte[10 * 1024 * 1024]; // Exactly 10 MB

        var result = await _store.SaveAsync("big.png", "image/png", data);

        Assert.IsNotNull(result);
        Assert.AreEqual(10 * 1024 * 1024L, result.FileSize);
    }

    [TestMethod]
    public async Task SaveAsync_UnsupportedMimeType_ThrowsArgumentException()
    {
        var data = new byte[] { 1, 2, 3 };

        var ex = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _store.SaveAsync("virus.exe", "application/x-msdownload", data));

        Assert.IsTrue(ex.Message.Contains("Unsupported"));
    }

    [TestMethod]
    public async Task SaveAsync_TextHtml_ThrowsArgumentException()
    {
        var data = "<html></html>"u8.ToArray();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _store.SaveAsync("page.html", "text/html", data));
    }

    [TestMethod]
    public async Task SaveAsync_ApplicationJson_ThrowsArgumentException()
    {
        var data = "{}"u8.ToArray();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _store.SaveAsync("data.json", "application/json", data));
    }

    // ── GetAsync: Happy Path ───────────────────────────────────────

    [TestMethod]
    public async Task GetAsync_ExistingFile_ReturnsFileWithCorrectData()
    {
        var data = new byte[] { 10, 20, 30, 40, 50 };
        var saved = await _store.SaveAsync("test.png", "image/png", data);

        var file = await _store.GetAsync(saved.StoredFileName);

        Assert.IsNotNull(file);
        CollectionAssert.AreEqual(data, file.Data);
        Assert.AreEqual("image/png", file.ContentType);
    }

    [TestMethod]
    public async Task GetAsync_JpegFile_ReturnsJpegContentType()
    {
        var data = new byte[] { 0xFF, 0xD8 };
        var saved = await _store.SaveAsync("photo.jpg", "image/jpeg", data);

        var file = await _store.GetAsync(saved.StoredFileName);

        Assert.IsNotNull(file);
        Assert.AreEqual("image/jpeg", file.ContentType);
    }

    [TestMethod]
    public async Task GetAsync_GifFile_ReturnsGifContentType()
    {
        var data = new byte[] { 0x47, 0x49, 0x46 };
        var saved = await _store.SaveAsync("anim.gif", "image/gif", data);

        var file = await _store.GetAsync(saved.StoredFileName);

        Assert.IsNotNull(file);
        Assert.AreEqual("image/gif", file.ContentType);
    }

    // ── GetAsync: Not Found ────────────────────────────────────────

    [TestMethod]
    public async Task GetAsync_NonExistentFile_ReturnsNull()
    {
        var result = await _store.GetAsync("nonexistent.png");

        Assert.IsNull(result);
    }

    // ── GetAsync: Path Traversal Prevention ────────────────────────

    [TestMethod]
    public async Task GetAsync_PathTraversal_DotDot_ReturnsNull()
    {
        var result = await _store.GetAsync("../../etc/passwd");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAsync_PathTraversal_ForwardSlash_ReturnsNull()
    {
        var result = await _store.GetAsync("sub/file.png");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAsync_PathTraversal_Backslash_ReturnsNull()
    {
        var result = await _store.GetAsync("sub\\file.png");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAsync_EmptyFileName_ReturnsNull()
    {
        var result = await _store.GetAsync("");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAsync_WhitespaceFileName_ReturnsNull()
    {
        var result = await _store.GetAsync("   ");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAsync_NullFileName_ReturnsNull()
    {
        var result = await _store.GetAsync(null!);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAsync_DotDotAtStart_ReturnsNull()
    {
        var result = await _store.GetAsync("..secret.png");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAsync_AbsolutePath_ReturnsNull()
    {
        var result = await _store.GetAsync("/etc/passwd");

        Assert.IsNull(result);
    }

    // ── Constructor: Default Storage Path ──────────────────────────

    [TestMethod]
    public void Constructor_NoConfig_UsesDefaultStoragePath()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Should not throw — creates storage/chat-uploads under current dir
        var store = new LocalChatImageStore(config, NullLogger<LocalChatImageStore>.Instance);

        Assert.IsNotNull(store);
    }

    // ── Round-trip: Save then Get ──────────────────────────────────

    [TestMethod]
    public async Task SaveAndGet_RoundTrip_DataIsPreserved()
    {
        var original = new byte[1024];
        Random.Shared.NextBytes(original);

        var saved = await _store.SaveAsync("random.png", "image/png", original);
        var loaded = await _store.GetAsync(saved.StoredFileName);

        Assert.IsNotNull(loaded);
        CollectionAssert.AreEqual(original, loaded.Data);
        Assert.AreEqual("image/png", loaded.ContentType);
    }

    [TestMethod]
    public async Task SaveAndGet_MultipleFiles_EachRetrievable()
    {
        var data1 = new byte[] { 1, 2, 3 };
        var data2 = new byte[] { 4, 5, 6 };
        var data3 = new byte[] { 7, 8, 9 };

        var r1 = await _store.SaveAsync("a.png", "image/png", data1);
        var r2 = await _store.SaveAsync("b.jpg", "image/jpeg", data2);
        var r3 = await _store.SaveAsync("c.gif", "image/gif", data3);

        var f1 = await _store.GetAsync(r1.StoredFileName);
        var f2 = await _store.GetAsync(r2.StoredFileName);
        var f3 = await _store.GetAsync(r3.StoredFileName);

        Assert.IsNotNull(f1);
        Assert.IsNotNull(f2);
        Assert.IsNotNull(f3);
        CollectionAssert.AreEqual(data1, f1.Data);
        CollectionAssert.AreEqual(data2, f2.Data);
        CollectionAssert.AreEqual(data3, f3.Data);
    }
}

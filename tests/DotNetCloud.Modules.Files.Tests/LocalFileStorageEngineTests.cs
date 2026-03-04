using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests;

/// <summary>
/// Tests for <see cref="LocalFileStorageEngine"/> covering read, write, existence, and delete operations.
/// </summary>
[TestClass]
public class LocalFileStorageEngineTests
{
    private string _testBasePath = null!;
    private LocalFileStorageEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _testBasePath = Path.Combine(Path.GetTempPath(), $"dotnetcloud-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testBasePath);
        _engine = new LocalFileStorageEngine(_testBasePath,
            NullLogger<LocalFileStorageEngine>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testBasePath))
        {
            Directory.Delete(_testBasePath, recursive: true);
        }
    }

    [TestMethod]
    public async Task WhenChunkWrittenThenExistsReturnsTrue()
    {
        var data = "hello"u8.ToArray();
        var path = "chunks/ab/cd/test-chunk";

        await _engine.WriteChunkAsync(path, data);

        Assert.IsTrue(await _engine.ExistsAsync(path));
    }

    [TestMethod]
    public async Task WhenChunkWrittenThenReadReturnsCorrectData()
    {
        var data = "hello world"u8.ToArray();
        var path = "chunks/ab/cd/test-chunk";

        await _engine.WriteChunkAsync(path, data);
        var result = await _engine.ReadChunkAsync(path);

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(data, result);
    }

    [TestMethod]
    public async Task WhenChunkDoesNotExistThenReadReturnsNull()
    {
        var result = await _engine.ReadChunkAsync("chunks/no/pe/nonexistent");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task WhenChunkDoesNotExistThenExistsReturnsFalse()
    {
        Assert.IsFalse(await _engine.ExistsAsync("chunks/no/pe/nonexistent"));
    }

    [TestMethod]
    public async Task WhenChunkDeletedThenExistsReturnsFalse()
    {
        var data = "to-delete"u8.ToArray();
        var path = "chunks/de/le/delete-me";

        await _engine.WriteChunkAsync(path, data);
        Assert.IsTrue(await _engine.ExistsAsync(path));

        await _engine.DeleteAsync(path);

        Assert.IsFalse(await _engine.ExistsAsync(path));
    }

    [TestMethod]
    public async Task WhenDeleteNonExistentChunkThenDoesNotThrow()
    {
        // Should not throw
        await _engine.DeleteAsync("chunks/no/pe/nonexistent");
    }

    [TestMethod]
    public async Task WhenOpenReadStreamCalledThenReturnsReadableStream()
    {
        var data = "stream test"u8.ToArray();
        var path = "chunks/st/re/stream-chunk";

        await _engine.WriteChunkAsync(path, data);

        await using var stream = await _engine.OpenReadStreamAsync(path);

        Assert.IsNotNull(stream);
        Assert.IsTrue(stream.CanRead);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        CollectionAssert.AreEqual(data, ms.ToArray());
    }

    [TestMethod]
    public async Task WhenOpenReadStreamForNonExistentThenReturnsNull()
    {
        var stream = await _engine.OpenReadStreamAsync("chunks/no/pe/nonexistent");

        Assert.IsNull(stream);
    }

    [TestMethod]
    public async Task WhenGetTotalSizeCalledWithNoFilesThenReturnsZero()
    {
        var emptyPath = Path.Combine(Path.GetTempPath(), $"dotnetcloud-empty-{Guid.NewGuid():N}");
        var emptyEngine = new LocalFileStorageEngine(emptyPath,
            NullLogger<LocalFileStorageEngine>.Instance);

        var size = await emptyEngine.GetTotalSizeAsync();

        Assert.AreEqual(0, size);
    }

    [TestMethod]
    public async Task WhenGetTotalSizeCalledWithFilesThenReturnsTotalBytes()
    {
        var data1 = new byte[100];
        var data2 = new byte[200];

        await _engine.WriteChunkAsync("chunks/a1/b1/chunk1", data1);
        await _engine.WriteChunkAsync("chunks/a2/b2/chunk2", data2);

        var size = await _engine.GetTotalSizeAsync();

        Assert.AreEqual(300, size);
    }

    [TestMethod]
    public void WhenConstructedWithNullBasePathThenThrows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new LocalFileStorageEngine(null!,
                NullLogger<LocalFileStorageEngine>.Instance));
    }

    [TestMethod]
    public void WhenConstructedWithEmptyBasePathThenThrows()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new LocalFileStorageEngine("",
                NullLogger<LocalFileStorageEngine>.Instance));
    }

    [TestMethod]
    public async Task WhenStoragePathContainsTraversalThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _engine.WriteChunkAsync("../../../etc/passwd", new byte[1]));
    }

    [TestMethod]
    public async Task WhenStoragePathIsNullThenWriteThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _engine.WriteChunkAsync(null!, new byte[1]));
    }

    [TestMethod]
    public async Task WhenStoragePathIsEmptyThenReadThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _engine.ReadChunkAsync(""));
    }

    [TestMethod]
    public void WhenImplementsIFileStorageEngineThenIsCorrectType()
    {
        Assert.IsInstanceOfType<IFileStorageEngine>(_engine);
    }

    [TestMethod]
    public async Task WhenLargeChunkWrittenThenReadReturnsCorrectData()
    {
        var data = new byte[4 * 1024 * 1024]; // 4MB
        Random.Shared.NextBytes(data);
        var path = "chunks/lg/ch/large-chunk";

        await _engine.WriteChunkAsync(path, data);
        var result = await _engine.ReadChunkAsync(path);

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(data, result);
    }
}

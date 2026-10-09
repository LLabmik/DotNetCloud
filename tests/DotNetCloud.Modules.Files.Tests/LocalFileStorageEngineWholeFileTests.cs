using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests;

/// <summary>
/// Tests for the whole-file storage primitives on <see cref="LocalFileStorageEngine"/>:
/// streaming writes with atomic replace, and path enumeration.
/// </summary>
[TestClass]
public class LocalFileStorageEngineWholeFileTests
{
    private string _basePath = null!;
    private LocalFileStorageEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dnc-engine-wholefile-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(_basePath);
        _engine = new LocalFileStorageEngine(_basePath, NullLogger<LocalFileStorageEngine>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, recursive: true);
    }

    [TestMethod]
    public async Task WriteFromStreamAsync_WritesContentAtPath()
    {
        var data = "whole file payload"u8.ToArray();
        using var source = new MemoryStream(data);

        await _engine.WriteFromStreamAsync("files/ab/cd/blob1", source, data.Length);

        var read = await _engine.ReadChunkAsync("files/ab/cd/blob1");
        CollectionAssert.AreEqual(data, read);
        Assert.IsFalse(Directory.EnumerateFiles(_basePath, "*.tmp-*", SearchOption.AllDirectories).Any());
    }

    [TestMethod]
    public async Task WriteFromStreamAsync_LengthMismatch_ThrowsAndLeavesNoFile()
    {
        using var source = new MemoryStream("short"u8.ToArray());

        await Assert.ThrowsExactlyAsync<IOException>(
            () => _engine.WriteFromStreamAsync("files/ab/cd/blob2", source, expectedLength: 9999));

        Assert.IsFalse(await _engine.ExistsAsync("files/ab/cd/blob2"));
        Assert.IsFalse(Directory.EnumerateFiles(_basePath, "*.tmp-*", SearchOption.AllDirectories).Any(),
            "A failed write must not leave a scratch file behind.");
    }

    [TestMethod]
    public async Task WriteFromStreamAsync_OverwritesExistingBlobAtomically()
    {
        await _engine.WriteChunkAsync("files/ab/cd/blob3", "old"u8.ToArray());
        using var source = new MemoryStream("new-content"u8.ToArray());

        await _engine.WriteFromStreamAsync("files/ab/cd/blob3", source, 11);

        var read = await _engine.ReadChunkAsync("files/ab/cd/blob3");
        CollectionAssert.AreEqual("new-content"u8.ToArray(), read);
    }

    [TestMethod]
    public async Task EnumerateStoragePathsAsync_ReturnsRelativePathsUnderPrefix()
    {
        await _engine.WriteChunkAsync("files/ab/cd/one", "1"u8.ToArray());
        await _engine.WriteChunkAsync("files/ee/ff/two", "2"u8.ToArray());
        await _engine.WriteChunkAsync("chunks/aa/bb/other", "3"u8.ToArray());

        var paths = new List<string>();
        await foreach (var path in _engine.EnumerateStoragePathsAsync("files"))
            paths.Add(path);

        CollectionAssert.AreEquivalent(
            new[] { "files/ab/cd/one", "files/ee/ff/two" },
            paths);
    }

    [TestMethod]
    public async Task EnumerateStoragePathsAsync_MissingPrefix_IsEmpty()
    {
        var paths = new List<string>();
        await foreach (var path in _engine.EnumerateStoragePathsAsync("files"))
            paths.Add(path);

        Assert.AreEqual(0, paths.Count);
    }
}

using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests.Services;

[TestClass]
public sealed class ShareFileWriterTests
{
    private string _cacheRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), "dnc-share-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_cacheRoot))
            Directory.Delete(_cacheRoot, recursive: true);
    }

    [TestMethod]
    public async Task WriteShareFileAsync_SameFileSharedTwice_ProducesDistinctPaths()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3 };

        // Act
        var first = await ShareFileWriter.WriteShareFileAsync(data, "MindTheSeam-BestCover.png", _cacheRoot);
        var second = await ShareFileWriter.WriteShareFileAsync(data, "MindTheSeam-BestCover.png", _cacheRoot);

        // Assert — a shared path means a shared FileProvider URI, which receiving apps cache.
        Assert.AreNotEqual(first, second);
        Assert.AreNotEqual(Path.GetDirectoryName(first), Path.GetDirectoryName(second));
    }

    [TestMethod]
    public async Task WriteShareFileAsync_SourceName_IsKeptForTheReceiver()
    {
        // Act
        var path = await ShareFileWriter.WriteShareFileAsync(new byte[] { 9 }, "MindTheSeam-BestCover.png", _cacheRoot);

        // Assert
        Assert.AreEqual("MindTheSeam-BestCover.png", Path.GetFileName(path));
    }

    [TestMethod]
    public async Task WriteShareFileAsync_WritesThePayload()
    {
        // Arrange
        var data = new byte[] { 4, 5, 6 };

        // Act
        var path = await ShareFileWriter.WriteShareFileAsync(data, "x.jpg", _cacheRoot);

        // Assert
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task WriteShareFileAsync_ParentDirectoryName_StaysInsideTheShareRoot()
    {
        // Act
        var path = await ShareFileWriter.WriteShareFileAsync(new byte[] { 1 }, "..", _cacheRoot);

        // Assert
        var shareRoot = Path.GetFullPath(Path.Combine(_cacheRoot, "share")) + Path.DirectorySeparatorChar;
        Assert.IsTrue(Path.GetFullPath(path).StartsWith(shareRoot, StringComparison.Ordinal));
        Assert.AreNotEqual("..", Path.GetFileName(path));
    }

    [TestMethod]
    public void BuildSafeFileName_NoUsableName_FallsBackToImage()
    {
        Assert.AreEqual("image", ShareFileWriter.BuildSafeFileName(null));
        Assert.AreEqual("image", ShareFileWriter.BuildSafeFileName(string.Empty));
        Assert.AreEqual("image", ShareFileWriter.BuildSafeFileName("   "));
    }

    [TestMethod]
    public void BuildSafeFileName_DirectoryQualifiedName_KeepsOnlyTheFileName()
    {
        Assert.AreEqual("BestCover.png", ShareFileWriter.BuildSafeFileName("/tmp/a/BestCover.png"));
    }

    [TestMethod]
    public void BuildSafeFileName_FileWithoutExtension_KeepsName()
    {
        Assert.AreEqual("BestCover", ShareFileWriter.BuildSafeFileName("BestCover"));
    }
}

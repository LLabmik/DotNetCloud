using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Tests.Models;

/// <summary>
/// Tests for <see cref="FileVersionChunk"/> junction entity.
/// </summary>
[TestClass]
public class FileVersionChunkTests
{
    [TestMethod]
    public void WhenCreatedThenSequenceIndexIsZero()
    {
        var mapping = new FileVersionChunk();

        Assert.AreEqual(0, mapping.SequenceIndex);
    }

    [TestMethod]
    public void WhenCreatedThenNavigationPropertiesAreNull()
    {
        var mapping = new FileVersionChunk();

        Assert.IsNull(mapping.FileVersion);
        Assert.IsNull(mapping.FileChunk);
    }

    [TestMethod]
    public void WhenPropertiesSetThenStoresValues()
    {
        var versionId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();

        var mapping = new FileVersionChunk
        {
            FileVersionId = versionId,
            FileChunkId = chunkId,
            SequenceIndex = 3
        };

        Assert.AreEqual(versionId, mapping.FileVersionId);
        Assert.AreEqual(chunkId, mapping.FileChunkId);
        Assert.AreEqual(3, mapping.SequenceIndex);
    }
}

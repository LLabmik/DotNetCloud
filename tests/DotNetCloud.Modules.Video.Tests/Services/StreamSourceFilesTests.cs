using DotNetCloud.Modules.Video.Host.Services;

namespace DotNetCloud.Modules.Video.Tests.Services;

/// <summary>
/// Tests for <see cref="StreamSourceFiles"/> — the guard that stops the streaming endpoints from
/// deleting permanent storage when a download hands back a direct file stream.
/// </summary>
[TestClass]
public class StreamSourceFilesTests
{
    [TestMethod]
    public void IsDeletableScratchFile_TempFile_IsTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), "dotnetcloud-download-abc123.bin");

        Assert.IsTrue(StreamSourceFiles.IsDeletableScratchFile(path));
    }

    [TestMethod]
    public void IsDeletableScratchFile_TempStreamSource_IsTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), "dotnetcloud-stream-source", "source-01a11ef4");

        Assert.IsTrue(StreamSourceFiles.IsDeletableScratchFile(path));
    }

    [TestMethod]
    public void IsDeletableScratchFile_WholeFileBlob_IsFalse()
    {
        // The exact shape that was being deleted: a whole-file media blob in the storage root.
        const string path = "/var/lib/dotnetcloud/storage/files/ae/39/ae395195dc7daec31457fb9d7787fb0bc9b5af21a49751fa8b9068b3a6f9a0dd";

        Assert.IsFalse(StreamSourceFiles.IsDeletableScratchFile(path));
    }

    [TestMethod]
    public void IsDeletableScratchFile_AdminSharedSourceFile_IsFalse()
    {
        const string path = "/srv/media/20261005_225022.mp4";

        Assert.IsFalse(StreamSourceFiles.IsDeletableScratchFile(path));
    }

    [TestMethod]
    public void IsDeletableScratchFile_TempPathPrefixSibling_IsFalse()
    {
        // "/tmp-evil/..." must not pass just because it starts with the temp path text.
        var tempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var path = tempRoot + "-evil" + Path.DirectorySeparatorChar + "blob";

        Assert.IsFalse(StreamSourceFiles.IsDeletableScratchFile(path));
    }

    [TestMethod]
    public void IsDeletableScratchFile_NullOrEmpty_IsFalse()
    {
        Assert.IsFalse(StreamSourceFiles.IsDeletableScratchFile(null));
        Assert.IsFalse(StreamSourceFiles.IsDeletableScratchFile(string.Empty));
        Assert.IsFalse(StreamSourceFiles.IsDeletableScratchFile("   "));
    }
}

using DotNetCloud.Modules.Files.Services;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for <see cref="FileStorageClassifier"/> media classification.
/// </summary>
[TestClass]
public class FileStorageClassifierTests
{
    [TestMethod]
    [DataRow("image/jpeg")]
    [DataRow("IMAGE/PNG")]
    [DataRow("audio/mpeg")]
    [DataRow("video/mp4")]
    public void IsImmutableMedia_MediaMimePrefix_ReturnsTrue(string mimeType)
    {
        Assert.IsTrue(FileStorageClassifier.IsImmutableMedia(mimeType, "file.bin"));
    }

    [TestMethod]
    [DataRow("application/pdf")]
    [DataRow("text/plain")]
    [DataRow("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [DataRow("application/zip")]
    [DataRow("application/octet-stream")]
    public void IsImmutableMedia_DocumentMimeType_ReturnsFalse(string mimeType)
    {
        Assert.IsFalse(FileStorageClassifier.IsImmutableMedia(mimeType, "document.dat"));
    }

    [TestMethod]
    [DataRow("photo.JPG")]
    [DataRow("song.mp3")]
    [DataRow("clip.MP4")]
    [DataRow("movie.mkv")]
    [DataRow("image.heic")]
    public void IsImmutableMedia_NullMime_KnownMediaExtension_ReturnsTrue(string fileName)
    {
        Assert.IsTrue(FileStorageClassifier.IsImmutableMedia(null, fileName));
    }

    [TestMethod]
    [DataRow("report.docx")]
    [DataRow("notes.txt")]
    [DataRow("archive.zip")]
    [DataRow("noextension")]
    public void IsImmutableMedia_NullMime_NonMediaExtension_ReturnsFalse(string fileName)
    {
        Assert.IsFalse(FileStorageClassifier.IsImmutableMedia(null, fileName));
    }

    [TestMethod]
    public void IsImmutableMedia_NullMimeAndFileName_ReturnsFalse()
    {
        Assert.IsFalse(FileStorageClassifier.IsImmutableMedia(null, null));
    }

    [TestMethod]
    public void IsImmutableMedia_GenericMimeButMediaExtension_ReturnsTrue()
    {
        // A client may send application/octet-stream for a known media extension.
        Assert.IsTrue(FileStorageClassifier.IsImmutableMedia("application/octet-stream", "holiday.jpeg"));
    }

    [TestMethod]
    public void IsWholeFileEligible_MediaAndOptionEnabled_ReturnsTrue()
    {
        Assert.IsTrue(FileStorageClassifier.IsWholeFileEligible("video/mp4", "clip.mp4", optionEnabled: true));
    }

    [TestMethod]
    public void IsWholeFileEligible_MediaButOptionDisabled_ReturnsFalse()
    {
        Assert.IsFalse(FileStorageClassifier.IsWholeFileEligible("video/mp4", "clip.mp4", optionEnabled: false));
    }

    [TestMethod]
    public void IsWholeFileEligible_DocumentAndOptionEnabled_ReturnsFalse()
    {
        Assert.IsFalse(FileStorageClassifier.IsWholeFileEligible("application/pdf", "doc.pdf", optionEnabled: true));
    }
}

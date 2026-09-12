using DotNetCloud.Modules.Files.UI;

namespace DotNetCloud.Modules.Files.Tests.UI;

/// <summary>
/// Tests for <see cref="FilesImageHelper"/> — the image detection and filtering used by the
/// Files browser gallery view and the preview slideshow.
/// </summary>
[TestClass]
public class FilesImageHelperTests
{
    private static FileNodeViewModel File(string name, string? mimeType = null, bool isVirtual = false)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NodeType = "File",
            MimeType = mimeType,
            IsVirtual = isVirtual
        };

    private static FileNodeViewModel Folder(string name)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NodeType = "Folder"
        };

    [TestMethod]
    public void IsImage_ImageMimeType_ReturnsTrue()
    {
        var node = File("photo", "image/jpeg");

        Assert.IsTrue(FilesImageHelper.IsImage(node));
    }

    [TestMethod]
    public void IsImage_ImageExtensionWithoutMimeType_ReturnsTrue()
    {
        var node = File("photo.png");

        Assert.IsTrue(FilesImageHelper.IsImage(node));
    }

    [TestMethod]
    public void IsImage_GenericMimeTypeWithImageExtension_ReturnsTrue()
    {
        var node = File("photo.webp", "application/octet-stream");

        Assert.IsTrue(FilesImageHelper.IsImage(node));
    }

    [TestMethod]
    public void IsImage_ExtensionIsCaseInsensitive_ReturnsTrue()
    {
        var node = File("PHOTO.JPEG");

        Assert.IsTrue(FilesImageHelper.IsImage(node));
    }

    [TestMethod]
    public void IsImage_Folder_ReturnsFalse()
    {
        Assert.IsFalse(FilesImageHelper.IsImage(Folder("Pictures")));
    }

    [TestMethod]
    public void IsImage_NonImageFile_ReturnsFalse()
    {
        Assert.IsFalse(FilesImageHelper.IsImage(File("report.pdf", "application/pdf")));
    }

    [TestMethod]
    public void IsImage_VirtualSharedItem_ReturnsFalse()
    {
        var node = File("shared-photo.jpg", "image/jpeg", isVirtual: true);

        Assert.IsFalse(FilesImageHelper.IsImage(node));
    }

    [TestMethod]
    public void IsImage_NullNode_ReturnsFalse()
    {
        Assert.IsFalse(FilesImageHelper.IsImage(null));
    }

    [TestMethod]
    public void IsImage_MimeTypeHelper_ImageMimeType_ReturnsTrue()
    {
        Assert.IsTrue(FilesImageHelper.IsImage("image/svg+xml", "icon"));
    }

    [TestMethod]
    public void IsImage_MimeTypeHelper_NonImage_ReturnsFalse()
    {
        Assert.IsFalse(FilesImageHelper.IsImage("text/plain", "notes.txt"));
    }

    [TestMethod]
    public void Filter_MixedNodes_ReturnsOnlyImagesPreservingOrder()
    {
        var first = File("a.png");
        var second = File("b.txt", "text/plain");
        var third = File("c.jpg", "image/jpeg");
        var folder = Folder("d");

        var result = FilesImageHelper.Filter([first, second, folder, third]);

        CollectionAssert.AreEqual(new[] { first.Id, third.Id }, result.Select(n => n.Id).ToArray());
    }

    [TestMethod]
    public void Filter_NoImages_ReturnsEmpty()
    {
        var result = FilesImageHelper.Filter([File("a.txt", "text/plain"), Folder("b")]);

        Assert.AreEqual(0, result.Count);
    }
}

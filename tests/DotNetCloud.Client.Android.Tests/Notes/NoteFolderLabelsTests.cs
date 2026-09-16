using DotNetCloud.Client.Android.Notes;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Tests.Notes;

/// <summary>
/// Covers the folder label resolution used by the note card tag: note folders belong to their
/// owner, so a shared note's folder is not in the caller's list and cannot be named.
/// </summary>
[TestClass]
public sealed class NoteFolderLabelsTests
{
    private static readonly Guid FolderId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

    [TestMethod]
    public void Resolve_UnfiledNote_ReturnsNull()
    {
        var label = NoteFolderLabels.Resolve(null, [CreateFolder("Work")]);

        Assert.IsNull(label);
    }

    [TestMethod]
    public void Resolve_KnownFolder_ReturnsTheFolderName()
    {
        var label = NoteFolderLabels.Resolve(FolderId, [CreateFolder("Work")]);

        Assert.AreEqual("Work", label);
    }

    [TestMethod]
    public void Resolve_FolderOutsideTheCallersList_ReturnsTheSharedFallback()
    {
        var label = NoteFolderLabels.Resolve(Guid.NewGuid(), [CreateFolder("Work")]);

        Assert.AreEqual(NoteFolderLabels.SharedFolderFallback, label);
    }

    [TestMethod]
    public void Resolve_NoFoldersLoaded_ReturnsTheSharedFallback()
    {
        var label = NoteFolderLabels.Resolve(FolderId, null);

        Assert.AreEqual(NoteFolderLabels.SharedFolderFallback, label);
    }

    [TestMethod]
    public void Resolve_BlankFolderName_ReturnsTheSharedFallback()
    {
        var label = NoteFolderLabels.Resolve(FolderId, [CreateFolder("   ")]);

        Assert.AreEqual(NoteFolderLabels.SharedFolderFallback, label);
    }

    private static NoteFolderDto CreateFolder(string name) => new()
    {
        Id = FolderId,
        OwnerId = Guid.NewGuid(),
        Name = name,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}

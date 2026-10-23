using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Music.UI;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Unit tests for the Music page folder view row builder.
/// </summary>
[TestClass]
public class MusicFolderViewTests
{
    // ── Ordering ──────────────────────────────────────────────

    [TestMethod]
    public void BuildFolderEntries_FoldersFirstThenFiles_EachAlphabetical()
    {
        var nodes = new[]
        {
            MakeFile("zebra.flac"),
            MakeFolder("B-sides"),
            MakeFile("apple.mp3"),
            MakeFolder("Albums"),
        };

        var entries = MusicPage.BuildFolderEntries(nodes, new Dictionary<Guid, TrackDto>());

        Assert.AreEqual(4, entries.Count);
        Assert.IsTrue(entries[0].IsFolder);
        Assert.AreEqual("Albums", entries[0].Name);
        Assert.IsTrue(entries[1].IsFolder);
        Assert.AreEqual("B-sides", entries[1].Name);
        Assert.IsFalse(entries[2].IsFolder);
        Assert.AreEqual("apple.mp3", entries[2].Name);
        Assert.AreEqual("zebra.flac", entries[3].Name);
    }

    [TestMethod]
    public void BuildFolderEntries_SortingIsCaseInsensitive()
    {
        var nodes = new[]
        {
            MakeFolder("brackets"),
            MakeFolder("Albums"),
        };

        var entries = MusicPage.BuildFolderEntries(nodes, new Dictionary<Guid, TrackDto>());

        Assert.AreEqual("Albums", entries[0].Name);
        Assert.AreEqual("brackets", entries[1].Name);
    }

    // ── Track resolution ──────────────────────────────────────

    [TestMethod]
    public void BuildFolderEntries_IndexedFile_GetsTrack()
    {
        var file = MakeFile("song.flac");
        var track = MakeTrack(file.Id);

        var entries = MusicPage.BuildFolderEntries(
            [file],
            new Dictionary<Guid, TrackDto> { [file.Id] = track });

        Assert.AreEqual(1, entries.Count);
        Assert.IsNotNull(entries[0].Track);
        Assert.AreEqual(track.Id, entries[0].Track!.Id);
    }

    [TestMethod]
    public void BuildFolderEntries_UnindexedFile_HasNoTrack()
    {
        var entries = MusicPage.BuildFolderEntries([MakeFile("cover.jpg")], new Dictionary<Guid, TrackDto>());

        Assert.AreEqual(1, entries.Count);
        Assert.IsNull(entries[0].Track);
    }

    [TestMethod]
    public void BuildFolderEntries_Folder_NeverHasTrack()
    {
        var folder = MakeFolder("Album");
        var bogusTrack = MakeTrack(folder.Id);

        var entries = MusicPage.BuildFolderEntries(
            [folder],
            new Dictionary<Guid, TrackDto> { [folder.Id] = bogusTrack });

        Assert.AreEqual(1, entries.Count);
        Assert.IsTrue(entries[0].IsFolder);
        Assert.IsNull(entries[0].Track);
    }

    [TestMethod]
    public void BuildFolderEntries_CopiesNodeMetadata()
    {
        var file = MakeFile("song.flac", size: 12_345_678);

        var entries = MusicPage.BuildFolderEntries([file], new Dictionary<Guid, TrackDto>());

        Assert.AreEqual(file.Id, entries[0].Id);
        Assert.AreEqual(file.MimeType, entries[0].MimeType);
        Assert.AreEqual(12_345_678, entries[0].Size);
        Assert.AreEqual(file.UpdatedAt, entries[0].UpdatedAt);
    }

    [TestMethod]
    public void BuildFolderEntries_EmptyInput_ReturnsEmpty()
    {
        var entries = MusicPage.BuildFolderEntries([], new Dictionary<Guid, TrackDto>());

        Assert.AreEqual(0, entries.Count);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static FileNodeDto MakeFolder(string name) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        NodeType = "Folder",
        OwnerId = Guid.CreateVersion7(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static FileNodeDto MakeFile(string name, long size = 1024) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        NodeType = "File",
        MimeType = "audio/flac",
        Size = size,
        OwnerId = Guid.CreateVersion7(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static TrackDto MakeTrack(Guid fileNodeId) => new()
    {
        Id = Guid.CreateVersion7(),
        OwnerId = Guid.CreateVersion7(),
        FileNodeId = fileNodeId,
        Title = "Song",
        Duration = TimeSpan.FromMinutes(3),
        MimeType = "audio/flac",
        ArtistId = Guid.CreateVersion7(),
        ArtistName = "Artist",
        CreatedAt = DateTime.UtcNow,
    };
}

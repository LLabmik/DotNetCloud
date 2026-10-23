using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.UI;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Unit tests for the Music page album art lightbox.
/// </summary>
[TestClass]
public class MusicPageAlbumArtModalTests
{
    [TestMethod]
    public void OpenAlbumArtModal_AlbumWithCover_OpensModalForAlbum()
    {
        var page = new MusicPage();
        var album = MakeAlbum(hasCoverArt: true);

        page.OpenAlbumArtModal(album);

        Assert.IsTrue(page.IsAlbumArtModalOpen);
        Assert.AreEqual(album.Id, page.AlbumArtModalAlbumId);
    }

    [TestMethod]
    public void OpenAlbumArtModal_AlbumWithoutCover_DoesNotOpen()
    {
        var page = new MusicPage();
        var album = MakeAlbum(hasCoverArt: false);

        page.OpenAlbumArtModal(album);

        Assert.IsFalse(page.IsAlbumArtModalOpen);
    }

    [TestMethod]
    public void OpenAlbumArtModal_SecondAlbum_ReplacesAlbum()
    {
        var page = new MusicPage();
        var first = MakeAlbum(hasCoverArt: true);
        var second = MakeAlbum(hasCoverArt: true);

        page.OpenAlbumArtModal(first);
        page.OpenAlbumArtModal(second);

        Assert.IsTrue(page.IsAlbumArtModalOpen);
        Assert.AreEqual(second.Id, page.AlbumArtModalAlbumId);
    }

    [TestMethod]
    public void CloseAlbumArtModal_AfterOpen_Closes()
    {
        var page = new MusicPage();
        page.OpenAlbumArtModal(MakeAlbum(hasCoverArt: true));

        page.CloseAlbumArtModal();

        Assert.IsFalse(page.IsAlbumArtModalOpen);
    }

    [TestMethod]
    public void CloseAlbumArtModal_WhenClosed_StaysClosed()
    {
        var page = new MusicPage();

        page.CloseAlbumArtModal();

        Assert.IsFalse(page.IsAlbumArtModalOpen);
    }

    private static MusicAlbumDto MakeAlbum(bool hasCoverArt) => new()
    {
        Id = Guid.CreateVersion7(),
        Title = "Test Album",
        ArtistId = Guid.CreateVersion7(),
        ArtistName = "Test Artist",
        HasCoverArt = hasCoverArt,
        CreatedAt = DateTime.UtcNow,
    };
}

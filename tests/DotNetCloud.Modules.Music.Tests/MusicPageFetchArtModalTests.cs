using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.UI;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Unit tests for the manual cover-art search flow of the Music page.
/// </summary>
[TestClass]
public class MusicPageFetchArtModalTests
{
    [TestMethod]
    public void ShowFetchArtModal_Album_TargetsThatAlbum()
    {
        var page = new MusicPage();
        var album = MakeAlbum();

        page.ShowFetchArtModal(album);

        Assert.IsTrue(page.IsFetchArtModalOpen);
        Assert.AreEqual(album.Id, page.FetchArtAlbumId,
            "The modal must remember the album it was opened for so the refresh after applying art targets it.");
    }

    [TestMethod]
    public void ShowFetchArtModal_SecondAlbum_RetargetsModal()
    {
        var page = new MusicPage();
        var first = MakeAlbum();
        var second = MakeAlbum();

        page.ShowFetchArtModal(first);
        page.ShowFetchArtModal(second);

        Assert.IsTrue(page.IsFetchArtModalOpen);
        Assert.AreEqual(second.Id, page.FetchArtAlbumId);
    }

    private static MusicAlbumDto MakeAlbum() => new()
    {
        Id = Guid.CreateVersion7(),
        Title = "Test Album",
        ArtistId = Guid.CreateVersion7(),
        ArtistName = "Test Artist",
        Year = 1973,
        HasCoverArt = false,
        CreatedAt = DateTime.UtcNow,
    };
}

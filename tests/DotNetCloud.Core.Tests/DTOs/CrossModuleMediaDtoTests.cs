using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Tests.DTOs;

[TestClass]
public class CrossModuleMediaDtoTests
{
    [TestMethod]
    public void MediaSearchResultDto_TotalCount_SumsAllCollections()
    {
        var dto = new MediaSearchResultDto
        {
            Photos = new List<PhotoDto>
            {
                CreatePhoto(), CreatePhoto()
            },
            Tracks = new List<TrackDto>
            {
                CreateTrack()
            },
            Albums = new List<MusicAlbumDto>
            {
                CreateMusicAlbum(), CreateMusicAlbum(), CreateMusicAlbum()
            },
            Artists = new List<ArtistDto>(),
            Videos = new List<VideoDto>
            {
                CreateVideo()
            }
        };

        Assert.AreEqual(7, dto.TotalCount);
    }

    [TestMethod]
    public void MediaSearchResultDto_TotalCount_ZeroWhenEmpty()
    {
        var dto = new MediaSearchResultDto
        {
            Photos = Array.Empty<PhotoDto>(),
            Tracks = Array.Empty<TrackDto>(),
            Albums = Array.Empty<MusicAlbumDto>(),
            Artists = Array.Empty<ArtistDto>(),
            Videos = Array.Empty<VideoDto>()
        };

        Assert.AreEqual(0, dto.TotalCount);
    }

    [TestMethod]
    public void VideoContinueWatchingDto_ProgressPercent_CalculatesCorrectly()
    {
        var dto = new VideoContinueWatchingDto
        {
            VideoId = Guid.CreateVersion7(),
            Title = "Test",
            FileName = "test.mp4",
            Duration = TimeSpan.FromMinutes(100),
            WatchPosition = TimeSpan.FromMinutes(50),
            LastWatchedAt = DateTime.UtcNow
        };

        Assert.AreEqual(0.5, dto.ProgressPercent, 0.001);
    }

    [TestMethod]
    public void VideoContinueWatchingDto_ProgressPercent_CapsAtOne()
    {
        var dto = new VideoContinueWatchingDto
        {
            VideoId = Guid.CreateVersion7(),
            Title = "Test",
            FileName = "test.mp4",
            Duration = TimeSpan.FromMinutes(10),
            WatchPosition = TimeSpan.FromMinutes(15),
            LastWatchedAt = DateTime.UtcNow
        };

        Assert.AreEqual(1.0, dto.ProgressPercent, 0.001);
    }

    [TestMethod]
    public void VideoContinueWatchingDto_ProgressPercent_ZeroForZeroDuration()
    {
        var dto = new VideoContinueWatchingDto
        {
            VideoId = Guid.CreateVersion7(),
            Title = "Test",
            FileName = "test.mp4",
            Duration = TimeSpan.Zero,
            WatchPosition = TimeSpan.FromMinutes(5),
            LastWatchedAt = DateTime.UtcNow
        };

        Assert.AreEqual(0.0, dto.ProgressPercent);
    }

    [TestMethod]
    public void VideoContinueWatchingDto_ProgressPercent_ZeroWhenNotStarted()
    {
        var dto = new VideoContinueWatchingDto
        {
            VideoId = Guid.CreateVersion7(),
            Title = "Test",
            FileName = "test.mp4",
            Duration = TimeSpan.FromMinutes(90),
            WatchPosition = TimeSpan.Zero,
            LastWatchedAt = DateTime.UtcNow
        };

        Assert.AreEqual(0.0, dto.ProgressPercent, 0.001);
    }

    [TestMethod]
    public void VideoContinueWatchingDto_ProgressPercent_NearComplete()
    {
        var dto = new VideoContinueWatchingDto
        {
            VideoId = Guid.CreateVersion7(),
            Title = "Test",
            FileName = "test.mp4",
            Duration = TimeSpan.FromSeconds(100),
            WatchPosition = TimeSpan.FromSeconds(99),
            LastWatchedAt = DateTime.UtcNow
        };

        Assert.AreEqual(0.99, dto.ProgressPercent, 0.001);
    }

    [TestMethod]
    public void RecentMediaItemDto_RequiredProperties_AreInitialized()
    {
        var id = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        var dto = new RecentMediaItemDto
        {
            MediaType = "Photo",
            Id = id,
            Title = "sunset.jpg",
            AddedAt = now
        };

        Assert.AreEqual("Photo", dto.MediaType);
        Assert.AreEqual(id, dto.Id);
        Assert.AreEqual("sunset.jpg", dto.Title);
        Assert.AreEqual(now, dto.AddedAt);
    }

    [TestMethod]
    public void MediaDashboardDto_RequiredProperties_AreInitialized()
    {
        var dto = new MediaDashboardDto
        {
            RecentPhotos = Array.Empty<PhotoDto>(),
            RecentlyPlayed = Array.Empty<TrackDto>(),
            ContinueWatching = Array.Empty<VideoContinueWatchingDto>(),
            RecentlyAdded = Array.Empty<RecentMediaItemDto>()
        };

        Assert.IsNotNull(dto.RecentPhotos);
        Assert.IsNotNull(dto.RecentlyPlayed);
        Assert.IsNotNull(dto.ContinueWatching);
        Assert.IsNotNull(dto.RecentlyAdded);
        Assert.AreEqual(0, dto.RecentPhotos.Count);
    }

    [TestMethod]
    public void CrossModuleLinkType_HasAllMediaTypes()
    {
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.Photo));
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.PhotoAlbum));
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.MusicTrack));
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.MusicAlbum));
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.MusicArtist));
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.Playlist));
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.Video));
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.VideoCollection));
    }

    [TestMethod]
    public void CrossModuleLinkType_HasOriginalTypes()
    {
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.File));
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.CalendarEvent));
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.Contact));
        Assert.IsTrue(Enum.IsDefined(typeof(CrossModuleLinkType), CrossModuleLinkType.Note));
    }

    [TestMethod]
    public void CrossModuleLinkDto_ConstructionWithMediaTypes_Works()
    {
        var dto = new CrossModuleLinkDto
        {
            LinkType = CrossModuleLinkType.MusicTrack,
            TargetId = Guid.CreateVersion7(),
            DisplayLabel = "Bohemian Rhapsody",
            Href = "/music/tracks/abc123"
        };

        Assert.AreEqual(CrossModuleLinkType.MusicTrack, dto.LinkType);
        Assert.AreEqual("Bohemian Rhapsody", dto.DisplayLabel);
        Assert.IsTrue(dto.IsResolved);
    }

    [TestMethod]
    public void CrossModuleLinkRequest_WithVideoType_Constructs()
    {
        var targetId = Guid.CreateVersion7();
        var request = new CrossModuleLinkRequest
        {
            LinkType = CrossModuleLinkType.VideoCollection,
            TargetId = targetId
        };

        Assert.AreEqual(CrossModuleLinkType.VideoCollection, request.LinkType);
        Assert.AreEqual(targetId, request.TargetId);
    }

    [TestMethod]
    public void MediaSearchResultDto_WithMixedResults_CountsCorrectly()
    {
        var dto = new MediaSearchResultDto
        {
            Photos = new[] { CreatePhoto() },
            Tracks = new[] { CreateTrack(), CreateTrack(), CreateTrack() },
            Albums = Array.Empty<MusicAlbumDto>(),
            Artists = new[] { CreateArtist(), CreateArtist() },
            Videos = new[] { CreateVideo(), CreateVideo() }
        };

        Assert.AreEqual(8, dto.TotalCount);
    }

    private static PhotoDto CreatePhoto() => new()
    {
        Id = Guid.CreateVersion7(),
        FileNodeId = Guid.CreateVersion7(),
        OwnerId = Guid.CreateVersion7(),
        FileName = "photo.jpg",
        MimeType = "image/jpeg",
        TakenAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };

    private static TrackDto CreateTrack() => new()
    {
        Id = Guid.CreateVersion7(),
        FileNodeId = Guid.CreateVersion7(),
        Title = "Track",
        MimeType = "audio/mpeg",
        TrackNumber = 1,
        ArtistId = Guid.CreateVersion7(),
        ArtistName = "Test Artist",
        OwnerId = Guid.CreateVersion7(),
        CreatedAt = DateTime.UtcNow
    };

    private static MusicAlbumDto CreateMusicAlbum() => new()
    {
        Id = Guid.CreateVersion7(),
        Title = "Album",
        ArtistId = Guid.CreateVersion7(),
        ArtistName = "Artist",
        CreatedAt = DateTime.UtcNow
    };

    private static ArtistDto CreateArtist() => new()
    {
        Id = Guid.CreateVersion7(),
        Name = "Artist",
        CreatedAt = DateTime.UtcNow
    };

    private static VideoDto CreateVideo() => new()
    {
        Id = Guid.CreateVersion7(),
        FileNodeId = Guid.CreateVersion7(),
        Title = "Video",
        FileName = "video.mp4",
        MimeType = "video/mp4",
        OwnerId = Guid.CreateVersion7(),
        CreatedAt = DateTime.UtcNow
    };
}

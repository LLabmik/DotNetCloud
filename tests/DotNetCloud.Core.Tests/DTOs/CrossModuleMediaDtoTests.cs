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
            VideoId = Guid.NewGuid(),
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
            VideoId = Guid.NewGuid(),
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
            VideoId = Guid.NewGuid(),
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
            VideoId = Guid.NewGuid(),
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
            VideoId = Guid.NewGuid(),
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
        var id = Guid.NewGuid();
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
            TargetId = Guid.NewGuid(),
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
        var targetId = Guid.NewGuid();
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
        Id = Guid.NewGuid(),
        FileNodeId = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
        FileName = "photo.jpg",
        MimeType = "image/jpeg",
        TakenAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };

    private static TrackDto CreateTrack() => new()
    {
        Id = Guid.NewGuid(),
        FileNodeId = Guid.NewGuid(),
        Title = "Track",
        MimeType = "audio/mpeg",
        TrackNumber = 1,
        ArtistId = Guid.NewGuid(),
        ArtistName = "Test Artist",
        CreatedAt = DateTime.UtcNow
    };

    private static MusicAlbumDto CreateMusicAlbum() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Album",
        ArtistId = Guid.NewGuid(),
        ArtistName = "Artist",
        CreatedAt = DateTime.UtcNow
    };

    private static ArtistDto CreateArtist() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Artist",
        CreatedAt = DateTime.UtcNow
    };

    private static VideoDto CreateVideo() => new()
    {
        Id = Guid.NewGuid(),
        FileNodeId = Guid.NewGuid(),
        Title = "Video",
        FileName = "video.mp4",
        MimeType = "video/mp4",
        CreatedAt = DateTime.UtcNow
    };
}

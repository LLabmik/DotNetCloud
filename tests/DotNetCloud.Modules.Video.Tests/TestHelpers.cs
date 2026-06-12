using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using VideoModel = DotNetCloud.Modules.Video.Models.Video;
using CanonicalVideoModel = DotNetCloud.Modules.Video.Models.CanonicalVideo;

namespace DotNetCloud.Modules.Video.Tests;

/// <summary>
/// Shared helpers for Video module service tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>Creates a fresh InMemory VideoDbContext.</summary>
    public static VideoDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        return new VideoDbContext(options);
    }

    /// <summary>Creates a CallerContext for a user.</summary>
    public static CallerContext CreateCaller(Guid? userId = null)
        => new(userId ?? Guid.CreateVersion7(), ["user"], CallerType.User);

    /// <summary>Seeds a video in the database (legacy + canonical).</summary>
    public static async Task<VideoModel> SeedVideoAsync(
        VideoDbContext db,
        string title = "Test Video",
        string mimeType = "video/mp4",
        long sizeBytes = 500_000_000,
        Guid? ownerId = null)
    {
        var owner = ownerId ?? Guid.CreateVersion7();
        var video = new VideoModel
        {
            FileNodeId = Guid.CreateVersion7(),
            OwnerId = owner,
            Title = title,
            FileName = $"{title.Replace(' ', '_').ToLowerInvariant()}.mp4",
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            DurationTicks = TimeSpan.FromMinutes(90).Ticks
        };
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        // Also seed canonical data so services using the canonical path can find this video
        var contentHash = Guid.CreateVersion7().ToString("N");
        if (!db.CanonicalVideos.Any(cv => cv.ContentHash == contentHash))
        {
            db.CanonicalVideos.Add(new CanonicalVideoModel
            {
                ContentHash = contentHash,
                Title = title,
                FileName = video.FileName,
                MimeType = mimeType,
                SizeBytes = sizeBytes,
                DurationTicks = video.DurationTicks
            });
            await db.SaveChangesAsync();
        }

        if (!db.UserVideos.Any(uv => uv.Id == video.Id))
        {
            db.UserVideos.Add(new UserVideo
            {
                Id = video.Id,
                OwnerId = owner,
                FileNodeId = video.FileNodeId,
                CanonicalContentHash = contentHash
            });
            await db.SaveChangesAsync();
        }

        return video;
    }

    /// <summary>Seeds a video collection in the database (new per-user model).</summary>
    public static async Task<UserVideoCollection> SeedCollectionAsync(
        VideoDbContext db,
        string name = "Test Collection",
        Guid? ownerId = null)
    {
        var collection = new UserVideoCollection
        {
            OwnerId = ownerId ?? Guid.CreateVersion7(),
            Name = name,
            Description = "A test collection"
        };
        db.UserVideoCollections.Add(collection);
        await db.SaveChangesAsync();
        return collection;
    }

    /// <summary>Seeds a subtitle for a video.</summary>
    public static async Task<Subtitle> SeedSubtitleAsync(
        VideoDbContext db,
        Guid videoId,
        string language = "en",
        string format = "srt",
        Guid? ownerId = null)
    {
        var subtitle = new Subtitle
        {
            VideoId = videoId,
            Language = language,
            Label = $"{language} subtitle",
            Format = format,
            Content = "1\n00:00:01,000 --> 00:00:04,000\nHello World\n"
        };
        db.Subtitles.Add(subtitle);
        await db.SaveChangesAsync();
        return subtitle;
    }

    /// <summary>Seeds a watch progress entry.</summary>
    public static async Task<WatchProgress> SeedWatchProgressAsync(
        VideoDbContext db,
        Guid videoId,
        Guid userId,
        long positionTicks = 0,
        bool isCompleted = false)
    {
        var progress = new WatchProgress
        {
            VideoId = videoId,
            UserId = userId,
            PositionTicks = positionTicks,
            IsCompleted = isCompleted
        };
        db.WatchProgresses.Add(progress);
        await db.SaveChangesAsync();
        return progress;
    }

    /// <summary>Seeds a video with metadata for complete testing (legacy + canonical).</summary>
    public static async Task<(VideoModel Video, VideoMetadata Metadata)> SeedCompleteVideoAsync(
        VideoDbContext db,
        string title = "Complete Video",
        Guid? ownerId = null)
    {
        var video = await SeedVideoAsync(db, title, ownerId: ownerId);
        var metadata = new VideoMetadata
        {
            VideoId = video.Id,
            Width = 1920,
            Height = 1080,
            FrameRate = 24.0,
            VideoCodec = "H.264",
            AudioCodec = "AAC",
            Bitrate = 8_000_000,
            AudioTrackCount = 2,
            SubtitleTrackCount = 1,
            ContainerFormat = "MP4"
        };
        db.VideoMetadata.Add(metadata);

        // Also seed canonical metadata so services using the canonical path can find it
        var userVideo = await db.UserVideos.FirstOrDefaultAsync(uv => uv.Id == video.Id);
        if (userVideo is not null)
        {
            var contentHash = userVideo.CanonicalContentHash;
            if (!db.CanonicalVideoMetadata.Any(cm => cm.VideoContentHash == contentHash))
            {
                db.CanonicalVideoMetadata.Add(new CanonicalVideoMetadata
                {
                    VideoContentHash = contentHash,
                    Width = 1920,
                    Height = 1080,
                    FrameRate = 24.0,
                    VideoCodec = "H.264",
                    AudioCodec = "AAC",
                    Bitrate = 8_000_000,
                    AudioTrackCount = 2,
                    SubtitleTrackCount = 1,
                    ContainerFormat = "MP4",
                    ExtractedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();
        return (video, metadata);
    }

    /// <summary>Seeds a canonical video + user video junction.</summary>
    public static async Task<(CanonicalVideoModel Canonical, UserVideo UserVideo)> SeedCanonicalVideoAsync(
        VideoDbContext db,
        string title = "Canonical Video",
        string contentHash = "abc123def456",
        Guid? ownerId = null,
        long sizeBytes = 500_000_000,
        TimeSpan? duration = null)
    {
        var canonical = new CanonicalVideoModel
        {
            ContentHash = contentHash,
            Title = title,
            FileName = $"{title.Replace(' ', '_').ToLowerInvariant()}.mp4",
            MimeType = "video/mp4",
            SizeBytes = sizeBytes,
            DurationTicks = (duration ?? TimeSpan.FromMinutes(90)).Ticks,
            HasExternalPoster = false
        };
        db.CanonicalVideos.Add(canonical);
        await db.SaveChangesAsync();

        var userVideo = new UserVideo
        {
            OwnerId = ownerId ?? Guid.CreateVersion7(),
            FileNodeId = Guid.CreateVersion7(),
            CanonicalContentHash = contentHash
        };
        db.UserVideos.Add(userVideo);
        await db.SaveChangesAsync();

        return (canonical, userVideo);
    }

    /// <summary>Seeds a canonical TV series with one season and one episode linked to a canonical video.</summary>
    public static async Task<(CanonicalVideoSeries Series, CanonicalVideoSeason Season, CanonicalVideoEpisode Episode)> SeedCanonicalSeriesWithEpisodeAsync(
        VideoDbContext db,
        string seriesName = "Test Series",
        string videoContentHash = "abc123def456",
        int seasonNumber = 1,
        int episodeNumber = 1)
    {
        var series = new CanonicalVideoSeries
        {
            Name = seriesName,
            Type = SeriesType.TvSeries,
            TotalSeasons = 1,
            TotalEpisodes = 1
        };
        db.CanonicalVideoSeries.Add(series);
        await db.SaveChangesAsync();

        var season = new CanonicalVideoSeason
        {
            SeriesId = series.Id,
            SeasonNumber = seasonNumber,
            Name = $"Season {seasonNumber}",
            EpisodeCount = 1
        };
        db.CanonicalVideoSeasons.Add(season);
        await db.SaveChangesAsync();

        var episode = new CanonicalVideoEpisode
        {
            SeasonId = season.Id,
            VideoContentHash = videoContentHash,
            EpisodeNumber = episodeNumber,
            Title = $"Episode {episodeNumber}",
            SortOrder = episodeNumber
        };
        db.CanonicalVideoEpisodes.Add(episode);
        await db.SaveChangesAsync();

        return (series, season, episode);
    }
}

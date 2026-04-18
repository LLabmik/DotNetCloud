using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using VideoModel = DotNetCloud.Modules.Video.Models.Video;

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
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new VideoDbContext(options);
    }

    /// <summary>Creates a CallerContext for a user.</summary>
    public static CallerContext CreateCaller(Guid? userId = null)
        => new(userId ?? Guid.NewGuid(), ["user"], CallerType.User);

    /// <summary>Seeds a video in the database.</summary>
    public static async Task<VideoModel> SeedVideoAsync(
        VideoDbContext db,
        string title = "Test Video",
        string mimeType = "video/mp4",
        long sizeBytes = 500_000_000,
        Guid? ownerId = null)
    {
        var video = new VideoModel
        {
            FileNodeId = Guid.NewGuid(),
            OwnerId = ownerId ?? Guid.NewGuid(),
            Title = title,
            FileName = $"{title.Replace(' ', '_').ToLowerInvariant()}.mp4",
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            DurationTicks = TimeSpan.FromMinutes(90).Ticks
        };
        db.Videos.Add(video);
        await db.SaveChangesAsync();
        return video;
    }

    /// <summary>Seeds a video collection in the database.</summary>
    public static async Task<VideoCollection> SeedCollectionAsync(
        VideoDbContext db,
        string name = "Test Collection",
        Guid? ownerId = null)
    {
        var collection = new VideoCollection
        {
            OwnerId = ownerId ?? Guid.NewGuid(),
            Name = name,
            Description = "A test collection"
        };
        db.VideoCollections.Add(collection);
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

    /// <summary>Seeds a video with metadata for complete testing.</summary>
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
        await db.SaveChangesAsync();
        return (video, metadata);
    }
}

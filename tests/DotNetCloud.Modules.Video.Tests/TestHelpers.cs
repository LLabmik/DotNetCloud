using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;

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

    /// <summary>Seeds a video in the database (canonical + user junction).</summary>
    public static async Task<UserVideo> SeedVideoAsync(
        VideoDbContext db,
        string title = "Test Video",
        string mimeType = "video/mp4",
        long sizeBytes = 500_000_000,
        Guid? ownerId = null)
    {
        var owner = ownerId ?? Guid.CreateVersion7();
        var contentHash = Guid.CreateVersion7().ToString("N");

        var canonical = new CanonicalVideo
        {
            ContentHash = contentHash,
            Title = title,
            FileName = $"{title.Replace(' ', '_').ToLowerInvariant()}.mp4",
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            DurationTicks = TimeSpan.FromMinutes(90).Ticks
        };
        db.CanonicalVideos.Add(canonical);
        await db.SaveChangesAsync();

        var userVideo = new UserVideo
        {
            OwnerId = owner,
            FileNodeId = Guid.CreateVersion7(),
            CanonicalContentHash = contentHash
        };
        db.UserVideos.Add(userVideo);
        await db.SaveChangesAsync();

        return userVideo;
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

    /// <summary>Seeds a canonical subtitle for a video content hash.</summary>
    public static async Task<CanonicalSubtitle> SeedSubtitleAsync(
        VideoDbContext db,
        string contentHash,
        string language = "en",
        string format = "srt")
    {
        var subtitle = new CanonicalSubtitle
        {
            VideoContentHash = contentHash,
            Language = language,
            Label = $"{language} subtitle",
            Format = format,
            Content = "1\n00:00:01,000 --> 00:00:04,000\nHello World\n"
        };
        db.CanonicalSubtitles.Add(subtitle);
        await db.SaveChangesAsync();
        return subtitle;
    }

    /// <summary>Seeds a video with metadata for complete testing (canonical).</summary>
    public static async Task<(UserVideo Video, CanonicalVideoMetadata Metadata)> SeedCompleteVideoAsync(
        VideoDbContext db,
        string title = "Complete Video",
        Guid? ownerId = null)
    {
        var video = await SeedVideoAsync(db, title, ownerId: ownerId);
        var contentHash = video.CanonicalContentHash;

        var metadata = new CanonicalVideoMetadata
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
        };
        db.CanonicalVideoMetadata.Add(metadata);
        await db.SaveChangesAsync();

        return (video, metadata);
    }

    /// <summary>Seeds a canonical video + user video junction.</summary>
    public static async Task<(CanonicalVideo Canonical, UserVideo UserVideo)> SeedCanonicalVideoAsync(
        VideoDbContext db,
        string title = "Canonical Video",
        string contentHash = "abc123def456",
        Guid? ownerId = null,
        long sizeBytes = 500_000_000,
        TimeSpan? duration = null)
    {
        var canonical = new CanonicalVideo
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

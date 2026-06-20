using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class VideoSeriesServiceTests
{
    private VideoDbContext _db = null!;
    private VideoSeriesService _service = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        var configMock = new Mock<IConfiguration>();
        configMock
            .Setup(c => c["Files:Storage:RootPath"])
            .Returns(System.IO.Path.GetTempPath());
        _service = new VideoSeriesService(_db, Mock.Of<ITmdbClient>(), Mock.Of<ILogger<VideoSeriesService>>(), configMock.Object);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── GetSeasonEpisodesAsync ─────────────────────────────────────

    [TestMethod]
    public async Task GetSeasonEpisodesAsync_CanonicalPath_ReturnsEpisodesWithVideo()
    {
        // Arrange
        var caller = TestHelpers.CreateCaller();
        var contentHash = "episode1hash";
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Episode Video", contentHash, caller.UserId);
        var (_, season, _) = await TestHelpers.SeedCanonicalSeriesWithEpisodeAsync(_db, videoContentHash: contentHash);

        // Act
        var episodes = await _service.GetSeasonEpisodesAsync(season.Id, caller);

        // Assert
        Assert.AreEqual(1, episodes.Count);
        var episode = episodes[0];
        Assert.IsNotNull(episode.Video, "Video DTO should not be null in canonical path");
        Assert.AreEqual("Episode Video", episode.Video.Title);
        Assert.AreNotEqual(Guid.Empty, episode.VideoId, "VideoId should not be Guid.Empty");
    }

    [TestMethod]
    public async Task GetSeasonEpisodesAsync_CanonicalPath_MapsCorrectVideoFields()
    {
        // Arrange
        var caller = TestHelpers.CreateCaller();
        var contentHash = "videohash789";
        var duration = TimeSpan.FromMinutes(45);
        await TestHelpers.SeedCanonicalVideoAsync(_db, "My Episode", contentHash, caller.UserId, sizeBytes: 200_000_000, duration: duration);
        var (_, season, _) = await TestHelpers.SeedCanonicalSeriesWithEpisodeAsync(_db, videoContentHash: contentHash);

        // Act
        var episodes = await _service.GetSeasonEpisodesAsync(season.Id, caller);

        // Assert
        var episode = episodes[0];
        Assert.IsNotNull(episode.Video);
        Assert.AreEqual("My Episode", episode.Video.Title);
        Assert.AreEqual(200_000_000, episode.Video.SizeBytes);
        Assert.AreEqual(duration, episode.Video.Duration);
        Assert.AreEqual("video/mp4", episode.Video.MimeType);
    }

    [TestMethod]
    public async Task GetSeasonEpisodesAsync_CanonicalPath_VideoIdMatchesUserVideo()
    {
        // Arrange
        var caller = TestHelpers.CreateCaller();
        var contentHash = "matchhash";
        var (canonical, userVideo) = await TestHelpers.SeedCanonicalVideoAsync(_db, "Match Video", contentHash, caller.UserId);
        var (_, season, _) = await TestHelpers.SeedCanonicalSeriesWithEpisodeAsync(_db, videoContentHash: contentHash);

        // Act
        var episodes = await _service.GetSeasonEpisodesAsync(season.Id, caller);

        // Assert
        var episode = episodes[0];
        Assert.AreEqual(userVideo.Id, episode.VideoId, "VideoId should match the UserVideo.Id");
        Assert.IsNotNull(episode.Video);
        Assert.AreEqual(userVideo.Id, episode.Video.Id);
    }

    [TestMethod]
    public async Task GetSeasonEpisodesAsync_CanonicalPath_OnlyOwnersVideos()
    {
        // Arrange
        var caller = TestHelpers.CreateCaller();
        var otherUser = TestHelpers.CreateCaller();
        var contentHash = "otheruserhash";
        // Seed the video but owned by a different user
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Other's Video", contentHash, otherUser.UserId);
        var (_, season, _) = await TestHelpers.SeedCanonicalSeriesWithEpisodeAsync(_db, videoContentHash: contentHash);

        // Act
        var episodes = await _service.GetSeasonEpisodesAsync(season.Id, caller);

        // Assert — episodes without a matching UserVideo for the caller are now filtered out
        Assert.AreEqual(0, episodes.Count, "Episodes without a matching UserVideo should be excluded");
    }

    [TestMethod]
    public async Task GetSeasonEpisodesAsync_CanonicalPath_MultipleEpisodes_AllMapped()
    {
        // Arrange
        var caller = TestHelpers.CreateCaller();
        var hash1 = "hash1";
        var hash2 = "hash2";
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Video 1", hash1, caller.UserId);
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Video 2", hash2, caller.UserId);

        var series = new Models.CanonicalVideoSeries
        {
            Name = "Multi Episode Series",
            Type = Models.SeriesType.TvSeries,
            TotalSeasons = 1,
            TotalEpisodes = 2
        };
        _db.CanonicalVideoSeries.Add(series);
        await _db.SaveChangesAsync();

        var season = new Models.CanonicalVideoSeason
        {
            SeriesId = series.Id,
            SeasonNumber = 1,
            Name = "Season 1",
            EpisodeCount = 2
        };
        _db.CanonicalVideoSeasons.Add(season);
        await _db.SaveChangesAsync();

        _db.CanonicalVideoEpisodes.AddRange(
            new Models.CanonicalVideoEpisode { SeasonId = season.Id, VideoContentHash = hash1, EpisodeNumber = 1, Title = "Ep 1", SortOrder = 1 },
            new Models.CanonicalVideoEpisode { SeasonId = season.Id, VideoContentHash = hash2, EpisodeNumber = 2, Title = "Ep 2", SortOrder = 2 }
        );
        await _db.SaveChangesAsync();

        // Act
        var episodes = await _service.GetSeasonEpisodesAsync(season.Id, caller);

        // Assert
        Assert.AreEqual(2, episodes.Count);
        Assert.IsNotNull(episodes[0].Video);
        Assert.IsNotNull(episodes[1].Video);
        Assert.AreEqual("Video 1", episodes[0].Video!.Title);
        Assert.AreEqual("Video 2", episodes[1].Video!.Title);
        Assert.AreEqual(1, episodes[0].EpisodeNumber);
        Assert.AreEqual(2, episodes[1].EpisodeNumber);
    }

    [TestMethod]
    public async Task GetSeasonEpisodesAsync_NoEpisodes_ReturnsEmpty()
    {
        // Arrange
        var series = new Models.CanonicalVideoSeries
        {
            Name = "Empty Series",
            Type = Models.SeriesType.TvSeries
        };
        _db.CanonicalVideoSeries.Add(series);
        await _db.SaveChangesAsync();

        var season = new Models.CanonicalVideoSeason
        {
            SeriesId = series.Id,
            SeasonNumber = 1,
            EpisodeCount = 0
        };
        _db.CanonicalVideoSeasons.Add(season);
        await _db.SaveChangesAsync();

        // Act
        var episodes = await _service.GetSeasonEpisodesAsync(season.Id, _caller);

        // Assert
        Assert.AreEqual(0, episodes.Count);
    }

    [TestMethod]
    public async Task GetSeasonEpisodesAsync_NonExistentSeason_ReturnsEmpty()
    {
        // Act
        var episodes = await _service.GetSeasonEpisodesAsync(Guid.CreateVersion7(), _caller);

        // Assert
        Assert.AreEqual(0, episodes.Count);
    }

    // ─── GetSeriesThumbnailAsync ────────────────────────────────────

    [TestMethod]
    public async Task GetSeriesThumbnailAsync_NoPosterHash_ReturnsNull()
    {
        // Arrange
        var series = new Models.CanonicalVideoSeries
        {
            Name = "No Poster Series",
            Type = Models.SeriesType.TvSeries,
            PosterHash = null
        };
        _db.CanonicalVideoSeries.Add(series);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.GetSeriesThumbnailAsync(series.Id, _caller);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetSeriesThumbnailAsync_WithPosterHashNoFile_ReturnsNull()
    {
        // Arrange
        var series = new Models.CanonicalVideoSeries
        {
            Name = "Missing Poster Series",
            Type = Models.SeriesType.TvSeries,
            PosterHash = "nonexistent_hash_12345"
        };
        _db.CanonicalVideoSeries.Add(series);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.GetSeriesThumbnailAsync(series.Id, _caller);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetSeriesThumbnailAsync_NonExistentSeries_ReturnsNull()
    {
        // Act
        var result = await _service.GetSeriesThumbnailAsync(Guid.CreateVersion7(), _caller);

        // Assert
        Assert.IsNull(result);
    }
}

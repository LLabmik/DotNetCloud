using DotNetCloud.Modules.Video.UI;

namespace DotNetCloud.Modules.Video.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoPage.ComputeNextEpisodeIndex"/> — the pure helper used by the
/// player's prev/next episode navigation (TV season and movie franchise).
/// </summary>
[TestClass]
public sealed class VideoPlayerNavigationTests
{
    [TestMethod]
    public void ComputeNextEpisodeIndex_Middle_Advances()
    {
        Assert.AreEqual(3, VideoPage.ComputeNextEpisodeIndex(5, 2, 1));
        Assert.AreEqual(1, VideoPage.ComputeNextEpisodeIndex(5, 2, -1));
    }

    [TestMethod]
    public void ComputeNextEpisodeIndex_First_PrevReturnsNull()
    {
        Assert.IsNull(VideoPage.ComputeNextEpisodeIndex(5, 0, -1));
        Assert.AreEqual(1, VideoPage.ComputeNextEpisodeIndex(5, 0, 1));
    }

    [TestMethod]
    public void ComputeNextEpisodeIndex_Last_NextReturnsNull()
    {
        Assert.IsNull(VideoPage.ComputeNextEpisodeIndex(5, 4, 1));
        Assert.AreEqual(3, VideoPage.ComputeNextEpisodeIndex(5, 4, -1));
    }

    [TestMethod]
    public void ComputeNextEpisodeIndex_SingleEpisode_NoNavigation()
    {
        Assert.IsNull(VideoPage.ComputeNextEpisodeIndex(1, 0, 1));
        Assert.IsNull(VideoPage.ComputeNextEpisodeIndex(1, 0, -1));
    }

    [TestMethod]
    public void ComputeNextEpisodeIndex_EmptyList_ReturnsNull()
    {
        Assert.IsNull(VideoPage.ComputeNextEpisodeIndex(0, 0, 1));
        Assert.IsNull(VideoPage.ComputeNextEpisodeIndex(-1, 0, 1));
    }

    [TestMethod]
    public void ComputeNextEpisodeIndex_InvalidCurrent_ReturnsNull()
    {
        Assert.IsNull(VideoPage.ComputeNextEpisodeIndex(5, -1, 1));
        Assert.IsNull(VideoPage.ComputeNextEpisodeIndex(5, 99, 1));
    }
}

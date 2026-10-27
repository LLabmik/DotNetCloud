using DotNetCloud.Modules.Music.UI;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Tests for <see cref="MusicRoutes"/> — the route matcher the global playbar uses to decide
/// whether the now-playing track belongs in the browser tab title.
/// </summary>
[TestClass]
public sealed class MusicRoutesTests
{
    [TestMethod]
    public void IsMusicUrl_MusicPagePath_ReturnsTrue()
    {
        Assert.IsTrue(MusicRoutes.IsMusicUrl("/apps/music"));
    }

    [TestMethod]
    public void IsMusicUrl_AbsoluteMusicUrl_ReturnsTrue()
    {
        Assert.IsTrue(MusicRoutes.IsMusicUrl("https://cloud.example.com/apps/music"));
    }

    [TestMethod]
    public void IsMusicUrl_AbsoluteMusicUrlWithQuery_ReturnsTrue()
    {
        Assert.IsTrue(MusicRoutes.IsMusicUrl("https://cloud.example.com/apps/music?playing=true&trackId=abc"));
    }

    [TestMethod]
    public void IsMusicUrl_RelativeMusicPathWithQuery_ReturnsTrue()
    {
        Assert.IsTrue(MusicRoutes.IsMusicUrl("/apps/music?artistId=42"));
    }

    [TestMethod]
    public void IsMusicUrl_NestedMusicPath_ReturnsTrue()
    {
        Assert.IsTrue(MusicRoutes.IsMusicUrl("/apps/music/help"));
    }

    [TestMethod]
    public void IsMusicUrl_TrailingSlash_ReturnsTrue()
    {
        Assert.IsTrue(MusicRoutes.IsMusicUrl("https://cloud.example.com/apps/music/"));
    }

    [TestMethod]
    public void IsMusicUrl_MixedCase_ReturnsTrue()
    {
        Assert.IsTrue(MusicRoutes.IsMusicUrl("https://cloud.example.com/Apps/Music"));
    }

    [TestMethod]
    public void IsMusicUrl_OtherModule_ReturnsFalse()
    {
        Assert.IsFalse(MusicRoutes.IsMusicUrl("https://cloud.example.com/apps/files?fileId=1"));
    }

    [TestMethod]
    public void IsMusicUrl_PathPrefixOnly_ReturnsFalse()
    {
        Assert.IsFalse(MusicRoutes.IsMusicUrl("https://cloud.example.com/apps/musicology"));
    }

    [TestMethod]
    public void IsMusicUrl_Null_ReturnsFalse()
    {
        Assert.IsFalse(MusicRoutes.IsMusicUrl(null));
    }

    [TestMethod]
    public void IsMusicUrl_Empty_ReturnsFalse()
    {
        Assert.IsFalse(MusicRoutes.IsMusicUrl(string.Empty));
    }
}

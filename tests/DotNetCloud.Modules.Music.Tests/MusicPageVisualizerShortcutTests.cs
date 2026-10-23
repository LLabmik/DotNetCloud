using DotNetCloud.Modules.Music.UI;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Unit tests for the visualizer preset stepping used by the N / B keyboard shortcuts.
/// </summary>
[TestClass]
public class MusicPageVisualizerShortcutTests
{
    [TestMethod]
    public void GetSteppedPresetIndex_NextFromMiddle_Advances()
    {
        Assert.AreEqual(2, MusicPage.GetSteppedPresetIndex(1, 3, 1));
    }

    [TestMethod]
    public void GetSteppedPresetIndex_NextFromLast_WrapsToFirst()
    {
        Assert.AreEqual(0, MusicPage.GetSteppedPresetIndex(2, 3, 1));
    }

    [TestMethod]
    public void GetSteppedPresetIndex_PreviousFromMiddle_GoesBack()
    {
        Assert.AreEqual(1, MusicPage.GetSteppedPresetIndex(2, 3, -1));
    }

    [TestMethod]
    public void GetSteppedPresetIndex_PreviousFromFirst_WrapsToLast()
    {
        Assert.AreEqual(2, MusicPage.GetSteppedPresetIndex(0, 3, -1));
    }

    [TestMethod]
    public void GetSteppedPresetIndex_UnknownCurrentForward_ReturnsFirst()
    {
        Assert.AreEqual(0, MusicPage.GetSteppedPresetIndex(-1, 3, 1));
    }

    [TestMethod]
    public void GetSteppedPresetIndex_UnknownCurrentBackward_ReturnsLast()
    {
        Assert.AreEqual(2, MusicPage.GetSteppedPresetIndex(-1, 3, -1));
    }

    [TestMethod]
    public void GetSteppedPresetIndex_SinglePreset_ReturnsZero()
    {
        Assert.AreEqual(0, MusicPage.GetSteppedPresetIndex(0, 1, 1));
        Assert.AreEqual(0, MusicPage.GetSteppedPresetIndex(0, 1, -1));
    }

    [TestMethod]
    public void GetSteppedPresetIndex_NoPresets_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, MusicPage.GetSteppedPresetIndex(-1, 0, 1));
        Assert.AreEqual(-1, MusicPage.GetSteppedPresetIndex(0, 0, -1));
    }
}

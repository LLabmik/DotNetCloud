using DotNetCloud.Modules.Music.UI;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Unit tests for keeping the visualizer preset dropdown in sync with the preset that JS reports.
/// </summary>
[TestClass]
public class MusicPageVisualizerPresetSyncTests
{
    [TestMethod]
    public void ApplyVisualizerPresetFromJs_ValidName_UpdatesSelectedPreset()
    {
        var page = new MusicPage();

        page.ApplyVisualizerPresetFromJs("DotNetCloud-Basic - Aurora Drift");

        Assert.AreEqual("DotNetCloud-Basic - Aurora Drift", page.SelectedVisualizerPresetForTests);
    }

    [TestMethod]
    public void ApplyVisualizerPresetFromJs_SecondName_ReplacesPrevious()
    {
        var page = new MusicPage();

        page.ApplyVisualizerPresetFromJs("First");
        page.ApplyVisualizerPresetFromJs("Second");

        Assert.AreEqual("Second", page.SelectedVisualizerPresetForTests);
    }

    [TestMethod]
    public void ApplyVisualizerPresetFromJs_EmptyOrWhitespace_KeepsCurrentPreset()
    {
        var page = new MusicPage();
        page.ApplyVisualizerPresetFromJs("Existing");

        page.ApplyVisualizerPresetFromJs(string.Empty);
        Assert.AreEqual("Existing", page.SelectedVisualizerPresetForTests);

        page.ApplyVisualizerPresetFromJs("   ");
        Assert.AreEqual("Existing", page.SelectedVisualizerPresetForTests);
    }
}

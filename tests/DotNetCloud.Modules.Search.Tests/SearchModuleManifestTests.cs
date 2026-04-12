using DotNetCloud.Core.Events.Search;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="SearchModuleManifest"/>.
/// </summary>
[TestClass]
public class SearchModuleManifestTests
{
    private SearchModuleManifest _manifest = null!;

    [TestInitialize]
    public void Setup()
    {
        _manifest = new SearchModuleManifest();
    }

    [TestMethod]
    public void Id_IsCorrect()
    {
        Assert.AreEqual("dotnetcloud.search", _manifest.Id);
    }

    [TestMethod]
    public void Name_IsCorrect()
    {
        Assert.AreEqual("Search", _manifest.Name);
    }

    [TestMethod]
    public void Version_IsCorrect()
    {
        Assert.AreEqual("1.0.0", _manifest.Version);
    }

    [TestMethod]
    public void RequiredCapabilities_ContainsSearchableModule()
    {
        Assert.IsTrue(_manifest.RequiredCapabilities.Contains("ISearchableModule"));
    }

    [TestMethod]
    public void RequiredCapabilities_ContainsEventBus()
    {
        Assert.IsTrue(_manifest.RequiredCapabilities.Contains("IEventBus"));
    }

    [TestMethod]
    public void PublishedEvents_ContainsSearchIndexCompletedEvent()
    {
        Assert.IsTrue(_manifest.PublishedEvents.Contains(nameof(SearchIndexCompletedEvent)));
    }

    [TestMethod]
    public void SubscribedEvents_ContainsSearchIndexRequestEvent()
    {
        Assert.IsTrue(_manifest.SubscribedEvents.Contains(nameof(SearchIndexRequestEvent)));
    }

    [TestMethod]
    public void RequiredCapabilities_ExactCount()
    {
        Assert.AreEqual(2, _manifest.RequiredCapabilities.Count);
    }

    [TestMethod]
    public void PublishedEvents_ExactCount()
    {
        Assert.AreEqual(1, _manifest.PublishedEvents.Count);
    }

    [TestMethod]
    public void SubscribedEvents_ExactCount()
    {
        Assert.AreEqual(1, _manifest.SubscribedEvents.Count);
    }
}

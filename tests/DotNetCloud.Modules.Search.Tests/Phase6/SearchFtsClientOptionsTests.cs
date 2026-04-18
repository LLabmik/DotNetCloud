extern alias SearchClient;

using SearchClient::DotNetCloud.Modules.Search.Client;

namespace DotNetCloud.Modules.Search.Tests.Phase6;

/// <summary>
/// Tests for <see cref="SearchFtsClientOptions"/> configuration model.
/// </summary>
[TestClass]
public class SearchFtsClientOptionsTests
{
    [TestMethod]
    public void SectionName_IsSearchModule()
    {
        Assert.AreEqual("SearchModule", SearchFtsClientOptions.SectionName);
    }

    [TestMethod]
    public void DefaultTimeout_IsTenSeconds()
    {
        var options = new SearchFtsClientOptions();
        Assert.AreEqual(TimeSpan.FromSeconds(10), options.Timeout);
    }

    [TestMethod]
    public void SearchModuleAddress_DefaultsToNull()
    {
        var options = new SearchFtsClientOptions();
        Assert.IsNull(options.SearchModuleAddress);
    }

    [TestMethod]
    public void SearchModuleAddress_CanBeSetToHttp()
    {
        var options = new SearchFtsClientOptions
        {
            SearchModuleAddress = "http://localhost:5080"
        };
        Assert.AreEqual("http://localhost:5080", options.SearchModuleAddress);
    }

    [TestMethod]
    public void SearchModuleAddress_CanBeSetToUnixSocket()
    {
        var options = new SearchFtsClientOptions
        {
            SearchModuleAddress = "unix:///var/run/dotnetcloud/search.sock"
        };
        Assert.AreEqual("unix:///var/run/dotnetcloud/search.sock", options.SearchModuleAddress);
    }

    [TestMethod]
    public void Timeout_CanBeCustomized()
    {
        var options = new SearchFtsClientOptions
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        Assert.AreEqual(TimeSpan.FromSeconds(30), options.Timeout);
    }
}

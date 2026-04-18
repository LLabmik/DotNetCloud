extern alias SearchClient;

using SearchClient::DotNetCloud.Modules.Search.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Search.Tests.Phase6;

/// <summary>
/// Tests for <see cref="SearchClientServiceExtensions"/> DI registration methods.
/// </summary>
[TestClass]
public class SearchClientServiceExtensionsTests
{
    #region AddSearchFtsClient with IConfiguration

    [TestMethod]
    public void AddSearchFtsClient_WithConfiguration_RegistersServices()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SearchModule:SearchModuleAddress"] = "http://localhost:5080",
                ["SearchModule:Timeout"] = "00:00:05"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSearchFtsClient(config);

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<ISearchFtsClient>();

        Assert.IsNotNull(client);
        Assert.IsInstanceOfType<SearchFtsClient>(client);
        Assert.IsTrue(client.IsAvailable);
    }

    [TestMethod]
    public void AddSearchFtsClient_WithEmptyConfiguration_RegistersUnavailableClient()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSearchFtsClient(config);

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<ISearchFtsClient>();

        Assert.IsNotNull(client);
        Assert.IsFalse(client.IsAvailable);
    }

    [TestMethod]
    public void AddSearchFtsClient_WithConfiguration_RegistersAsSingleton()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SearchModule:SearchModuleAddress"] = "http://localhost:5080"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSearchFtsClient(config);

        var provider = services.BuildServiceProvider();
        var client1 = provider.GetService<ISearchFtsClient>();
        var client2 = provider.GetService<ISearchFtsClient>();

        Assert.AreSame(client1, client2);
    }

    #endregion

    #region AddSearchFtsClient with address string

    [TestMethod]
    public void AddSearchFtsClient_WithAddress_RegistersAvailableClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSearchFtsClient("http://localhost:5080");

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<ISearchFtsClient>();

        Assert.IsNotNull(client);
        Assert.IsTrue(client.IsAvailable);
    }

    [TestMethod]
    public void AddSearchFtsClient_WithUnixSocketAddress_RegistersAvailableClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSearchFtsClient("unix:///var/run/dotnetcloud/search.sock");

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<ISearchFtsClient>();

        Assert.IsNotNull(client);
        Assert.IsTrue(client.IsAvailable);
    }

    #endregion
}

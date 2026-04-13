extern alias SearchClient;

using DotNetCloud.Core.DTOs.Search;
using SearchClient::DotNetCloud.Modules.Search.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase6;

/// <summary>
/// Tests for <see cref="SearchFtsClient"/> — the gRPC client wrapper used by
/// Files, Chat, Notes modules to delegate search to the centralized Search module.
/// </summary>
[TestClass]
public class SearchFtsClientTests
{
    #region IsAvailable

    [TestMethod]
    public void IsAvailable_WhenAddressConfigured_ReturnsTrue()
    {
        var options = Options.Create(new SearchFtsClientOptions
        {
            SearchModuleAddress = "http://localhost:5080"
        });

        using var client = new SearchFtsClient(options, NullLogger<SearchFtsClient>.Instance);

        Assert.IsTrue(client.IsAvailable);
    }

    [TestMethod]
    public void IsAvailable_WhenAddressNull_ReturnsFalse()
    {
        var options = Options.Create(new SearchFtsClientOptions
        {
            SearchModuleAddress = null
        });

        using var client = new SearchFtsClient(options, NullLogger<SearchFtsClient>.Instance);

        Assert.IsFalse(client.IsAvailable);
    }

    [TestMethod]
    public void IsAvailable_WhenAddressEmpty_ReturnsFalse()
    {
        var options = Options.Create(new SearchFtsClientOptions
        {
            SearchModuleAddress = ""
        });

        using var client = new SearchFtsClient(options, NullLogger<SearchFtsClient>.Instance);

        Assert.IsFalse(client.IsAvailable);
    }

    [TestMethod]
    public void IsAvailable_WhenAddressWhitespace_ReturnsFalse()
    {
        var options = Options.Create(new SearchFtsClientOptions
        {
            SearchModuleAddress = "   "
        });

        using var client = new SearchFtsClient(options, NullLogger<SearchFtsClient>.Instance);

        Assert.IsFalse(client.IsAvailable);
    }

    #endregion

    #region SearchAsync — unavailable

    [TestMethod]
    public async Task SearchAsync_WhenNotAvailable_ReturnsNull()
    {
        var options = Options.Create(new SearchFtsClientOptions
        {
            SearchModuleAddress = null
        });

        using var client = new SearchFtsClient(options, NullLogger<SearchFtsClient>.Instance);

        var result = await client.SearchAsync("test query");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SearchAsync_WhenAddressEmpty_ReturnsNull()
    {
        var options = Options.Create(new SearchFtsClientOptions
        {
            SearchModuleAddress = ""
        });

        using var client = new SearchFtsClient(options, NullLogger<SearchFtsClient>.Instance);

        var result = await client.SearchAsync("test query", moduleFilter: "files");

        Assert.IsNull(result);
    }

    #endregion

    #region SearchAsync — gRPC unavailable (graceful degradation)

    [TestMethod]
    public async Task SearchAsync_WhenGrpcServiceUnavailable_ReturnsNull()
    {
        // Use a non-existent address to trigger RpcException(Unavailable)
        var options = Options.Create(new SearchFtsClientOptions
        {
            SearchModuleAddress = "http://127.0.0.1:19999",
            Timeout = TimeSpan.FromMilliseconds(500)
        });

        using var client = new SearchFtsClient(options, NullLogger<SearchFtsClient>.Instance);

        var result = await client.SearchAsync("test query", userId: Guid.NewGuid());

        // Should return null gracefully, not throw
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SearchAsync_WithModuleFilter_WhenUnavailable_ReturnsNull()
    {
        var options = Options.Create(new SearchFtsClientOptions
        {
            SearchModuleAddress = "http://127.0.0.1:19999",
            Timeout = TimeSpan.FromMilliseconds(500)
        });

        using var client = new SearchFtsClient(options, NullLogger<SearchFtsClient>.Instance);

        var result = await client.SearchAsync(
            "budget report",
            moduleFilter: "notes",
            entityTypeFilter: "Note",
            userId: Guid.NewGuid(),
            page: 1,
            pageSize: 20,
            sortOrder: SearchSortOrder.Relevance);

        Assert.IsNull(result);
    }

    #endregion

    #region Dispose

    [TestMethod]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        var options = Options.Create(new SearchFtsClientOptions
        {
            SearchModuleAddress = "http://localhost:5080"
        });

        var client = new SearchFtsClient(options, NullLogger<SearchFtsClient>.Instance);

        client.Dispose();
        client.Dispose(); // second dispose should not throw
    }

    [TestMethod]
    public void Dispose_WithoutEverCalling_DoesNotThrow()
    {
        var options = Options.Create(new SearchFtsClientOptions
        {
            SearchModuleAddress = null
        });

        var client = new SearchFtsClient(options, NullLogger<SearchFtsClient>.Instance);
        client.Dispose();
    }

    #endregion
}

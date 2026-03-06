using System.Net;
using DotNetCloud.Integration.Tests.Infrastructure;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for health check endpoints.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class HealthEndpointTests
{
    private static DotNetCloudWebApplicationFactory _factory = null!;
    private static HttpClient _client = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _factory = new DotNetCloudWebApplicationFactory();
        _client = _factory.CreateApiClient();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [TestMethod]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        ApiAssert.StatusCode(response, HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task HealthReady_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        ApiAssert.StatusCode(response, HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task HealthLive_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        ApiAssert.StatusCode(response, HttpStatusCode.OK);
    }
}

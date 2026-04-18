using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Integration.Tests.Infrastructure;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for the public update endpoints (<c>/api/v1/core/updates/*</c>).
/// These endpoints require no authentication.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class UpdateEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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

    // ---------------------------------------------------------------------------
    // GET /api/v1/core/updates/check
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task CheckForUpdate_ReturnsSuccessEnvelope()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/core/updates/check");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Update check should return 200. Body: {await response.Content.ReadAsStringAsync()}");

        var root = await ParseJsonAsync(response);
        Assert.IsTrue(root.TryGetProperty("success", out var success), "Response should have 'success' property");
        Assert.IsTrue(success.GetBoolean(), "success should be true");
        Assert.IsTrue(root.TryGetProperty("data", out var data), "Response should have 'data' property");
        Assert.IsTrue(data.TryGetProperty("currentVersion", out _), "data should contain 'currentVersion'");
        Assert.IsTrue(data.TryGetProperty("latestVersion", out _), "data should contain 'latestVersion'");
        Assert.IsTrue(data.TryGetProperty("isUpdateAvailable", out _), "data should contain 'isUpdateAvailable'");
    }

    [TestMethod]
    public async Task CheckForUpdate_WithVersionParam_AcceptsVersion()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/core/updates/check?currentVersion=0.0.1");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Update check with version param should return 200. Body: {await response.Content.ReadAsStringAsync()}");

        var root = await ParseJsonAsync(response);
        var data = root.GetProperty("data");
        var currentVersion = data.GetProperty("currentVersion").GetString();
        Assert.AreEqual("0.0.1", currentVersion,
            "currentVersion should reflect the supplied query parameter");
    }

    // ---------------------------------------------------------------------------
    // GET /api/v1/core/updates/releases
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task GetRecentReleases_ReturnsSuccessEnvelope()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/core/updates/releases");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Releases endpoint should return 200. Body: {await response.Content.ReadAsStringAsync()}");

        var root = await ParseJsonAsync(response);
        Assert.IsTrue(root.TryGetProperty("success", out var success), "Response should have 'success' property");
        Assert.IsTrue(success.GetBoolean(), "success should be true");
        Assert.IsTrue(root.TryGetProperty("data", out var data), "Response should have 'data' property");
        Assert.AreEqual(JsonValueKind.Array, data.ValueKind, "data should be an array of releases");
    }

    [TestMethod]
    public async Task GetRecentReleases_CountParamIsClamped()
    {
        // Act — request a count of 100 (max is 20)
        var response = await _client.GetAsync("/api/v1/core/updates/releases?count=100");

        // Assert — should succeed regardless (server clamps to 20)
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Releases endpoint should accept and clamp count. Body: {await response.Content.ReadAsStringAsync()}");

        var root = await ParseJsonAsync(response);
        Assert.IsTrue(root.GetProperty("success").GetBoolean());
    }

    // ---------------------------------------------------------------------------
    // GET /api/v1/core/updates/releases/latest
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task GetLatestRelease_ReturnsSuccessOrNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/core/updates/releases/latest");

        // Assert — may be 200 (if GitHub has releases cached) or 404 (no releases in test env)
        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Latest release should return 200 or 404. Got: {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");

        var root = await ParseJsonAsync(response);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.IsTrue(root.GetProperty("success").GetBoolean());
            Assert.IsTrue(root.TryGetProperty("data", out var data), "200 response should have 'data'");
            Assert.IsTrue(data.TryGetProperty("version", out _), "Release data should have 'version'");
        }
        else
        {
            Assert.IsFalse(root.GetProperty("success").GetBoolean());
            Assert.IsTrue(root.TryGetProperty("error", out var error), "404 response should have 'error'");
            Assert.AreEqual("NO_RELEASES", error.GetProperty("code").GetString());
        }
    }

    // ---------------------------------------------------------------------------
    // No auth required — anonymous access
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task UpdateEndpoints_DoNotRequireAuthentication()
    {
        // All three endpoints should be accessible without any auth headers.
        // The _client created by CreateApiClient() has no auth headers.
        var checkResponse = await _client.GetAsync("/api/v1/core/updates/check");
        var releasesResponse = await _client.GetAsync("/api/v1/core/updates/releases");
        var latestResponse = await _client.GetAsync("/api/v1/core/updates/releases/latest");

        // None should return 401 or 403
        Assert.AreNotEqual(HttpStatusCode.Unauthorized, checkResponse.StatusCode,
            "Update check should not require auth");
        Assert.AreNotEqual(HttpStatusCode.Forbidden, checkResponse.StatusCode,
            "Update check should not be forbidden");

        Assert.AreNotEqual(HttpStatusCode.Unauthorized, releasesResponse.StatusCode,
            "Releases should not require auth");
        Assert.AreNotEqual(HttpStatusCode.Forbidden, releasesResponse.StatusCode,
            "Releases should not be forbidden");

        Assert.AreNotEqual(HttpStatusCode.Unauthorized, latestResponse.StatusCode,
            "Latest release should not require auth");
        Assert.AreNotEqual(HttpStatusCode.Forbidden, latestResponse.StatusCode,
            "Latest release should not be forbidden");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static async Task<JsonElement> ParseJsonAsync(HttpResponseMessage response)
    {
        var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(JsonOptions);
        Assert.IsNotNull(doc, "Response body should be valid JSON");
        return doc.RootElement;
    }
}

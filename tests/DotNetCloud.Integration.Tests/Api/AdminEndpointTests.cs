using System.Net;
using System.Net.Http.Json;
using DotNetCloud.Integration.Tests.Infrastructure;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for the admin API endpoints (<c>/api/v1/core/admin/*</c>).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class AdminEndpointTests
{
    private static DotNetCloudWebApplicationFactory _factory = null!;
    private static HttpClient _adminClient = null!;
    private static HttpClient _userClient = null!;
    private static HttpClient _anonClient = null!;

    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid RegularUserId = Guid.NewGuid();

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _factory = new DotNetCloudWebApplicationFactory();
        _anonClient = _factory.CreateApiClient();
        _adminClient = _factory.CreateAdminApiClient(AdminUserId);
        _userClient = _factory.CreateAuthenticatedApiClient(RegularUserId);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _anonClient?.Dispose();
        _userClient?.Dispose();
        _adminClient?.Dispose();
        _factory?.Dispose();
    }

    // ---------------------------------------------------------------------------
    // Settings CRUD
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task PutSetting_AsAdmin_ReturnsOk()
    {
        // Arrange
        var settingValue = new { Value = "test-value", Description = "Integration test setting" };

        // Act
        var response = await _adminClient.PutAsJsonAsync(
            "/api/v1/core/admin/settings/test-module/test-key", settingValue);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to create settings. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [TestMethod]
    public async Task GetSetting_AfterPut_ReturnsValue()
    {
        // Arrange — create a setting first
        var settingValue = new { Value = "round-trip-value", Description = "Round trip test" };
        await _adminClient.PutAsJsonAsync(
            "/api/v1/core/admin/settings/roundtrip/key1", settingValue);

        // Act
        var response = await _adminClient.GetAsync("/api/v1/core/admin/settings/roundtrip/key1");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to read settings. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [TestMethod]
    public async Task ListSettings_AsAdmin_ReturnsOk()
    {
        // Act
        var response = await _adminClient.GetAsync("/api/v1/core/admin/settings");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to list settings. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [TestMethod]
    public async Task DeleteSetting_AsAdmin_ReturnsOk()
    {
        // Arrange — create a setting to delete
        var settingValue = new { Value = "delete-me", Description = "Will be deleted" };
        await _adminClient.PutAsJsonAsync(
            "/api/v1/core/admin/settings/delete-test/key1", settingValue);

        // Act
        var response = await _adminClient.DeleteAsync(
            "/api/v1/core/admin/settings/delete-test/key1");

        // Assert — should succeed (200 or 204)
        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Admin should be able to delete settings. Status: {response.StatusCode}");
    }

    // ---------------------------------------------------------------------------
    // Module Management
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task ListModules_AsAdmin_ReturnsOk()
    {
        // Act
        var response = await _adminClient.GetAsync("/api/v1/core/admin/modules");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to list modules. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------------------------------------------------------------------------
    // System Health
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task HealthCheck_AsAdmin_ReturnsOk()
    {
        // Act
        var response = await _adminClient.GetAsync("/api/v1/core/admin/health");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to get health report. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------------------------------------------------------------------------
    // Authorization enforcement
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task Settings_AsNonAdmin_ReturnsForbidden()
    {
        // Act
        var response = await _userClient.GetAsync("/api/v1/core/admin/settings");

        // Assert
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode,
            "Non-admin should not access admin settings");
    }

    [TestMethod]
    public async Task Modules_AsNonAdmin_ReturnsForbidden()
    {
        // Act
        var response = await _userClient.GetAsync("/api/v1/core/admin/modules");

        // Assert
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode,
            "Non-admin should not access admin modules");
    }

    [TestMethod]
    public async Task Settings_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/v1/core/admin/settings");

        // Assert
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Unauthenticated request should not succeed");
    }
}

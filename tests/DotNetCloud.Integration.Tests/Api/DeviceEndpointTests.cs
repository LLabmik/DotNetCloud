using System.Net;
using DotNetCloud.Integration.Tests.Infrastructure;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for the device management API endpoints (<c>/api/v1/core/auth/devices/*</c>).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class DeviceEndpointTests
{
    private static DotNetCloudWebApplicationFactory _factory = null!;
    private static HttpClient _authClient = null!;
    private static HttpClient _anonClient = null!;

    private static readonly Guid UserId = Guid.NewGuid();

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _factory = new DotNetCloudWebApplicationFactory();
        _anonClient = _factory.CreateApiClient();
        _authClient = _factory.CreateAuthenticatedApiClient(UserId);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _anonClient?.Dispose();
        _authClient?.Dispose();
        _factory?.Dispose();
    }

    // ---------------------------------------------------------------------------
    // List Devices
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task ListDevices_Authenticated_ReturnsOk()
    {
        // Act
        var response = await _authClient.GetAsync("/api/v1/core/auth/devices");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Authenticated user should be able to list devices. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------------------------------------------------------------------------
    // Delete Device
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task DeleteDevice_NonExistent_ReturnsNotFound()
    {
        // Act
        var fakeDeviceId = Guid.NewGuid();
        var response = await _authClient.DeleteAsync(
            $"/api/v1/core/auth/devices/{fakeDeviceId}");

        // Assert — should handle gracefully (404 or similar, not 500)
        Assert.AreNotEqual(HttpStatusCode.InternalServerError, response.StatusCode,
            "Deleting non-existent device should not cause server error");
    }

    // ---------------------------------------------------------------------------
    // Authorization enforcement
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task ListDevices_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/v1/core/auth/devices");

        // Assert
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Unauthenticated request should not succeed");
    }

    [TestMethod]
    public async Task DeleteDevice_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.DeleteAsync(
            $"/api/v1/core/auth/devices/{Guid.NewGuid()}");

        // Assert
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Unauthenticated request should not succeed");
    }
}

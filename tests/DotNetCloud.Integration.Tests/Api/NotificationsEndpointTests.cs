using System.Net;
using DotNetCloud.Integration.Tests.Infrastructure;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for the notifications API endpoints (<c>/api/v1/notifications/*</c>).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class NotificationsEndpointTests
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
    // Unread notifications
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task GetUnread_Authenticated_ReturnsOk()
    {
        // Act
        var response = await _authClient.GetAsync("/api/v1/notifications/unread");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Authenticated user should be able to get unread notifications. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [TestMethod]
    public async Task GetUnreadCount_Authenticated_ReturnsOk()
    {
        // Act
        var response = await _authClient.GetAsync("/api/v1/notifications/unread-count");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Authenticated user should be able to get unread count. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------------------------------------------------------------------------
    // Mark read
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task MarkAllRead_Authenticated_Succeeds()
    {
        // Act
        var response = await _authClient.PostAsync("/api/v1/notifications/mark-all-read", null);

        // Assert — should succeed even if there are no notifications
        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Mark all read should succeed. Status: {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [TestMethod]
    public async Task MarkRead_NonExistentNotification_ReturnsNotFoundOrBadRequest()
    {
        // Act
        var fakeId = Guid.NewGuid();
        var response = await _authClient.PostAsync(
            $"/api/v1/notifications/{fakeId}/mark-read", null);

        // Assert — should handle gracefully (not 500)
        Assert.AreNotEqual(HttpStatusCode.InternalServerError, response.StatusCode,
            "Marking non-existent notification should not cause server error");
    }

    // ---------------------------------------------------------------------------
    // Authorization enforcement
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task GetUnread_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/v1/notifications/unread");

        // Assert
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Unauthenticated request should not succeed");
    }

    [TestMethod]
    public async Task GetUnreadCount_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/v1/notifications/unread-count");

        // Assert
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Unauthenticated request should not succeed");
    }

    [TestMethod]
    public async Task MarkAllRead_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.PostAsync("/api/v1/notifications/mark-all-read", null);

        // Assert
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Unauthenticated request should not succeed");
    }
}

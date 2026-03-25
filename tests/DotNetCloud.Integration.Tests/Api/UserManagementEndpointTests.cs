using System.Net;
using System.Net.Http.Json;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Integration.Tests.Builders;
using DotNetCloud.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for the user management API endpoints (<c>/api/v1/core/users/*</c>).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class UserManagementEndpointTests
{
    private static DotNetCloudWebApplicationFactory _factory = null!;
    private static HttpClient _adminClient = null!;
    private static HttpClient _userClient = null!;
    private static HttpClient _anonClient = null!;

    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid RegularUserId = Guid.NewGuid();

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        _factory = new DotNetCloudWebApplicationFactory();
        _anonClient = _factory.CreateApiClient();
        _adminClient = _factory.CreateAdminApiClient(AdminUserId);
        _userClient = _factory.CreateAuthenticatedApiClient(RegularUserId);

        // Seed users in the database so endpoints that look them up succeed
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var adminUser = new ApplicationUserBuilder()
            .WithId(AdminUserId)
            .WithEmail("admin-mgmt@test.local")
            .WithDisplayName("Test Admin")
            .Build();
        await userManager.CreateAsync(adminUser, "TestP@ssw0rd!");

        var regularUser = new ApplicationUserBuilder()
            .WithId(RegularUserId)
            .WithEmail("user-mgmt@test.local")
            .WithDisplayName("Regular User")
            .Build();
        await userManager.CreateAsync(regularUser, "TestP@ssw0rd!");
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
    // List Users
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task ListUsers_AsAdmin_ReturnsOk()
    {
        // Act
        var response = await _adminClient.GetAsync("/api/v1/core/users");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to list users. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [TestMethod]
    public async Task ListUsers_AsNonAdmin_ReturnsForbidden()
    {
        // Act
        var response = await _userClient.GetAsync("/api/v1/core/users");

        // Assert
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode,
            "Non-admin should not be able to list users");
    }

    [TestMethod]
    public async Task ListUsers_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/v1/core/users");

        // Assert — should not be 200 OK
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Unauthenticated request should not succeed");
    }

    // ---------------------------------------------------------------------------
    // Get User
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task GetUser_OwnProfile_ReturnsOk()
    {
        // Act
        var response = await _userClient.GetAsync($"/api/v1/core/users/{RegularUserId}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"User should be able to view own profile. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [TestMethod]
    public async Task GetUser_OtherUser_AsAdmin_ReturnsOk()
    {
        // Act
        var response = await _adminClient.GetAsync($"/api/v1/core/users/{RegularUserId}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to view any user. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------------------------------------------------------------------------
    // Update User
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task UpdateUser_OwnProfile_ReturnsOk()
    {
        // Arrange
        var updateRequest = new { DisplayName = "Updated User Name" };

        // Act
        var response = await _userClient.PutAsJsonAsync(
            $"/api/v1/core/users/{RegularUserId}", updateRequest);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"User should be able to update own profile. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------------------------------------------------------------------------
    // Delete User (Admin)
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task DeleteUser_AsAdmin_ReturnsOk()
    {
        // Arrange — create a disposable user
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var disposableId = Guid.NewGuid();
        var disposableUser = new ApplicationUserBuilder()
            .WithId(disposableId)
            .WithEmail($"disposable-{disposableId:N}@test.local")
            .Build();
        await userManager.CreateAsync(disposableUser, "TestP@ssw0rd!");

        // Act
        var response = await _adminClient.DeleteAsync($"/api/v1/core/users/{disposableId}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to delete users. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [TestMethod]
    public async Task DeleteUser_Self_ReturnsBadRequest()
    {
        // Act — admin tries to delete themselves
        var response = await _adminClient.DeleteAsync($"/api/v1/core/users/{AdminUserId}");

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode,
            "Admin should not be able to delete themselves");
    }

    // ---------------------------------------------------------------------------
    // Disable / Enable User (Admin)
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task DisableUser_AsAdmin_ReturnsOk()
    {
        // Arrange — create a user to disable
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var targetId = Guid.NewGuid();
        var targetUser = new ApplicationUserBuilder()
            .WithId(targetId)
            .WithEmail($"disable-target-{targetId:N}@test.local")
            .Build();
        await userManager.CreateAsync(targetUser, "TestP@ssw0rd!");

        // Act
        var response = await _adminClient.PostAsync(
            $"/api/v1/core/users/{targetId}/disable", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to disable users. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [TestMethod]
    public async Task EnableUser_AsAdmin_ReturnsOk()
    {
        // Arrange — create a disabled user
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var targetId = Guid.NewGuid();
        var targetUser = new ApplicationUserBuilder()
            .WithId(targetId)
            .WithEmail($"enable-target-{targetId:N}@test.local")
            .WithIsActive(false)
            .Build();
        await userManager.CreateAsync(targetUser, "TestP@ssw0rd!");

        // Act
        var response = await _adminClient.PostAsync(
            $"/api/v1/core/users/{targetId}/enable", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to enable users. Body: {await response.Content.ReadAsStringAsync()}");
    }
}

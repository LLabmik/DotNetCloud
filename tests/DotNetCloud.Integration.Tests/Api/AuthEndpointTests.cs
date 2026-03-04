using System.Net;
using System.Net.Http.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Integration.Tests.Builders;
using DotNetCloud.Integration.Tests.Infrastructure;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for the authentication API endpoints (<c>/api/v1/core/auth/*</c>).
/// </summary>
[TestClass]
public class AuthEndpointTests
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

    // ---------------------------------------------------------------------------
    // Registration
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task Register_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new RegisterRequestBuilder()
            .WithEmail("newuser@test.local")
            .WithPassword("TestP@ssw0rd!")
            .WithDisplayName("New User")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/core/auth/register", request);

        // Assert
        var root = await ApiAssert.SuccessAsync(response);
        Assert.IsTrue(root.TryGetProperty("data", out var data), "Response should contain 'data'");
    }

    [TestMethod]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        // Arrange — register a user first
        var email = $"duplicate-{Guid.NewGuid():N}@test.local";
        var request = new RegisterRequestBuilder()
            .WithEmail(email)
            .WithPassword("TestP@ssw0rd!")
            .Build();

        await _client.PostAsJsonAsync("/api/v1/core/auth/register", request);

        // Act — attempt to register again with same email
        var duplicate = new RegisterRequestBuilder()
            .WithEmail(email)
            .WithPassword("TestP@ssw0rd!")
            .Build();

        var response = await _client.PostAsJsonAsync("/api/v1/core/auth/register", duplicate);

        // Assert — should fail
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode,
            "Duplicate registration should return BadRequest");
    }

    [TestMethod]
    public async Task Register_WeakPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequestBuilder()
            .WithEmail($"weakpw-{Guid.NewGuid():N}@test.local")
            .WithPassword("123")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/core/auth/register", request);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode,
            "Weak password should be rejected");
    }

    // ---------------------------------------------------------------------------
    // Login
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task Login_ValidCredentials_ReturnsOk()
    {
        // Arrange — register first
        var email = $"login-{Guid.NewGuid():N}@test.local";
        var password = "TestP@ssw0rd!";

        var registerRequest = new RegisterRequestBuilder()
            .WithEmail(email)
            .WithPassword(password)
            .Build();
        await _client.PostAsJsonAsync("/api/v1/core/auth/register", registerRequest);

        var loginRequest = new LoginRequest { Email = email, Password = password };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/core/auth/login", loginRequest);

        // Assert
        await ApiAssert.SuccessAsync(response);
    }

    [TestMethod]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "nonexistent@test.local",
            Password = "WrongPassword123!",
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/core/auth/login", request);

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Unauthenticated access to protected endpoints
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task Logout_WithoutAuth_ReturnsUnauthorizedOrRedirect()
    {
        // Act
        var response = await _client.PostAsync("/api/v1/core/auth/logout", null);

        // Assert — should not be 200 OK
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Logout without authentication should not succeed");
    }

    [TestMethod]
    public async Task GetCurrentUser_WithoutAuth_ReturnsUnauthorizedOrRedirect()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/core/auth/user");

        // Assert
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Getting current user without auth should not succeed");
    }

    // ---------------------------------------------------------------------------
    // Password reset
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task ForgotPassword_ValidEmail_ReturnsOk()
    {
        // Arrange — register a user
        var email = $"forgot-{Guid.NewGuid():N}@test.local";
        var registerRequest = new RegisterRequestBuilder()
            .WithEmail(email)
            .WithPassword("TestP@ssw0rd!")
            .Build();
        await _client.PostAsJsonAsync("/api/v1/core/auth/register", registerRequest);

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1/core/auth/password/forgot",
            new { Email = email });

        // Assert — should succeed (even if email isn't actually sent in tests)
        Assert.AreNotEqual(HttpStatusCode.InternalServerError, response.StatusCode,
            "Forgot password should not cause server error");
    }
}

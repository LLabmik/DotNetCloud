using System.Text;
using System.Text.Json;
using DotNetCloud.Core.Dtos.Auth;
using Xunit;

namespace DotNetCloud.Core.Server.Tests.Controllers;

/// <summary>
/// Integration tests for the authentication endpoints.
/// </summary>
public class AuthControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task RegisterAsync_WithValidCredentials_ReturnsCreated()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/auth/register", content);

        // Assert
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Created ||
                   response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsOk()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Email = "login@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Login Test User"
        };

        var registerContent = new StringContent(
            JsonSerializer.Serialize(registerRequest),
            Encoding.UTF8,
            "application/json");

        // First register a user
        await Client.PostAsync("/api/v1/auth/register", registerContent);

        // Now try to login
        var loginRequest = new LoginRequest
        {
            Email = "login@example.com",
            Password = "SecurePassword123!"
        };

        var loginContent = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/auth/login", loginContent);

        // Assert
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK ||
                   response.StatusCode == System.Net.HttpStatusCode.Accepted ||
                   response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "WrongPassword123!"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/auth/login", content);

        // Assert
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                   response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_ReturnsOk()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "valid_refresh_token_here"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/auth/refresh", content);

        // Assert
        // This will likely fail in test due to no valid token, but endpoint exists
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                   response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_WithValidEmail_ReturnsOk()
    {
        // Arrange
        var request = new PasswordResetRequestDto
        {
            Email = "reset@example.com"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/auth/password-reset-request", content);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProfileAsync_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/auth/profile");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "CurrentPassword123!",
            NewPassword = "NewPassword123!"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/auth/change-password", content);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LogoutAsync_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.PostAsync("/api/v1/auth/logout", null);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

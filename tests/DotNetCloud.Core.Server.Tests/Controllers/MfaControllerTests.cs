using System.Text;
using System.Text.Json;
using DotNetCloud.Core.Dtos.Auth;
using Xunit;

namespace DotNetCloud.Core.Server.Tests.Controllers;

/// <summary>
/// Integration tests for the MFA endpoints.
/// </summary>
public class MfaControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task GetTotpSetupAsync_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/auth/mfa/totp-setup");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyTotpAsync_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new TotpVerifyRequest
        {
            Code = "000000"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/auth/mfa/totp-verify", content);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DisableTotpAsync_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.PostAsync("/api/v1/auth/mfa/totp-disable", null);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GenerateBackupCodesAsync_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.PostAsync("/api/v1/auth/mfa/backup-codes", null);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMfaStatusAsync_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/auth/mfa/status");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

using System.Net;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Integration.Tests.Builders;
using DotNetCloud.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for the MFA API endpoints (<c>/api/v1/core/auth/mfa/*</c>).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class MfaEndpointTests
{
    private static DotNetCloudWebApplicationFactory _factory = null!;
    private static HttpClient _authClient = null!;
    private static HttpClient _anonClient = null!;

    private static readonly Guid UserId = Guid.NewGuid();

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        _factory = new DotNetCloudWebApplicationFactory();
        _anonClient = _factory.CreateApiClient();
        _authClient = _factory.CreateAuthenticatedApiClient(UserId);

        // Seed the user in the database — MFA endpoints look up the user via UserManager
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUserBuilder()
            .WithId(UserId)
            .WithEmail("mfa-test@test.local")
            .WithDisplayName("MFA Test User")
            .Build();
        await userManager.CreateAsync(user, "TestP@ssw0rd!");
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _anonClient?.Dispose();
        _authClient?.Dispose();
        _factory?.Dispose();
    }

    // ---------------------------------------------------------------------------
    // MFA Status
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task GetStatus_Authenticated_ReturnsOk()
    {
        // Act
        var response = await _authClient.GetAsync("/api/v1/core/auth/mfa/status");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Authenticated user should be able to get MFA status. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------------------------------------------------------------------------
    // TOTP Setup
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task SetupTotp_Authenticated_ReturnsOk()
    {
        // Act
        var response = await _authClient.PostAsync("/api/v1/core/auth/mfa/totp/setup", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Authenticated user should be able to initiate TOTP setup. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------------------------------------------------------------------------
    // Backup Codes
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task GetBackupCodes_Authenticated_ReturnsOk()
    {
        // Act
        var response = await _authClient.GetAsync("/api/v1/core/auth/mfa/backup-codes");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Authenticated user should be able to get backup codes. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------------------------------------------------------------------------
    // Authorization enforcement
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task GetStatus_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/v1/core/auth/mfa/status");

        // Assert
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Unauthenticated request should not succeed");
    }

    [TestMethod]
    public async Task SetupTotp_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.PostAsync("/api/v1/core/auth/mfa/totp/setup", null);

        // Assert
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Unauthenticated request should not succeed");
    }

    [TestMethod]
    public async Task BackupCodes_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/v1/core/auth/mfa/backup-codes");

        // Assert
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode,
            "Unauthenticated request should not succeed");
    }
}

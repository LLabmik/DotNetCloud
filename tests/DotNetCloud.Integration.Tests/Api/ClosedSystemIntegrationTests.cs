using System.Net;
using System.Net.Http.Json;
using DotNetCloud.Core.Constants;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Integration.Tests.Builders;
using DotNetCloud.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for closed system mode (Phase G).
/// Tests the HTTP-level behavior: registration gate, login blocking for PASSWORD_CHANGE_REQUIRED,
/// and normal flow after password change.
///
/// Admin user creation is tested at the unit test level (AuthServiceTests).
/// Integration tests seed users and settings via service scope to avoid claim-type mismatches
/// between the test auth handler and the production BuildCallerContext.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class ClosedSystemIntegrationTests
{
    private static DotNetCloudWebApplicationFactory _factory = null!;
    private static HttpClient _anonClient = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        _factory = new DotNetCloudWebApplicationFactory();
        _anonClient = _factory.CreateApiClient();

        // Enable closed system mode by seeding the system setting
        using var scope = _factory.Services.CreateScope();
        var adminSettingsService = scope.ServiceProvider
            .GetRequiredService<DotNetCloud.Core.Services.IAdminSettingsService>();
        await adminSettingsService.UpsertSettingAsync(
            SystemSettingKeys.CoreModule,
            SystemSettingKeys.ClosedSystemEnabled,
            new UpsertSystemSettingDto
            {
                Value = "true",
                Description = "Closed system mode enabled for integration tests",
            });
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _anonClient?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>
    /// Seeds a test user directly in the database via UserManager.
    /// Returns the email and password used.
    /// </summary>
    private static async Task<(string Email, string Password, Guid UserId)> SeedUserAsync(
        string emailPrefix,
        string password = "TestP@ssw0rd!",
        bool passwordChangeRequired = false)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@test.local";
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUserBuilder()
            .WithEmail(email)
            .WithDisplayName("Integration Test User")
            .Build();
        user.PasswordChangeRequired = passwordChangeRequired;
        await userManager.CreateAsync(user, password);
        return (email, password, user.Id);
    }

    // ---------------------------------------------------------------------------
    // Registration Gate — HTTP-level
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task Register_ClosedSystemEnabled_ReturnsForbidden()
    {
        // Arrange
        var request = new RegisterRequestBuilder()
            .WithEmail($"selfreg-{Guid.NewGuid():N}@test.local")
            .WithPassword("TestP@ssw0rd!")
            .WithDisplayName("Self Registrant")
            .Build();

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/v1/core/auth/register", request);

        // Assert — should return 403 Forbidden with CLOSED_SYSTEM code
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode,
            "Self-registration should be blocked with 403 when closed system mode is enabled");

        var body = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(body.Contains("CLOSED_SYSTEM", StringComparison.OrdinalIgnoreCase),
            "Response should contain CLOSED_SYSTEM error code");
    }

    // ---------------------------------------------------------------------------
    // Login — Password Change Required (HTTP-level)
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task Login_PasswordChangeRequired_ReturnsForbidden()
    {
        // Arrange — seed a user with PasswordChangeRequired = true in the database
        var (email, password, _) = await SeedUserAsync("pwreq", passwordChangeRequired: true);

        // Act — try to login via API
        var loginRequest = new LoginRequest { Email = email, Password = password };
        var loginResponse = await _anonClient.PostAsJsonAsync("/api/v1/core/auth/login", loginRequest);

        // Assert — should return 403 with PASSWORD_CHANGE_REQUIRED
        Assert.AreEqual(HttpStatusCode.Forbidden, loginResponse.StatusCode,
            "Login should return 403 when password change is required");

        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        Assert.IsTrue(loginBody.Contains("PASSWORD_CHANGE_REQUIRED", StringComparison.OrdinalIgnoreCase),
            "Response should contain PASSWORD_CHANGE_REQUIRED error code");
    }

    [TestMethod]
    public async Task Login_PasswordChangeNotRequired_ReturnsOk()
    {
        // Arrange — seed a normal user (PasswordChangeRequired = false)
        var (email, password, _) = await SeedUserAsync("normalpw", passwordChangeRequired: false);

        // Act — try to login via API
        var loginRequest = new LoginRequest { Email = email, Password = password };
        var loginResponse = await _anonClient.PostAsJsonAsync("/api/v1/core/auth/login", loginRequest);

        // Assert — should succeed (200 OK)
        Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode,
            "Login should succeed when PasswordChangeRequired is false");
    }

    // ---------------------------------------------------------------------------
    // Full Flow: Password Change → Normal Access
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task ChangePassword_AfterChange_LoginSucceeds()
    {
        // Arrange — seed a user with PasswordChangeRequired = true
        var (email, password, userId) = await SeedUserAsync("pwchange", passwordChangeRequired: true);

        // Change password and clear flag via service scope (simulating what the controller does)
        var newPassword = "NewP@ssw0rd!";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.IsNotNull(user);
            Assert.IsTrue(user!.PasswordChangeRequired, "User should have PasswordChangeRequired = true");

            // Change password
            var changeResult = await userManager.ChangePasswordAsync(user, password, newPassword);
            Assert.IsTrue(changeResult.Succeeded, "Password change should succeed");

            // Clear the flag (normally done by AuthSessionController.ChangePasswordPostAsync)
            user.PasswordChangeRequired = false;
            await userManager.UpdateAsync(user);
        }

        // Act — login with new password
        var loginRequest = new LoginRequest { Email = email, Password = newPassword };
        var loginResponse = await _anonClient.PostAsJsonAsync("/api/v1/core/auth/login", loginRequest);

        // Assert — should succeed
        Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode,
            "Login should succeed after password change when PasswordChangeRequired is cleared");
    }

    // ---------------------------------------------------------------------------
    // Normal Registration Works When Closed System Disabled
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task Register_ClosedSystemDisabled_AllowsSelfRegistration()
    {
        // Arrange — disable closed system mode
        using (var scope = _factory.Services.CreateScope())
        {
            var adminSettingsService = scope.ServiceProvider
                .GetRequiredService<DotNetCloud.Core.Services.IAdminSettingsService>();
            await adminSettingsService.UpsertSettingAsync(
                SystemSettingKeys.CoreModule,
                SystemSettingKeys.ClosedSystemEnabled,
                new UpsertSystemSettingDto
                {
                    Value = "false",
                    Description = "Closed system mode disabled for this test",
                });
        }

        var request = new RegisterRequestBuilder()
            .WithEmail($"normalreg-{Guid.NewGuid():N}@test.local")
            .WithPassword("TestP@ssw0rd!")
            .WithDisplayName("Normal Registrant")
            .Build();

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/v1/core/auth/register", request);

        // Assert — should succeed
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            "Self-registration should succeed when closed system mode is disabled");

        // Re-enable closed system mode for remaining tests
        using (var scope = _factory.Services.CreateScope())
        {
            var adminSettingsService = scope.ServiceProvider
                .GetRequiredService<DotNetCloud.Core.Services.IAdminSettingsService>();
            await adminSettingsService.UpsertSettingAsync(
                SystemSettingKeys.CoreModule,
                SystemSettingKeys.ClosedSystemEnabled,
                new UpsertSystemSettingDto
                {
                    Value = "true",
                    Description = "Closed system mode re-enabled for remaining tests",
                });
        }
    }
}

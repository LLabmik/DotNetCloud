using DotNetCloud.Core.Auth.Configuration;
using DotNetCloud.Core.Auth.Services;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Constants;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Abstractions;

namespace DotNetCloud.Core.Auth.Tests.Services;

/// <summary>
/// Tests for <see cref="AuthService"/>.
/// </summary>
[TestClass]
public class AuthServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
    private Mock<SignInManager<ApplicationUser>> _signInManagerMock = null!;
    private Mock<IOpenIddictTokenManager> _tokenManagerMock = null!;
    private Mock<IAdminSettingsService> _adminSettingsMock = null!;
    private Mock<ITransactionalEmailSender> _emailSenderMock = null!;
    private IOptions<SmtpOptions> _smtpOptions = null!;
    private Mock<IServiceProvider> _serviceProviderMock = null!;
    private Mock<ILogger<AuthService>> _loggerMock = null!;
    private AuthService _service = null!;
    private static readonly CallerContext SystemCaller =
        CallerContext.CreateModuleContext(Guid.CreateVersion7());

    [TestInitialize]
    public void Setup()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null, null, null, null, null, null, null, null);

        // Default: no existing users. Individual tests override this to simulate
        // duplicate usernames/display names.
        _userManagerMock.Setup(m => m.Users)
            .Returns(Enumerable.Empty<ApplicationUser>().AsQueryable());

        var httpContextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            _userManagerMock.Object,
            httpContextAccessor.Object,
            claimsFactory.Object,
            null, null, null, null);

        _tokenManagerMock = new Mock<IOpenIddictTokenManager>();
        _loggerMock = new Mock<ILogger<AuthService>>();
        _adminSettingsMock = new Mock<IAdminSettingsService>();
        _emailSenderMock = new Mock<ITransactionalEmailSender>();
        _smtpOptions = Options.Create(new SmtpOptions());
        _serviceProviderMock = new Mock<IServiceProvider>();

        // By default, return null (setting not found) so existing tests use normal flow
        _adminSettingsMock
            .Setup(s => s.GetSettingAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((SystemSettingDto?)null);

        _service = new AuthService(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _tokenManagerMock.Object,
            _adminSettingsMock.Object,
            _emailSenderMock.Object,
            _smtpOptions,
            _serviceProviderMock.Object,
            _loggerMock.Object);
    }

    // ---------------------------------------------------------------------------
    // RegisterAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task RegisterAsync_ValidRequest_ReturnsResponseWithUserId()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "P@ssw0rd!",
            DisplayName = "Test User",
        };
        ApplicationUser? createdUser = null;
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) =>
            {
                u.Id = Guid.CreateVersion7();
                createdUser = u;
            });
        // Options.SignIn.RequireConfirmedEmail defaults to false in IdentityOptions, so no mock needed

        // Act
        var response = await _service.RegisterAsync(request, SystemCaller);

        // Assert
        Assert.AreEqual(request.Email, response.Email);
        Assert.AreEqual("testuser", createdUser?.UserName, "Created user should use the requested username");
        Assert.IsFalse(response.RequiresEmailConfirmation);
    }

    [TestMethod]
    public async Task RegisterAsync_IdentityFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "bad@example.com",
            Password = "weak",
        };
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordTooShort",
                Description = "Password too short.",
            }));

        // Act & Assert
        try
        {
            await _service.RegisterAsync(request, SystemCaller);
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task RegisterAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            await _service.RegisterAsync(null!, SystemCaller);
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    // ---------------------------------------------------------------------------
    // RegisterAsync — Closed System Mode
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task RegisterAsync_ClosedSystemEnabled_NonAdmin_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "selfreg@example.com",
            Password = "P@ssw0rd!",
            DisplayName = "Self Registrant",
        };

        // Enable closed system mode
        _adminSettingsMock
            .Setup(s => s.GetSettingAsync(SystemSettingKeys.CoreModule, SystemSettingKeys.ClosedSystemEnabled))
            .ReturnsAsync(new SystemSettingDto
            {
                Module = SystemSettingKeys.CoreModule,
                Key = SystemSettingKeys.ClosedSystemEnabled,
                Value = "true",
            });

        // Act & Assert
        try
        {
            await _service.RegisterAsync(request, SystemCaller);
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(ex.Message.Contains("Self-registration is disabled", StringComparison.Ordinal),
                "Exception message should indicate self-registration is disabled");
        }
    }

    [TestMethod]
    public async Task RegisterAsync_ClosedSystemEnabled_Admin_SetsPasswordChangeRequired()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "admincreated",
            Email = "admincreated@example.com",
            Password = "P@ssw0rd!",
            DisplayName = "Admin Created User",
            PasswordChangeRequired = true,
        };

        // Enable closed system mode
        _adminSettingsMock
            .Setup(s => s.GetSettingAsync(SystemSettingKeys.CoreModule, SystemSettingKeys.ClosedSystemEnabled))
            .ReturnsAsync(new SystemSettingDto
            {
                Module = SystemSettingKeys.CoreModule,
                Key = SystemSettingKeys.ClosedSystemEnabled,
                Value = "true",
            });

        ApplicationUser? createdUser = null;
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) =>
            {
                u.Id = Guid.CreateVersion7();
                createdUser = u;
            });

        var adminCaller = new CallerContext(
            Guid.CreateVersion7(),
            new[] { "Administrator" },
            DotNetCloud.Core.Authorization.CallerType.User);

        // Act
        var response = await _service.RegisterAsync(request, adminCaller);

        // Assert
        Assert.IsNotNull(createdUser, "User should have been created");
        Assert.IsTrue(createdUser!.PasswordChangeRequired,
            "Admin-created user should have PasswordChangeRequired = true");
        Assert.AreEqual(request.Email, response.Email);
    }

    [TestMethod]
    public async Task RegisterAsync_ClosedSystemDisabled_AllowsSelfRegistration()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "selfreg@example.com",
            Password = "P@ssw0rd!",
            DisplayName = "Self Registrant",
        };

        // Explicitly set closed system to "false"
        _adminSettingsMock
            .Setup(s => s.GetSettingAsync(SystemSettingKeys.CoreModule, SystemSettingKeys.ClosedSystemEnabled))
            .ReturnsAsync(new SystemSettingDto
            {
                Module = SystemSettingKeys.CoreModule,
                Key = SystemSettingKeys.ClosedSystemEnabled,
                Value = "false",
            });

        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) => { u.Id = Guid.CreateVersion7(); });

        // Act
        var response = await _service.RegisterAsync(request, SystemCaller);

        // Assert
        Assert.AreEqual(request.Email, response.Email,
            "Self-registration should succeed when closed system mode is disabled");
    }

    // ---------------------------------------------------------------------------
    // LoginAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task LoginAsync_ValidCredentials_ReturnsLoginResponse()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "testuser",
            DisplayName = "Test User",
            IsActive = true,
        };
        var request = new LoginRequest { Username = "testuser", Password = "P@ssw0rd!" };

        _userManagerMock.Setup(m => m.FindByNameAsync(request.Username)).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, request.Password)).ReturnsAsync(true);
        _userManagerMock.Setup(m => m.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.GetTwoFactorEnabledAsync(user)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var response = await _service.LoginAsync(request, SystemCaller);

        // Assert
        Assert.AreEqual(userId, response.UserId);
        Assert.AreEqual("Test User", response.DisplayName);
        Assert.AreEqual("Bearer", response.TokenType);
    }

    [TestMethod]
    public async Task LoginAsync_UserNotFound_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var request = new LoginRequest { Username = "noone", Password = "P@ssw0rd!" };
        _userManagerMock.Setup(m => m.FindByNameAsync(request.Username)).ReturnsAsync((ApplicationUser?)null);

        // Act & Assert
        try
        {
            await _service.LoginAsync(request, SystemCaller);
            Assert.Fail("Expected UnauthorizedAccessException");
        }
        catch (UnauthorizedAccessException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task LoginAsync_InvalidPassword_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            DisplayName = "Test User",
            Email = "user@example.com",
            UserName = "testuser",
            IsActive = true,
        };
        var request = new LoginRequest { Username = "testuser", Password = "WrongPass" };

        _userManagerMock.Setup(m => m.FindByNameAsync(request.Username)).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, request.Password)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act & Assert
        try
        {
            await _service.LoginAsync(request, SystemCaller);
            Assert.Fail("Expected UnauthorizedAccessException");
        }
        catch (UnauthorizedAccessException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task LoginAsync_LockedAccount_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            DisplayName = "Locked User",
            Email = "locked@example.com",
            UserName = "lockeduser",
            IsActive = true,
        };
        var request = new LoginRequest { Username = "lockeduser", Password = "P@ssw0rd!" };

        _userManagerMock.Setup(m => m.FindByNameAsync(request.Username)).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(true);

        // Act & Assert
        try
        {
            await _service.LoginAsync(request, SystemCaller);
            Assert.Fail("Expected UnauthorizedAccessException");
        }
        catch (UnauthorizedAccessException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task LoginAsync_InactiveAccount_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            DisplayName = "Inactive User",
            Email = "inactive@example.com",
            UserName = "inactiveuser",
            IsActive = false,
        };
        var request = new LoginRequest { Username = "inactiveuser", Password = "P@ssw0rd!" };

        _userManagerMock.Setup(m => m.FindByNameAsync(request.Username)).ReturnsAsync(user);

        // Act & Assert
        try
        {
            await _service.LoginAsync(request, SystemCaller);
            Assert.Fail("Expected UnauthorizedAccessException");
        }
        catch (UnauthorizedAccessException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task LoginAsync_MfaEnabledNoTotpCode_ThrowsMfaRequired()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), DisplayName = "MFA User", Email = "mfa@example.com", UserName = "mfauser", IsActive = true };
        var request = new LoginRequest { Username = "mfauser", Password = "P@ssw0rd!" };

        _userManagerMock.Setup(m => m.FindByNameAsync(request.Username)).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, request.Password)).ReturnsAsync(true);
        _userManagerMock.Setup(m => m.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.GetTwoFactorEnabledAsync(user)).ReturnsAsync(true);

        // Act
        var ex = await GetMfaRequiredExceptionAsync();

        // Assert
        Assert.AreEqual("MFA_REQUIRED", ex.Message);
    }

    private async Task<InvalidOperationException> GetMfaRequiredExceptionAsync()
    {
        try
        {
            var user = new ApplicationUser { Id = Guid.CreateVersion7(), DisplayName = "MFA User", Email = "mfa@example.com", UserName = "mfauser", IsActive = true };
            var request = new LoginRequest { Username = "mfauser", Password = "P@ssw0rd!" };

            _userManagerMock.Setup(m => m.FindByNameAsync(request.Username)).ReturnsAsync(user);
            _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
            _userManagerMock.Setup(m => m.CheckPasswordAsync(user, request.Password)).ReturnsAsync(true);
            _userManagerMock.Setup(m => m.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(m => m.GetTwoFactorEnabledAsync(user)).ReturnsAsync(true);

            await _service.LoginAsync(request, SystemCaller);
            Assert.Fail("Expected InvalidOperationException");
            return null!;
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    // ---------------------------------------------------------------------------
    // LoginAsync — Closed System Mode
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task LoginAsync_PasswordChangeRequired_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "testuser",
            DisplayName = "Test User",
            IsActive = true,
            PasswordChangeRequired = true,
        };
        var request = new LoginRequest { Username = "testuser", Password = "P@ssw0rd!" };

        _userManagerMock.Setup(m => m.FindByNameAsync(request.Username)).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, request.Password)).ReturnsAsync(true);
        _userManagerMock.Setup(m => m.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.GetTwoFactorEnabledAsync(user)).ReturnsAsync(false);

        // Act & Assert
        try
        {
            await _service.LoginAsync(request, SystemCaller);
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            Assert.AreEqual("PASSWORD_CHANGE_REQUIRED", ex.Message,
                "Login should be blocked with PASSWORD_CHANGE_REQUIRED when flag is set");
        }
    }

    [TestMethod]
    public async Task LoginAsync_PasswordChangeNotRequired_ReturnsLoginResponse()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "testuser",
            DisplayName = "Test User",
            IsActive = true,
            PasswordChangeRequired = false,
        };
        var request = new LoginRequest { Username = "testuser", Password = "P@ssw0rd!" };

        _userManagerMock.Setup(m => m.FindByNameAsync(request.Username)).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, request.Password)).ReturnsAsync(true);
        _userManagerMock.Setup(m => m.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.GetTwoFactorEnabledAsync(user)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var response = await _service.LoginAsync(request, SystemCaller);

        // Assert
        Assert.AreEqual(userId, response.UserId);
        Assert.AreEqual("Test User", response.DisplayName);
        Assert.AreEqual("Bearer", response.TokenType);
    }

    // ---------------------------------------------------------------------------
    // LogoutAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task LogoutAsync_NoRefreshToken_RevokesAllTokensForSubject()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var tokenMock = new Mock<object>();

        _tokenManagerMock
            .Setup(m => m.FindBySubjectAsync(userId.ToString(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable(tokenMock.Object));
        _tokenManagerMock
            .Setup(m => m.TryRevokeAsync(tokenMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _service.LogoutAsync(userId, null, SystemCaller);

        // Assert
        _tokenManagerMock.Verify(
            m => m.TryRevokeAsync(tokenMock.Object, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static async IAsyncEnumerable<T> AsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    // ---------------------------------------------------------------------------
    // ChangePasswordAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenUserFoundAndCurrentPasswordValidThenChangePasswordReturnsTrue()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DisplayName = "Test User",
        };
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldP@ss1!",
            NewPassword = "NewP@ss2!",
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task WhenCurrentPasswordIncorrectThenChangePasswordReturnsFalse()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DisplayName = "Test User",
        };
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "WrongPass!",
            NewPassword = "NewP@ss2!",
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordMismatch",
                Description = "Incorrect password.",
            }));

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task WhenUserNotFoundThenChangePasswordReturnsFalse()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldP@ss1!",
            NewPassword = "NewP@ss2!",
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task WhenChangePasswordCalledWithNullRequestThenThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            await _service.ChangePasswordAsync(Guid.CreateVersion7(), null!);
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    // ---------------------------------------------------------------------------
    // GetUserProfileAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenUserExistsThenGetUserProfileReturnsProfile()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "jdoe",
            DisplayName = "Jane Doe",
            AvatarUrl = "https://example.com/avatar.png",
            Locale = "de-DE",
            Timezone = "Europe/Berlin",
            CreatedAt = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            LastLoginAt = new DateTime(2025, 7, 18, 12, 0, 0, DateTimeKind.Utc),
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "admin", "user" });
        _userManagerMock.Setup(m => m.GetTwoFactorEnabledAsync(user)).ReturnsAsync(true);

        // Act
        var profile = await _service.GetUserProfileAsync(userId);

        // Assert
        Assert.IsNotNull(profile);
        Assert.AreEqual(userId, profile.UserId);
        Assert.AreEqual("jdoe", profile.Username);
        Assert.AreEqual("user@example.com", profile.Email);
        Assert.AreEqual("Jane Doe", profile.DisplayName);
        Assert.AreEqual("https://example.com/avatar.png", profile.AvatarUrl);
        Assert.AreEqual("de-DE", profile.Locale);
        Assert.AreEqual("Europe/Berlin", profile.Timezone);
        Assert.AreEqual(2, profile.Roles.Count);
        Assert.IsTrue(profile.IsMfaEnabled);
        Assert.AreEqual(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), profile.CreatedAt);
        Assert.AreEqual(new DateTime(2025, 7, 18, 12, 0, 0, DateTimeKind.Utc), profile.LastLoginAt);
    }

    [TestMethod]
    public async Task WhenUserNotFoundThenGetUserProfileReturnsNull()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync((ApplicationUser?)null);

        // Act
        var profile = await _service.GetUserProfileAsync(userId);

        // Assert
        Assert.IsNull(profile);
    }

    // ---------------------------------------------------------------------------
    // RegisterAsync — email optional
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task RegisterAsync_EmailOmitted_StoresNullEmail()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "noemailuser",
            Email = null,
            Password = "P@ssw0rd!",
            DisplayName = "No Email User",
        };

        ApplicationUser? createdUser = null;
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) =>
            {
                u.Id = Guid.CreateVersion7();
                createdUser = u;
            });

        // Act
        var response = await _service.RegisterAsync(request, SystemCaller);

        // Assert
        Assert.IsNotNull(createdUser);
        Assert.IsNull(createdUser!.Email, "An omitted email must be stored as null, not empty string");
        Assert.IsNull(response.Email);
        Assert.IsFalse(response.RequiresEmailConfirmation);
    }

    [TestMethod]
    public async Task RegisterAsync_BlankEmail_StoresNullEmail()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "blankemailuser",
            Email = "   ",
            Password = "P@ssw0rd!",
            DisplayName = "Blank Email User",
        };

        ApplicationUser? createdUser = null;
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) =>
            {
                u.Id = Guid.CreateVersion7();
                createdUser = u;
            });

        // Act
        var response = await _service.RegisterAsync(request, SystemCaller);

        // Assert
        Assert.IsNotNull(createdUser);
        Assert.IsNull(createdUser!.Email, "A whitespace email must be stored as null, not empty string");
        Assert.IsNull(response.Email);
    }

    [TestMethod]
    public async Task RegisterAsync_EmailProvided_StoresTrimmedEmail()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "withemail",
            Email = "  with.email@example.com  ",
            Password = "P@ssw0rd!",
            DisplayName = "With Email",
        };

        ApplicationUser? createdUser = null;
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) =>
            {
                u.Id = Guid.CreateVersion7();
                createdUser = u;
            });

        // Act
        var response = await _service.RegisterAsync(request, SystemCaller);

        // Assert
        Assert.IsNotNull(createdUser);
        Assert.AreEqual("with.email@example.com", createdUser!.Email);
        Assert.AreEqual("with.email@example.com", response.Email);
    }

    [TestMethod]
    public async Task RegisterAsync_BlankDisplayName_FallsBackToUsername()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "benkimball",
            Email = "bpkimball@gmail.com",
            Password = "P@ssw0rd!",
            DisplayName = "   ",
        };

        ApplicationUser? createdUser = null;
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) =>
            {
                u.Id = Guid.CreateVersion7();
                createdUser = u;
            });

        // Act
        await _service.RegisterAsync(request, SystemCaller);

        // Assert — display name is the name others see, so it must never be blank
        Assert.IsNotNull(createdUser);
        Assert.AreEqual("benkimball", createdUser!.DisplayName,
            "A blank display name should fall back to the username");
    }

    [TestMethod]
    public async Task RegisterAsync_DisplayNameProvided_IsTrimmed()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "benkimball",
            Email = "bpkimball@gmail.com",
            Password = "P@ssw0rd!",
            DisplayName = "  Ben Kimball  ",
        };

        ApplicationUser? createdUser = null;
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) =>
            {
                u.Id = Guid.CreateVersion7();
                createdUser = u;
            });

        // Act
        await _service.RegisterAsync(request, SystemCaller);

        // Assert
        Assert.IsNotNull(createdUser);
        Assert.AreEqual("Ben Kimball", createdUser!.DisplayName);
    }

    [TestMethod]
    public async Task RegisterAsync_DuplicateDisplayName_ThrowsInvalidOperationException()
    {
        // Arrange — another user already has the display name "Ben Kimball"
        var existing = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "someoneelse",
            DisplayName = "Ben Kimball",
        };
        _userManagerMock.Setup(m => m.Users)
            .Returns(new List<ApplicationUser> { existing }.AsQueryable());

        var request = new RegisterRequest
        {
            Username = "benkimball",
            Email = "bpkimball@gmail.com",
            Password = "P@ssw0rd!",
            DisplayName = "Ben Kimball",
        };

        // Act & Assert
        try
        {
            await _service.RegisterAsync(request, SystemCaller);
            Assert.Fail("Expected InvalidOperationException for duplicate display name");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(ex.Message.Contains("already in use", StringComparison.Ordinal));
        }
    }

    // ---------------------------------------------------------------------------
    // InitiatePasswordResetAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task InitiatePasswordResetAsync_UserWithNoEmail_DoesNotGenerateTokenOrSend()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "noemailuser",
            Email = null,
            DisplayName = "No Email User",
        };

        _userManagerMock.Setup(m => m.FindByNameAsync("noemailuser")).ReturnsAsync(user);

        // Act
        await _service.InitiatePasswordResetAsync("noemailuser");

        // Assert
        _userManagerMock.Verify(
            m => m.GeneratePasswordResetTokenAsync(user),
            Times.Never,
            "No reset token should be generated when the account has no email on file");
        _emailSenderMock.Verify(
            m => m.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "No reset email should be sent when the account has no email on file");
    }

    [TestMethod]
    public async Task InitiatePasswordResetAsync_UserFoundByEmail_GeneratesTokenAndSends()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "testuser",
            Email = "user@example.com",
            DisplayName = "Test User",
        };

        _userManagerMock.Setup(m => m.FindByNameAsync("user@example.com")).ReturnsAsync((ApplicationUser?)null);
        _userManagerMock.Setup(m => m.FindByEmailAsync("user@example.com")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _emailSenderMock
            .Setup(m => m.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.InitiatePasswordResetAsync("user@example.com");

        // Assert
        _userManagerMock.Verify(
            m => m.GeneratePasswordResetTokenAsync(user),
            Times.Once);
        _emailSenderMock.Verify(
            m => m.SendAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once,
            "A reset email should be sent to the account's email address");
    }

    [TestMethod]
    public async Task InitiatePasswordResetAsync_UnknownAccount_DoesNotThrow()
    {
        // Arrange
        _userManagerMock.Setup(m => m.FindByNameAsync("ghost")).ReturnsAsync((ApplicationUser?)null);
        _userManagerMock.Setup(m => m.FindByEmailAsync("ghost")).ReturnsAsync((ApplicationUser?)null);

        // Act & Assert
        await _service.InitiatePasswordResetAsync("ghost");

        _userManagerMock.Verify(
            m => m.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()),
            Times.Never);
        _emailSenderMock.Verify(
            m => m.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}

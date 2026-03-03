using DotNetCloud.Core.Auth.Services;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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
    private Mock<ILogger<AuthService>> _loggerMock = null!;
    private AuthService _service = null!;
    private static readonly CallerContext SystemCaller =
        CallerContext.CreateModuleContext(Guid.NewGuid());

    [TestInitialize]
    public void Setup()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null, null, null, null, null, null, null, null);

        var httpContextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            _userManagerMock.Object,
            httpContextAccessor.Object,
            claimsFactory.Object,
            null, null, null, null);

        _tokenManagerMock = new Mock<IOpenIddictTokenManager>();
        _loggerMock = new Mock<ILogger<AuthService>>();

        _service = new AuthService(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _tokenManagerMock.Object,
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
            Email = "test@example.com",
            Password = "P@ssw0rd!",
            DisplayName = "Test User",
        };
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) => { u.Id = Guid.NewGuid(); });
        // Options.SignIn.RequireConfirmedEmail defaults to false in IdentityOptions, so no mock needed

        // Act
        var response = await _service.RegisterAsync(request, SystemCaller);

        // Assert
        Assert.AreEqual(request.Email, response.Email);
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
    // LoginAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task LoginAsync_ValidCredentials_ReturnsLoginResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "user@example.com",
            DisplayName = "Test User",
            IsActive = true,
        };
        var request = new LoginRequest { Email = "user@example.com", Password = "P@ssw0rd!" };

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, request.Password)).ReturnsAsync(true);
        _userManagerMock.Setup(m => m.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.GetTwoFactorEnabledAsync(user)).ReturnsAsync(false);

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
        var request = new LoginRequest { Email = "noone@example.com", Password = "P@ssw0rd!" };
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync((ApplicationUser?)null);

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
            Id = Guid.NewGuid(),
            DisplayName = "Test User",
            Email = "user@example.com",
            IsActive = true,
        };
        var request = new LoginRequest { Email = "user@example.com", Password = "WrongPass" };

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync(user);
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
            Id = Guid.NewGuid(),
            DisplayName = "Locked User",
            Email = "locked@example.com",
            IsActive = true,
        };
        var request = new LoginRequest { Email = "locked@example.com", Password = "P@ssw0rd!" };

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync(user);
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
            Id = Guid.NewGuid(),
            DisplayName = "Inactive User",
            Email = "inactive@example.com",
            IsActive = false,
        };
        var request = new LoginRequest { Email = "inactive@example.com", Password = "P@ssw0rd!" };

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync(user);

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
        var user = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "MFA User", Email = "mfa@example.com", IsActive = true };
        var request = new LoginRequest { Email = "mfa@example.com", Password = "P@ssw0rd!" };

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync(user);
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
            var user = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "MFA User", Email = "mfa@example.com", IsActive = true };
            var request = new LoginRequest { Email = "mfa@example.com", Password = "P@ssw0rd!" };

            _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync(user);
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
    // LogoutAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task LogoutAsync_NoRefreshToken_RevokesAllTokensForSubject()
    {
        // Arrange
        var userId = Guid.NewGuid();
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
}

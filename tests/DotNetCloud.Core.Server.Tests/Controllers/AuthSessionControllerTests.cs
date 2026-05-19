using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Server.Controllers;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Controllers;

[TestClass]
[DoNotParallelize]
public sealed class AuthSessionControllerTests
{
    private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
    private Mock<SignInManager<ApplicationUser>> _signInManagerMock = null!;
    private Mock<IAdminSettingsService> _adminSettingsMock = null!;
    private Mock<ILogger<AuthSessionController>> _loggerMock = null!;
    private AuthSessionController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            _userManagerMock.Object,
            httpContextAccessor.Object,
            claimsFactory.Object,
            null!, null!, null!, null!);

        _adminSettingsMock = new Mock<IAdminSettingsService>();
        _loggerMock = new Mock<ILogger<AuthSessionController>>();

        _controller = new AuthSessionController(
            _signInManagerMock.Object,
            _userManagerMock.Object,
            _adminSettingsMock.Object,
            _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MfaVerifyAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task MfaVerifyAsync_EmptyCode_RedirectsWithError()
    {
        var result = await _controller.MfaVerifyAsync(code: "", returnUrl: "/");

        var redirect = AssertRedirectToMfaVerify(result);
        Assert.IsTrue(redirect.Contains("Verification code is required"),
            "Expected error about missing code");
    }

    [TestMethod]
    public async Task MfaVerifyAsync_Success_RedirectsToReturnUrl()
    {
        _signInManagerMock
            .Setup(m => m.TwoFactorAuthenticatorSignInAsync("123456", true, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var result = await _controller.MfaVerifyAsync(code: "123456", returnUrl: "/files");

        var redirect = result as LocalRedirectResult;
        Assert.IsNotNull(redirect);
        Assert.AreEqual("/files", redirect.Url);
    }

    [TestMethod]
    public async Task MfaVerifyAsync_Success_DefaultReturnUrl()
    {
        _signInManagerMock
            .Setup(m => m.TwoFactorAuthenticatorSignInAsync("123456", true, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var result = await _controller.MfaVerifyAsync(code: "123456");

        var redirect = result as LocalRedirectResult;
        Assert.IsNotNull(redirect);
        Assert.AreEqual("/", redirect.Url);
    }

    [TestMethod]
    public async Task MfaVerifyAsync_LockedOut_RedirectsWithError()
    {
        _signInManagerMock
            .Setup(m => m.TwoFactorAuthenticatorSignInAsync("123456", true, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        var result = await _controller.MfaVerifyAsync(code: "123456", returnUrl: "/");

        var redirect = AssertRedirectToMfaVerify(result);
        Assert.IsTrue(redirect.Contains("Account locked"),
            "Expected lockout error message");
    }

    [TestMethod]
    public async Task MfaVerifyAsync_InvalidCode_RedirectsWithError()
    {
        _signInManagerMock
            .Setup(m => m.TwoFactorAuthenticatorSignInAsync("000000", true, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var result = await _controller.MfaVerifyAsync(code: "000000", returnUrl: "/");

        var redirect = AssertRedirectToMfaVerify(result);
        Assert.IsTrue(redirect.Contains("Invalid verification code"),
            "Expected invalid code error message");
    }

    [TestMethod]
    public async Task MfaVerifyAsync_UnsafeReturnUrl_DefaultsToRoot()
    {
        _signInManagerMock
            .Setup(m => m.TwoFactorAuthenticatorSignInAsync("123456", true, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var result = await _controller.MfaVerifyAsync(code: "123456", returnUrl: "https://evil.com");

        var redirect = result as LocalRedirectResult;
        Assert.IsNotNull(redirect);
        Assert.AreEqual("/", redirect.Url);
    }

    [TestMethod]
    public async Task MfaVerifyAsync_ExceptionDuringSignIn_RedirectsWithError()
    {
        _signInManagerMock
            .Setup(m => m.TwoFactorAuthenticatorSignInAsync("123456", true, true))
            .ThrowsAsync(new InvalidOperationException("Test failure"));

        var result = await _controller.MfaVerifyAsync(code: "123456", returnUrl: "/");

        var redirect = AssertRedirectToMfaVerify(result);
        Assert.IsTrue(redirect.Contains("Verification error"),
            "Expected error message about verification failure");
    }

    [TestMethod]
    public async Task MfaVerifyAsync_Success_AdminPathWithNonAdmin_RedirectsToRoot()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "user@test.com", DisplayName = "Test User" };

        _signInManagerMock
            .Setup(m => m.TwoFactorAuthenticatorSignInAsync("123456", true, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _signInManagerMock
            .Setup(m => m.GetTwoFactorAuthenticationUserAsync())
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.IsInRoleAsync(user, "Administrator"))
            .ReturnsAsync(false);

        var result = await _controller.MfaVerifyAsync(code: "123456", returnUrl: "/admin/users");

        var redirect = result as LocalRedirectResult;
        Assert.IsNotNull(redirect);
        Assert.AreEqual("/", redirect.Url);
    }

    [TestMethod]
    public async Task MfaVerifyAsync_Success_AdminPathWithAdmin_RedirectsToAdmin()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "admin@test.com", DisplayName = "Admin User" };

        _signInManagerMock
            .Setup(m => m.TwoFactorAuthenticatorSignInAsync("123456", true, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _signInManagerMock
            .Setup(m => m.GetTwoFactorAuthenticationUserAsync())
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.IsInRoleAsync(user, "Administrator"))
            .ReturnsAsync(true);

        var result = await _controller.MfaVerifyAsync(code: "123456", returnUrl: "/admin/users");

        var redirect = result as LocalRedirectResult;
        Assert.IsNotNull(redirect);
        Assert.AreEqual("/admin/users", redirect.Url);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Asserts the result is a LocalRedirect to /auth/mfa-verify and returns the decoded query string.
    /// </summary>
    private static string AssertRedirectToMfaVerify(IActionResult result)
    {
        var redirect = result as LocalRedirectResult;
        Assert.IsNotNull(redirect, "Expected LocalRedirectResult");
        Assert.IsTrue(redirect.Url.StartsWith("/auth/mfa-verify"),
            $"Expected redirect to /auth/mfa-verify but got {redirect.Url}");
        // Decode the URL to check error messages in plain text
        return Uri.UnescapeDataString(redirect.Url);
    }
}

using DotNetCloud.Core.Capabilities;
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
    private Mock<IAuditLogger> _auditLoggerMock = null!;
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
        _auditLoggerMock = new Mock<IAuditLogger>();

        _controller = new AuthSessionController(
            _signInManagerMock.Object,
            _userManagerMock.Object,
            _adminSettingsMock.Object,
            _auditLoggerMock.Object,
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
        var userId = Guid.CreateVersion7();
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
        var userId = Guid.CreateVersion7();
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
    // LoginAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task LoginAsync_InvalidCredentials_LogsAndAuditsFailure()
    {
        _signInManagerMock
            .Setup(m => m.PasswordSignInAsync("kaminskidale@gmail.com", "wrong-password", true, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var result = await _controller.LoginAsync("kaminskidale@gmail.com", "wrong-password", "/");

        var redirect = AssertRedirectToLogin(result);
        Assert.IsTrue(redirect.Contains("Invalid email or password"),
            "Expected invalid credentials error message");

        VerifyLoginFailureAudited("invalid-credentials", "kaminskidale@gmail.com");
    }

    [TestMethod]
    public async Task LoginAsync_LockedOut_LogsAndAuditsFailure()
    {
        _signInManagerMock
            .Setup(m => m.PasswordSignInAsync("kaminskidale@gmail.com", "pw", true, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        var result = await _controller.LoginAsync("kaminskidale@gmail.com", "pw", "/");

        var redirect = AssertRedirectToLogin(result);
        Assert.IsTrue(redirect.Contains("Account locked"),
            "Expected lockout error message");

        VerifyLoginFailureAudited("locked-out", "kaminskidale@gmail.com");
    }

    [TestMethod]
    public async Task LoginAsync_NotAllowed_LogsAndAuditsFailure()
    {
        _signInManagerMock
            .Setup(m => m.PasswordSignInAsync("kaminskidale@gmail.com", "pw", true, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.NotAllowed);

        var result = await _controller.LoginAsync("kaminskidale@gmail.com", "pw", "/");

        var redirect = AssertRedirectToLogin(result);
        Assert.IsTrue(redirect.Contains("confirm your email"),
            "Expected not-allowed error message");

        VerifyLoginFailureAudited("not-allowed", "kaminskidale@gmail.com");
    }

    [TestMethod]
    public async Task LoginAsync_MissingCredentials_RedirectsWithoutAudit()
    {
        var result = await _controller.LoginAsync("", "pw", "/");

        var redirect = AssertRedirectToLogin(result);
        Assert.IsTrue(redirect.Contains("required"),
            "Expected required-fields error message");

        _auditLoggerMock.Verify(
            m => m.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LogoutAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task LogoutAsync_ClearsCookieAndCacheHeaders()
    {
        var result = await _controller.LogoutAsync(returnUrl: null);

        var redirect = result as LocalRedirectResult;
        Assert.IsNotNull(redirect);
        Assert.AreEqual("/auth/login", redirect.Url);

        Assert.AreEqual("\"cache\", \"cookies\", \"storage\"", _controller.Response.Headers["Clear-Site-Data"].ToString());
        Assert.IsTrue(_controller.Response.Headers["Cache-Control"].ToString().Contains("no-store"),
            "Expected Cache-Control: no-store on logout response");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Asserts the result is a LocalRedirect to /auth/login and returns the decoded query string.
    /// </summary>
    private static string AssertRedirectToLogin(IActionResult result)
    {
        var redirect = result as LocalRedirectResult;
        Assert.IsNotNull(redirect, "Expected LocalRedirectResult");
        Assert.IsTrue(redirect.Url.StartsWith("/auth/login"),
            $"Expected redirect to /auth/login but got {redirect.Url}");
        // Decode the URL to check error messages in plain text
        return Uri.UnescapeDataString(redirect.Url);
    }

    /// <summary>
    /// Asserts the audit logger received exactly one form-login-failed entry for the given reason and email.
    /// </summary>
    private void VerifyLoginFailureAudited(string reason, string email)
    {
        _auditLoggerMock.Verify(
            m => m.LogAsync(
                It.Is<AuditEntry>(e =>
                    e.Description == $"form-login-failed:{reason}:{email}"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

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

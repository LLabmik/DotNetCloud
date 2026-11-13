using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Controllers;

/// <summary>
/// Guards the in-app consent approval path. The live Track C run found the prompt had no approve
/// action at all: a module could hit <c>MODULE_CONSENT_REQUIRED</c>, raise the banner, and the user
/// had nowhere to say yes.
/// </summary>
[TestClass]
public class AccountConsentControllerTests
{
    private const string ModuleId = "dotnetcloud.email";

    private Mock<IModuleConsentService> _consent = null!;
    private Mock<IConsentScopeRegistry> _registry = null!;
    private Mock<IAuditLogger> _audit = null!;
    private AccountConsentController _controller = null!;
    private Guid _userId;

    [TestInitialize]
    public void Setup()
    {
        _userId = Guid.CreateVersion7();
        _consent = new Mock<IModuleConsentService>();
        _registry = new Mock<IConsentScopeRegistry>();
        _audit = new Mock<IAuditLogger>();

        _controller = new AccountConsentController(
            _consent.Object,
            _registry.Object,
            _audit.Object,
            Mock.Of<ILogger<AccountConsentController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim("sub", _userId.ToString())], "Test"));
    }

    [TestMethod]
    public async Task GrantModuleAsync_DeclaredScopes_RecordsTheGrantAndAudits()
    {
        _registry
            .Setup(r => r.GetUserConsentScopes(ModuleId))
            .Returns(["files:read", "files:write"]);

        var result = await _controller.GrantModuleAsync(
            ModuleId,
            new GrantModuleConsentRequest(["files:read", "files:write"]),
            CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _consent.Verify(
            c => c.GrantAsync(
                ModuleId,
                _userId,
                It.Is<IReadOnlyCollection<string>>(scopes =>
                    scopes.Count == 2 && scopes.Contains("files:read") && scopes.Contains("files:write")),
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _audit.Verify(
            a => a.LogAsync(
                It.Is<AuditEntry>(entry =>
                    entry.Action == AuditAction.ModuleConsentGranted && entry.ModuleId == ModuleId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GrantModuleAsync_UndeclaredScope_IsRejectedAndNeverGranted()
    {
        // The frame must not be able to talk the user into a scope the module never declared.
        _registry.Setup(r => r.GetUserConsentScopes(ModuleId)).Returns(["files:read"]);

        var result = await _controller.GrantModuleAsync(
            ModuleId,
            new GrantModuleConsentRequest(["files:read", "mail:write"]),
            CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        _consent.Verify(
            c => c.GrantAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GrantModuleAsync_ScopeOutsideTheConsentCatalog_IsIgnored()
    {
        _registry.Setup(r => r.GetUserConsentScopes(ModuleId)).Returns(["files:read"]);

        var result = await _controller.GrantModuleAsync(
            ModuleId,
            // "openid" is an identity scope and "not-a-scope" is unknown: neither can be consented.
            new GrantModuleConsentRequest(["files:read", "openid", "not-a-scope"]),
            CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _consent.Verify(
            c => c.GrantAsync(
                ModuleId,
                _userId,
                It.Is<IReadOnlyCollection<string>>(scopes => scopes.Count == 1 && scopes.Contains("files:read")),
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GrantModuleAsync_ModuleNotLoaded_ReturnsNotFound()
    {
        _registry.Setup(r => r.GetUserConsentScopes(ModuleId)).Returns((IReadOnlyCollection<string>?)null);

        var result = await _controller.GrantModuleAsync(
            ModuleId,
            new GrantModuleConsentRequest(["files:read"]),
            CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        _consent.Verify(
            c => c.GrantAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GrantModuleAsync_NoUsableScopes_ReturnsBadRequest()
    {
        var result = await _controller.GrantModuleAsync(
            ModuleId,
            new GrantModuleConsentRequest([]),
            CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }
}

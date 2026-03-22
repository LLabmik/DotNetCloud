using System.Security.Claims;
using DotNetCloud.Modules.Files.Host.Controllers;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Host;

/// <summary>
/// Security regression tests for the controller security audit fixes:
///   - WopiController.EndSessionAsync uses authenticated caller (not user-supplied userId)
///   - WopiController.GenerateTokenAsync uses authenticated caller
///   - TrashController actions use authenticated caller
///   - ShareController/MySharesController actions use authenticated caller
///   - Unauthenticated requests return 403 (via ExecuteAsync exception handler)
/// </summary>
[TestClass]
public class ControllerSecurityAuditTests
{
    #region WopiController: EndSession uses authenticated caller

    [TestMethod]
    public void EndSession_UsesAuthenticatedCallerId_NotSpoofable()
    {
        // The critical fix: EndSession previously accepted a userId query param,
        // allowing any authenticated user to terminate another user's editing session.
        // Now it derives the userId from the bearer token via GetAuthenticatedCaller().
        var authenticatedUserId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var sessionTrackerMock = new Mock<IWopiSessionTracker>();
        var controller = CreateWopiController(sessionTrackerMock: sessionTrackerMock);
        SetAuthenticatedUser(controller, authenticatedUserId);

        var result = controller.EndSessionAsync(fileId);

        sessionTrackerMock.Verify(
            s => s.EndSession(fileId, authenticatedUserId),
            Times.Once,
            "EndSession must use the authenticated caller's ID from the bearer token");
        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public void EndSession_Unauthenticated_Throws()
    {
        // EndSession is not wrapped in ExecuteAsync, so ForbiddenException propagates directly
        var controller = CreateWopiController();
        SetUnauthenticatedUser(controller);

        Assert.ThrowsExactly<DotNetCloud.Core.Errors.ForbiddenException>(
            () => controller.EndSessionAsync(Guid.NewGuid()));
    }

    #endregion

    #region TrashController: all actions use authenticated caller

    [TestMethod]
    public async Task TrashList_UsesAuthenticatedCaller()
    {
        var userId = Guid.NewGuid();
        var trashMock = new Mock<ITrashService>();
        trashMock.Setup(t => t.ListTrashAsync(
            It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == userId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var controller = CreateTrashController(trashMock);
        SetAuthenticatedUser(controller, userId);

        var result = await controller.ListAsync();

        trashMock.Verify(
            t => t.ListTrashAsync(
                It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TrashList_Unauthenticated_Returns403()
    {
        var controller = CreateTrashController();
        SetUnauthenticatedUser(controller);

        var result = await controller.ListAsync() as ObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(403, result.StatusCode);
    }

    [TestMethod]
    public async Task TrashGetSize_UsesAuthenticatedCaller()
    {
        var userId = Guid.NewGuid();
        var trashMock = new Mock<ITrashService>();
        trashMock.Setup(t => t.GetTrashSizeAsync(
            It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == userId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);

        var controller = CreateTrashController(trashMock);
        SetAuthenticatedUser(controller, userId);

        await controller.GetSizeAsync();

        trashMock.Verify(
            t => t.GetTrashSizeAsync(
                It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TrashRestore_UsesAuthenticatedCaller()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var trashMock = new Mock<ITrashService>();
        trashMock.Setup(t => t.RestoreAsync(
            nodeId,
            It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == userId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DotNetCloud.Modules.Files.DTOs.FileNodeDto
            {
                Id = nodeId,
                Name = "restored.txt",
                NodeType = "file"
            });

        var controller = CreateTrashController(trashMock);
        SetAuthenticatedUser(controller, userId);

        await controller.RestoreAsync(nodeId);

        trashMock.Verify(
            t => t.RestoreAsync(
                nodeId,
                It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TrashPurge_UsesAuthenticatedCaller()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var trashMock = new Mock<ITrashService>();

        var controller = CreateTrashController(trashMock);
        SetAuthenticatedUser(controller, userId);

        await controller.PurgeAsync(nodeId);

        trashMock.Verify(
            t => t.PermanentDeleteAsync(
                nodeId,
                It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TrashEmpty_UsesAuthenticatedCaller()
    {
        var userId = Guid.NewGuid();
        var trashMock = new Mock<ITrashService>();

        var controller = CreateTrashController(trashMock);
        SetAuthenticatedUser(controller, userId);

        await controller.EmptyAsync();

        trashMock.Verify(
            t => t.EmptyTrashAsync(
                It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ShareController: uses authenticated caller

    [TestMethod]
    public async Task ShareList_Unauthenticated_Returns403()
    {
        var controller = CreateShareController();
        SetUnauthenticatedUser(controller);

        var result = await controller.ListAsync(Guid.NewGuid()) as ObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(403, result.StatusCode);
    }

    #endregion

    #region MySharesController: uses authenticated caller

    [TestMethod]
    public async Task MyShares_Unauthenticated_Returns403()
    {
        var controller = CreateMySharesController();
        SetUnauthenticatedUser(controller);

        var result = await controller.GetAsync() as ObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(403, result.StatusCode);
    }

    #endregion

    #region Helpers

    private static WopiController CreateWopiController(
        Mock<IWopiSessionTracker>? sessionTrackerMock = null)
    {
        return new WopiController(
            Mock.Of<IWopiService>(),
            Mock.Of<IWopiTokenService>(),
            Mock.Of<ICollaboraDiscoveryService>(),
            Mock.Of<IWopiProofKeyValidator>(),
            sessionTrackerMock?.Object ?? Mock.Of<IWopiSessionTracker>(),
            Microsoft.Extensions.Options.Options.Create(new CollaboraOptions()),
            NullLogger<WopiController>.Instance);
    }

    private static TrashController CreateTrashController(Mock<ITrashService>? trashMock = null)
    {
        return new TrashController(trashMock?.Object ?? Mock.Of<ITrashService>());
    }

    private static ShareController CreateShareController(Mock<IShareService>? shareMock = null)
    {
        return new ShareController(shareMock?.Object ?? Mock.Of<IShareService>());
    }

    private static MySharesController CreateMySharesController(Mock<IShareService>? shareMock = null)
    {
        return new MySharesController(shareMock?.Object ?? Mock.Of<IShareService>());
    }

    private static void SetAuthenticatedUser(ControllerBase controller, Guid userId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        ], authenticationType: "TestAuth");

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
                RequestServices = services.BuildServiceProvider()
            }
        };
    }

    private static void SetUnauthenticatedUser(ControllerBase controller)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider()
            }
        };
    }

    #endregion
}

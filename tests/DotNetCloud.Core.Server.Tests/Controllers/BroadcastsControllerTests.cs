using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Controllers;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Controllers;

/// <summary>
/// Tests for <see cref="BroadcastsController"/>.
/// </summary>
[TestClass]
public sealed class BroadcastsControllerTests
{
    private Mock<IAdminBroadcastService> _serviceMock = null!;
    private BroadcastsController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _serviceMock = new Mock<IAdminBroadcastService>();
        _controller = new BroadcastsController(_serviceMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    [TestMethod]
    public async Task GetActiveAsync_WhenBroadcastActive_ReturnsOkWithPayload()
    {
        var userId = Guid.CreateVersion7();
        SetUser(userId);
        var active = new ActiveAdminBroadcastDto
        {
            Id = Guid.CreateVersion7(),
            Title = "Reboot",
            Message = "Server reboots shortly.",
            Severity = AdminBroadcastSeverity.Critical,
            SentAtUtc = DateTime.UtcNow,
        };

        _serviceMock
            .Setup(s => s.GetActiveForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(active);

        var result = await _controller.GetActiveAsync();

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _serviceMock.Verify(
            s => s.GetActiveForUserAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetActiveAsync_WhenNothingActive_ReturnsOkWithNull()
    {
        SetUser(Guid.CreateVersion7());
        _serviceMock
            .Setup(s => s.GetActiveForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActiveAdminBroadcastDto?)null);

        var result = await _controller.GetActiveAsync();

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetActiveAsync_WhenUserClaimMissing_ReturnsUnauthorized()
    {
        var result = await _controller.GetActiveAsync();

        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
    }

    [TestMethod]
    public async Task DismissAsync_RecordsDismissalForCurrentUser()
    {
        var userId = Guid.CreateVersion7();
        var broadcastId = Guid.CreateVersion7();
        SetUser(userId);

        var result = await _controller.DismissAsync(broadcastId);

        Assert.IsInstanceOfType<NoContentResult>(result);
        _serviceMock.Verify(
            s => s.DismissAsync(broadcastId, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DismissAsync_WhenUserClaimMissing_ReturnsUnauthorized()
    {
        var result = await _controller.DismissAsync(Guid.CreateVersion7());

        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
        _serviceMock.Verify(
            s => s.DismissAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetUser(Guid userId)
    {
        var identity = new ClaimsIdentity([new Claim("sub", userId.ToString())], "TestAuth");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }
}

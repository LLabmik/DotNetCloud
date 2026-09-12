using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Controllers;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Controllers;

/// <summary>
/// Tests for <see cref="AdminBroadcastsController"/>.
/// </summary>
[TestClass]
public sealed class AdminBroadcastsControllerTests
{
    private Mock<IAdminBroadcastService> _serviceMock = null!;
    private Mock<IAuditLogger> _auditMock = null!;
    private AdminBroadcastsController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _serviceMock = new Mock<IAdminBroadcastService>();
        _auditMock = new Mock<IAuditLogger>();
        _auditMock
            .Setup(a => a.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _controller = new AdminBroadcastsController(
            _serviceMock.Object,
            _auditMock.Object,
            NullLogger<AdminBroadcastsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    [TestMethod]
    public async Task ListAsync_ReturnsOkWithBroadcasts()
    {
        _serviceMock
            .Setup(s => s.ListAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([SampleDto()]);

        var result = await _controller.ListAsync();

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateAsync_WhenRequestValid_CreatesAndAudits()
    {
        SetAdmin(Guid.CreateVersion7());
        _serviceMock
            .Setup(s => s.CreateAsync(It.IsAny<CreateAdminBroadcastRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleDto());

        var result = await _controller.CreateAsync(new CreateAdminBroadcastRequest
        {
            Title = "Reboot",
            Message = "Server reboots shortly.",
            Severity = AdminBroadcastSeverity.Warning,
        });

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _auditMock.Verify(
            a => a.LogAsync(
                It.Is<AuditEntry>(e => e.Action == AuditAction.Create && e.EntityType == "AdminBroadcast"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreateAsync_WhenTitleOrMessageMissing_ReturnsBadRequest()
    {
        SetAdmin(Guid.CreateVersion7());

        var result = await _controller.CreateAsync(new CreateAdminBroadcastRequest
        {
            Title = " ",
            Message = "Body",
        });

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        _serviceMock.Verify(
            s => s.CreateAsync(It.IsAny<CreateAdminBroadcastRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task CreateAsync_WhenServiceRejectsRequest_ReturnsBadRequest()
    {
        SetAdmin(Guid.CreateVersion7());
        _serviceMock
            .Setup(s => s.CreateAsync(It.IsAny<CreateAdminBroadcastRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Expiry must be later than the scheduled send time."));

        var result = await _controller.CreateAsync(new CreateAdminBroadcastRequest
        {
            Title = "Reboot",
            Message = "Server reboots shortly.",
        });

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateAsync_WhenUserClaimMissing_ReturnsUnauthorized()
    {
        var result = await _controller.CreateAsync(new CreateAdminBroadcastRequest
        {
            Title = "Reboot",
            Message = "Server reboots shortly.",
        });

        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
    }

    [TestMethod]
    public async Task SendNowAsync_WhenBroadcastSent_ReturnsOkAndAudits()
    {
        SetAdmin(Guid.CreateVersion7());
        var broadcastId = Guid.CreateVersion7();
        _serviceMock
            .Setup(s => s.SendNowAsync(broadcastId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.SendNowAsync(broadcastId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _auditMock.Verify(
            a => a.LogAsync(It.Is<AuditEntry>(e => e.Action == AuditAction.Update), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendNowAsync_WhenNothingToSend_ReturnsNotFound()
    {
        SetAdmin(Guid.CreateVersion7());
        _serviceMock
            .Setup(s => s.SendNowAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.SendNowAsync(Guid.CreateVersion7());

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DeleteAsync_WhenDeleted_ReturnsNoContentAndAudits()
    {
        SetAdmin(Guid.CreateVersion7());
        var broadcastId = Guid.CreateVersion7();
        _serviceMock
            .Setup(s => s.DeleteAsync(broadcastId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteAsync(broadcastId);

        Assert.IsInstanceOfType<NoContentResult>(result);
        _auditMock.Verify(
            a => a.LogAsync(It.Is<AuditEntry>(e => e.Action == AuditAction.Delete), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_WhenBroadcastMissing_ReturnsNotFound()
    {
        SetAdmin(Guid.CreateVersion7());
        _serviceMock
            .Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DeleteAsync(Guid.CreateVersion7());

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    private void SetAdmin(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.Role, "Administrator"),
            ], "TestAuth");

        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    private static AdminBroadcastDto SampleDto() => new()
    {
        Id = Guid.CreateVersion7(),
        Title = "Reboot",
        Message = "Server reboots shortly.",
        Severity = AdminBroadcastSeverity.Warning,
        Status = AdminBroadcastStatus.Sent,
        CreatedByUserId = Guid.CreateVersion7(),
        CreatedAtUtc = DateTime.UtcNow,
        SentAtUtc = DateTime.UtcNow,
    };
}

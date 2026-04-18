using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Controllers;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Controllers;

[TestClass]
public class UserSettingsControllerTests
{
    private Mock<IUserSettingsService> _settingsServiceMock = null!;
    private Mock<ILogger<UserSettingsController>> _loggerMock = null!;
    private UserSettingsController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _settingsServiceMock = new Mock<IUserSettingsService>();
        _loggerMock = new Mock<ILogger<UserSettingsController>>();
        _controller = new UserSettingsController(_settingsServiceMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [TestMethod]
    public async Task GetSettingAsync_WithAuthenticatedUserAndExistingSetting_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserClaim("sub", userId.ToString());

        _settingsServiceMock
            .Setup(s => s.GetSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed"))
            .ReturnsAsync(new UserSettingDto
            {
                UserId = userId,
                Module = "dotnetcloud.ui",
                Key = "navbar.collapsed",
                Value = "true"
            });

        // Act
        var result = await _controller.GetSettingAsync("dotnetcloud.ui", "navbar.collapsed");

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(result);
        _settingsServiceMock.Verify(
            s => s.GetSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed"),
            Times.Once);
    }

    [TestMethod]
    public async Task GetSettingAsync_WithAuthenticatedUserAndMissingSetting_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserClaim("sub", userId.ToString());

        _settingsServiceMock
            .Setup(s => s.GetSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed"))
            .ReturnsAsync((UserSettingDto?)null);

        // Act
        var result = await _controller.GetSettingAsync("dotnetcloud.ui", "navbar.collapsed");

        // Assert
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetSettingAsync_WithInvalidUserClaim_ReturnsUnauthorized()
    {
        // Arrange
        SetUserClaim("sub", "not-a-guid");

        // Act
        var result = await _controller.GetSettingAsync("dotnetcloud.ui", "navbar.collapsed");

        // Assert
        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
        _settingsServiceMock.Verify(
            s => s.GetSettingAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public async Task UpsertSettingAsync_WithAuthenticatedUser_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserClaim("sub", userId.ToString());

        var dto = new UpsertUserSettingDto
        {
            Value = "true",
            Description = "Navbar state",
            IsSensitive = false,
        };

        _settingsServiceMock
            .Setup(s => s.UpsertSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed", dto))
            .ReturnsAsync(new UserSettingDto
            {
                UserId = userId,
                Module = "dotnetcloud.ui",
                Key = "navbar.collapsed",
                Value = "true",
                Description = "Navbar state",
                IsSensitive = false,
            });

        // Act
        var result = await _controller.UpsertSettingAsync("dotnetcloud.ui", "navbar.collapsed", dto);

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(result);
        _settingsServiceMock.Verify(
            s => s.UpsertSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed", dto),
            Times.Once);
    }

    [TestMethod]
    public async Task UpsertSettingAsync_WithMissingUserClaim_ReturnsUnauthorized()
    {
        // Act
        var result = await _controller.UpsertSettingAsync(
            "dotnetcloud.ui",
            "navbar.collapsed",
            new UpsertUserSettingDto { Value = "false" });

        // Assert
        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
        _settingsServiceMock.Verify(
            s => s.UpsertSettingAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpsertUserSettingDto>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GetSettingAsync_WithUserIdClaim_UsesFallbackClaimType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserClaim("sub", userId.ToString());

        _settingsServiceMock
            .Setup(s => s.GetSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed"))
            .ReturnsAsync(new UserSettingDto
            {
                UserId = userId,
                Module = "dotnetcloud.ui",
                Key = "navbar.collapsed",
                Value = "false"
            });

        // Act
        var result = await _controller.GetSettingAsync("dotnetcloud.ui", "navbar.collapsed");

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(result);
        _settingsServiceMock.Verify(
            s => s.GetSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed"),
            Times.Once);
    }

    [TestMethod]
    public async Task UpsertSettingAsync_WithNameIdentifierClaim_UsesFallbackClaimType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserClaim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString());

        var dto = new UpsertUserSettingDto
        {
            Value = "false",
            Description = "Navbar state",
            IsSensitive = false,
        };

        _settingsServiceMock
            .Setup(s => s.UpsertSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed", dto))
            .ReturnsAsync(new UserSettingDto
            {
                UserId = userId,
                Module = "dotnetcloud.ui",
                Key = "navbar.collapsed",
                Value = "false",
                Description = "Navbar state",
                IsSensitive = false,
            });

        // Act
        var result = await _controller.UpsertSettingAsync("dotnetcloud.ui", "navbar.collapsed", dto);

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(result);
        _settingsServiceMock.Verify(
            s => s.UpsertSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed", dto),
            Times.Once);
    }

    private void SetUserClaim(string claimType, string claimValue)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(claimType, claimValue) }, "TestAuth");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }
}

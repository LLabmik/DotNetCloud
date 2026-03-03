using DotNetCloud.Core.Auth.Services;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Auth.Tests.Services;

/// <summary>
/// Tests for <see cref="UserManagementService"/>.
/// </summary>
[TestClass]
public class UserManagementServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
    private Mock<ILogger<UserManagementService>> _loggerMock = null!;
    private UserManagementService _service = null!;

    private static readonly ApplicationUser TestUser = new()
    {
        Id = Guid.NewGuid(),
        UserName = "user@example.com",
        Email = "user@example.com",
        DisplayName = "Test User",
        Locale = "en-US",
        Timezone = "UTC",
        IsActive = true,
        EmailConfirmed = true,
        CreatedAt = DateTime.UtcNow.AddDays(-30),
        LastLoginAt = DateTime.UtcNow.AddHours(-1),
    };

    [TestInitialize]
    public void Setup()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null, null, null, null, null, null, null, null);

        _loggerMock = new Mock<ILogger<UserManagementService>>();

        _service = new UserManagementService(
            _userManagerMock.Object,
            _loggerMock.Object);
    }

    // ---------------------------------------------------------------------------
    // GetUserAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenUserExistsThenGetUserReturnsDto()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(TestUser.Id.ToString()))
            .ReturnsAsync(TestUser);
        _userManagerMock
            .Setup(m => m.GetRolesAsync(TestUser))
            .ReturnsAsync(new List<string> { "admin" });

        // Act
        var result = await _service.GetUserAsync(TestUser.Id);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(TestUser.Id, result.Id);
        Assert.AreEqual(TestUser.Email, result.Email);
        Assert.AreEqual(TestUser.DisplayName, result.DisplayName);
        Assert.IsTrue(result.Roles.Contains("admin"));
    }

    [TestMethod]
    public async Task WhenUserDoesNotExistThenGetUserReturnsNull()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.GetUserAsync(Guid.NewGuid());

        // Assert
        Assert.IsNull(result);
    }

    // ---------------------------------------------------------------------------
    // UpdateUserAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenUserExistsThenUpdateUserReturnsUpdatedDto()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(TestUser.Id.ToString()))
            .ReturnsAsync(TestUser);
        _userManagerMock
            .Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.GetRolesAsync(TestUser))
            .ReturnsAsync(new List<string>());

        var dto = new UpdateUserDto { DisplayName = "Updated Name" };

        // Act
        var result = await _service.UpdateUserAsync(TestUser.Id, dto);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Updated Name", result.DisplayName);
    }

    [TestMethod]
    public async Task WhenUserDoesNotExistThenUpdateUserReturnsNull()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.UpdateUserAsync(Guid.NewGuid(), new UpdateUserDto());

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task WhenUpdateFailsThenUpdateUserThrowsInvalidOperationException()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(TestUser.Id.ToString()))
            .ReturnsAsync(TestUser);
        _userManagerMock
            .Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "ERR", Description = "Update failed" }));

        // Act & Assert
        try
        {
            await _service.UpdateUserAsync(TestUser.Id, new UpdateUserDto());
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }
    }

    // ---------------------------------------------------------------------------
    // DeleteUserAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenUserExistsThenDeleteUserReturnsTrue()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(TestUser.Id.ToString()))
            .ReturnsAsync(TestUser);
        _userManagerMock
            .Setup(m => m.DeleteAsync(TestUser))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.DeleteUserAsync(TestUser.Id);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task WhenUserDoesNotExistThenDeleteUserReturnsFalse()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.DeleteUserAsync(Guid.NewGuid());

        // Assert
        Assert.IsFalse(result);
    }

    // ---------------------------------------------------------------------------
    // DisableUserAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenUserExistsThenDisableUserReturnsTrueAndSetsInactive()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "active@example.com",
            Email = "active@example.com",
            DisplayName = "Active User",
            IsActive = true,
        };
        _userManagerMock
            .Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.DisableUserAsync(user.Id);

        // Assert
        Assert.IsTrue(result);
        Assert.IsFalse(user.IsActive);
    }

    [TestMethod]
    public async Task WhenUserDoesNotExistThenDisableUserReturnsFalse()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.DisableUserAsync(Guid.NewGuid());

        // Assert
        Assert.IsFalse(result);
    }

    // ---------------------------------------------------------------------------
    // EnableUserAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenUserExistsThenEnableUserReturnsTrueAndSetsActive()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "disabled@example.com",
            Email = "disabled@example.com",
            DisplayName = "Disabled User",
            IsActive = false,
        };
        _userManagerMock
            .Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.EnableUserAsync(user.Id);

        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(user.IsActive);
    }

    [TestMethod]
    public async Task WhenUserDoesNotExistThenEnableUserReturnsFalse()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.EnableUserAsync(Guid.NewGuid());

        // Assert
        Assert.IsFalse(result);
    }

    // ---------------------------------------------------------------------------
    // AdminResetPasswordAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenUserExistsThenAdminResetPasswordReturnsTrue()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(TestUser.Id.ToString()))
            .ReturnsAsync(TestUser);
        _userManagerMock
            .Setup(m => m.GeneratePasswordResetTokenAsync(TestUser))
            .ReturnsAsync("reset-token");
        _userManagerMock
            .Setup(m => m.ResetPasswordAsync(TestUser, "reset-token", "NewP@ssw0rd!"))
            .ReturnsAsync(IdentityResult.Success);

        var request = new AdminResetPasswordRequest { NewPassword = "NewP@ssw0rd!" };

        // Act
        var result = await _service.AdminResetPasswordAsync(TestUser.Id, request);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task WhenUserDoesNotExistThenAdminResetPasswordReturnsFalse()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.AdminResetPasswordAsync(
            Guid.NewGuid(), new AdminResetPasswordRequest { NewPassword = "NewP@ss!" });

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task WhenPasswordDoesNotMeetRequirementsThenAdminResetPasswordReturnsFalse()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByIdAsync(TestUser.Id.ToString()))
            .ReturnsAsync(TestUser);
        _userManagerMock
            .Setup(m => m.GeneratePasswordResetTokenAsync(TestUser))
            .ReturnsAsync("reset-token");
        _userManagerMock
            .Setup(m => m.ResetPasswordAsync(TestUser, "reset-token", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Weak", Description = "Too weak" }));

        var request = new AdminResetPasswordRequest { NewPassword = "weak" };

        // Act
        var result = await _service.AdminResetPasswordAsync(TestUser.Id, request);

        // Assert
        Assert.IsFalse(result);
    }
}

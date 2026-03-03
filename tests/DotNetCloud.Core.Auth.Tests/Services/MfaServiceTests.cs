using System.Security.Claims;
using DotNetCloud.Core.Auth.Services;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Auth;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Naming;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Auth.Tests.Services;

/// <summary>
/// Tests for <see cref="MfaService"/>.
/// </summary>
[TestClass]
public class MfaServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
    private CoreDbContext _dbContext = null!;
    private Mock<ILogger<MfaService>> _loggerMock = null!;
    private MfaService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null, null, null, null, null, null, null, null);

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"MfaTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        _loggerMock = new Mock<ILogger<MfaService>>();

        _service = new MfaService(_userManagerMock.Object, _dbContext, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task GenerateBackupCodes_Returns10Codes()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var response = await _service.GenerateBackupCodesAsync(userId);

        // Assert
        Assert.AreEqual(10, response.Codes.Count);
        Assert.IsTrue(response.Codes.All(c => c.Length == 8));
    }

    [TestMethod]
    public async Task GenerateBackupCodes_AllCodesAreUnique()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var response = await _service.GenerateBackupCodesAsync(userId);

        // Assert
        var distinct = response.Codes.Distinct().Count();
        Assert.AreEqual(10, distinct);
    }

    [TestMethod]
    public async Task GenerateBackupCodes_StoresHashedCodes()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        await _service.GenerateBackupCodesAsync(userId);

        // Assert
        var stored = await _dbContext.UserBackupCodes
            .Where(c => c.UserId == userId)
            .ToListAsync();
        Assert.AreEqual(10, stored.Count);
        Assert.IsTrue(stored.All(c => !c.IsUsed));
        Assert.IsTrue(stored.All(c => c.CodeHash.Length > 0));
    }

    [TestMethod]
    public async Task GenerateBackupCodes_RegeneratingInvalidatesOldCodes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _service.GenerateBackupCodesAsync(userId);

        // Act — regenerate
        await _service.GenerateBackupCodesAsync(userId);

        // Assert — only 10 codes exist (old ones replaced)
        var count = await _dbContext.UserBackupCodes
            .CountAsync(c => c.UserId == userId);
        Assert.AreEqual(10, count);
    }

    [TestMethod]
    public async Task UseBackupCode_ValidCode_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var response = await _service.GenerateBackupCodesAsync(userId);
        var validCode = response.Codes[0];

        // Act
        var result = await _service.UseBackupCodeAsync(userId, validCode);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task UseBackupCode_ValidCode_MarksCodeAsUsed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var response = await _service.GenerateBackupCodesAsync(userId);
        var validCode = response.Codes[0];

        // Act
        await _service.UseBackupCodeAsync(userId, validCode);

        // Assert — code is marked used with a timestamp
        var usedCodes = await _dbContext.UserBackupCodes
            .Where(c => c.UserId == userId && c.IsUsed)
            .ToListAsync();
        Assert.AreEqual(1, usedCodes.Count);
        Assert.IsNotNull(usedCodes[0].UsedAt);
    }

    [TestMethod]
    public async Task UseBackupCode_AlreadyUsedCode_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var response = await _service.GenerateBackupCodesAsync(userId);
        var validCode = response.Codes[0];
        await _service.UseBackupCodeAsync(userId, validCode);

        // Act — try to reuse the same code
        var result = await _service.UseBackupCodeAsync(userId, validCode);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UseBackupCode_InvalidCode_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _service.GenerateBackupCodesAsync(userId);

        // Act
        var result = await _service.UseBackupCodeAsync(userId, "INVALID1");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UseBackupCode_WrongUserId_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var response = await _service.GenerateBackupCodesAsync(userId);
        var validCode = response.Codes[0];
        var differentUserId = Guid.NewGuid();

        // Act
        var result = await _service.UseBackupCodeAsync(differentUserId, validCode);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task VerifyTotp_ValidCode_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, DisplayName = "Test User" };
        _userManagerMock
            .Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.VerifyTwoFactorTokenAsync(
                user,
                It.IsAny<string>(),
                "123456"))
            .ReturnsAsync(true);

        // Act
        var result = await _service.VerifyTotpAsync(userId, "123456");

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task VerifyTotp_InvalidCode_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, DisplayName = "Test User" };
        _userManagerMock
            .Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.VerifyTwoFactorTokenAsync(
                user,
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.VerifyTotpAsync(userId, "wrong");

        // Assert
        Assert.IsFalse(result);
    }
}

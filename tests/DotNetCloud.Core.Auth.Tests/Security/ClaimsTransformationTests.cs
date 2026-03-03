using System.Security.Claims;
using DotNetCloud.Core.Auth.Security;
using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Core.Auth.Tests.Security;

/// <summary>
/// Tests for <see cref="DotNetCloudClaimsTransformation"/>.
/// </summary>
[TestClass]
public class ClaimsTransformationTests
{
    private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
    private IMemoryCache _cache = null!;
    private Mock<ILogger<DotNetCloudClaimsTransformation>> _loggerMock = null!;
    private DotNetCloudClaimsTransformation _transformation = null!;

    [TestInitialize]
    public void Setup()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null, null, null, null, null, null, null, null);

        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<DotNetCloudClaimsTransformation>>();

        _transformation = new DotNetCloudClaimsTransformation(
            _userManagerMock.Object, _cache, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _cache.Dispose();
    }

    [TestMethod]
    public async Task TransformAsync_UnauthenticatedPrincipal_ReturnsSamePrincipal()
    {
        // Arrange — no NameIdentifier or sub claim
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        Assert.AreSame(principal, result);
        _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task TransformAsync_UserNotInDb_ReturnsPrincipalUnchanged()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _userManagerMock
            .Setup(m => m.FindByIdAsync(userId))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert — principal returned unchanged (no extra claims added)
        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [TestMethod]
    public async Task TransformAsync_AddsRoleClaims_WhenUserFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            DisplayName = "Test User",
            Locale = "en-US",
            Timezone = "UTC",
        };
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _userManagerMock
            .Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Admin", "User" });

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        var roles = result.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        CollectionAssert.Contains(roles, "Admin");
        CollectionAssert.Contains(roles, "User");
    }

    [TestMethod]
    public async Task TransformAsync_AddsDncLocaleClaim_WhenUserFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            DisplayName = "French User",
            Locale = "fr-FR",
            Timezone = "Europe/Paris",
        };
        var claims = new[] { new Claim("sub", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync([]);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        Assert.AreEqual("fr-FR", result.FindFirstValue("dnc:locale"));
        Assert.AreEqual("Europe/Paris", result.FindFirstValue("dnc:tz"));
    }

    [TestMethod]
    public async Task TransformAsync_SecondCall_UsesCachedClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, DisplayName = "Cache User", Locale = "en-US", Timezone = "UTC" };
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync([]);

        var identity1 = new ClaimsIdentity(claims, "TestAuth");
        var principal1 = new ClaimsPrincipal(identity1);

        var identity2 = new ClaimsIdentity(claims, "TestAuth");
        var principal2 = new ClaimsPrincipal(identity2);

        // Act
        await _transformation.TransformAsync(principal1);
        await _transformation.TransformAsync(principal2);

        // Assert — DB was queried only once; second call hit cache
        _userManagerMock.Verify(m => m.FindByIdAsync(userId.ToString()), Times.Once);
    }
}

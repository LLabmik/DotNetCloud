using System.Security.Claims;
using DotNetCloud.Core.Auth.Capabilities;
using DotNetCloud.Core.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;

namespace DotNetCloud.Core.Auth.Tests.Capabilities;

/// <summary>
/// Tests for <see cref="CurrentUserContextService"/>.
/// </summary>
[TestClass]
public class CurrentUserContextServiceTests
{
    private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
    private CurrentUserContextService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _service = new CurrentUserContextService(_httpContextAccessorMock.Object);
    }

    [TestMethod]
    public void GetCurrentCaller_WhenNoHttpContext_ReturnsNull()
    {
        // Arrange
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _service.GetCurrentCaller();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetCurrentCaller_WhenNotAuthenticated_ReturnsNull()
    {
        // Arrange
        var context = new DefaultHttpContext();
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

        // Act
        var result = _service.GetCurrentCaller();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetCurrentCaller_WhenAuthenticatedWithNameIdentifier_ReturnsCallerContext()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimTypes.Role, "user")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

        // Act
        var result = _service.GetCurrentCaller();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(CallerType.User, result.Type);
        Assert.AreEqual(2, result.Roles.Count);
        Assert.IsTrue(result.HasRole("admin"));
        Assert.IsTrue(result.HasRole("user"));
    }

    [TestMethod]
    public void GetCurrentCaller_WhenAuthenticatedWithSubClaim_ReturnsCallerContext()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.Role, "editor")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

        // Act
        var result = _service.GetCurrentCaller();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.UserId);
        Assert.IsTrue(result.HasRole("editor"));
    }

    [TestMethod]
    public void GetCurrentCaller_WhenUserIdNotValidGuid_ReturnsNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid"),
            new Claim(ClaimTypes.Role, "user")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

        // Act
        var result = _service.GetCurrentCaller();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetCurrentCaller_WhenNoRoles_ReturnsCallerWithEmptyRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

        // Act
        var result = _service.GetCurrentCaller();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(0, result.Roles.Count);
    }

    [TestMethod]
    public void GetCurrentCaller_WhenNameIdentifierMissing_FallsBackToSubClaim()
    {
        // Arrange — no NameIdentifier, only "sub"
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("sub", userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

        // Act
        var result = _service.GetCurrentCaller();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.UserId);
    }
}

using System.Security.Claims;
using DotNetCloud.Core.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;
using Moq;

namespace DotNetCloud.Core.Auth.Tests.Authorization;

/// <summary>
/// Tests for <see cref="PermissionAuthorizationHandler"/>.
/// </summary>
[TestClass]
public class PermissionAuthorizationHandlerTests
{
    private PermissionAuthorizationHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _handler = new PermissionAuthorizationHandler();
    }

    [TestMethod]
    public async Task HandleRequirement_HasMatchingPermissionClaim_Succeeds()
    {
        // Arrange
        var requirement = new PermissionRequirement("files.read");
        var claims = new[] { new Claim("dnc:perm", "files.read") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var resource = new object();

        var context = new AuthorizationHandlerContext(
            new[] { requirement }, user, resource);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.IsTrue(context.HasSucceeded);
    }

    [TestMethod]
    public async Task HandleRequirement_MissingPermissionClaim_Fails()
    {
        // Arrange
        var requirement = new PermissionRequirement("admin");
        var claims = new[] { new Claim("dnc:perm", "files.read") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var context = new AuthorizationHandlerContext(
            new[] { requirement }, user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.IsFalse(context.HasSucceeded);
    }

    [TestMethod]
    public async Task HandleRequirement_NoPermissionClaims_Fails()
    {
        // Arrange
        var requirement = new PermissionRequirement("files.write");
        var identity = new ClaimsIdentity([], "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var context = new AuthorizationHandlerContext(
            new[] { requirement }, user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.IsFalse(context.HasSucceeded);
    }

    [TestMethod]
    public async Task HandleRequirement_UserHasMultiplePermissions_SucceedsForAny()
    {
        // Arrange
        var requirement = new PermissionRequirement("files.write");
        var claims = new[]
        {
            new Claim("dnc:perm", "files.read"),
            new Claim("dnc:perm", "files.write"),
            new Claim("dnc:perm", "files.delete"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var context = new AuthorizationHandlerContext(
            new[] { requirement }, user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.IsTrue(context.HasSucceeded);
    }

    [TestMethod]
    public async Task HandleRequirement_UnauthenticatedUser_Fails()
    {
        // Arrange
        var requirement = new PermissionRequirement("files.read");
        var identity = new ClaimsIdentity(); // No auth type = not authenticated
        var user = new ClaimsPrincipal(identity);

        var context = new AuthorizationHandlerContext(
            new[] { requirement }, user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.IsFalse(context.HasSucceeded);
    }
}

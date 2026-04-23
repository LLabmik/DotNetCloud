using System.Security.Claims;
using System.Text.Json;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Controllers;

/// <summary>
/// Tests for <see cref="GroupsController"/>.
/// </summary>
[TestClass]
public sealed class GroupsControllerTests
{
    private CoreDbContext _dbContext = null!;
    private Mock<IGroupDirectory> _groupDirectoryMock = null!;
    private Mock<IGroupManager> _groupManagerMock = null!;
    private Mock<ILogger<GroupsController>> _loggerMock = null!;
    private GroupsController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CoreDbContext(options, new PostgreSqlNamingStrategy());
        _groupDirectoryMock = new Mock<IGroupDirectory>();
        _groupManagerMock = new Mock<IGroupManager>();
        _loggerMock = new Mock<ILogger<GroupsController>>();

        _controller = new GroupsController(
            _groupDirectoryMock.Object,
            _groupManagerMock.Object,
            _dbContext,
            _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task ListAsync_WhenGroupsExist_ReturnsOkWithMappedData()
    {
        var organizationId = Guid.NewGuid();
        _groupDirectoryMock
            .Setup(service => service.GetGroupsForOrganizationAsync(organizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateGroupInfo(organizationId, name: GroupName("Editors"), memberCount: 3),
                CreateGroupInfo(organizationId, name: GroupName("Reviewers"), memberCount: 1),
            ]);

        var result = await _controller.ListAsync(organizationId, CancellationToken.None);

        var ok = AssertResult<OkObjectResult>(result);
        var payload = ToJson(ok.Value);
        Assert.IsTrue(payload.GetProperty("success").GetBoolean());
        Assert.AreEqual(2, payload.GetProperty("data").GetArrayLength());
        Assert.AreEqual("Editors", payload.GetProperty("data")[0].GetProperty("Name").GetString());
    }

    [TestMethod]
    public async Task GetAsync_WhenGroupMissing_ReturnsNotFound()
    {
        _groupDirectoryMock
            .Setup(service => service.GetGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupInfo?)null);

        var result = await _controller.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateAsync_WhenNameMissing_ReturnsBadRequest()
    {
        var result = await _controller.CreateAsync(new CreateGroupDto { Name = "  " }, CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        _groupManagerMock.Verify(
            service => service.CreateGroupAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task CreateAsync_WhenSuccessful_ReturnsCreated()
    {
        var organizationId = Guid.NewGuid();
        var group = CreateGroupInfo(organizationId, name: GroupName("Editors"), memberCount: 0);

        _groupManagerMock
            .Setup(service => service.CreateGroupAsync(organizationId, "Editors", "Editorial", It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _controller.CreateAsync(new CreateGroupDto
        {
            OrganizationId = organizationId,
            Name = "Editors",
            Description = "Editorial",
        }, CancellationToken.None);

        var created = AssertResult<CreatedResult>(result);
        Assert.AreEqual($"api/v1/core/admin/groups/{group.Id}", created.Location);

        var payload = ToJson(created.Value);
        Assert.AreEqual("Editors", payload.GetProperty("data").GetProperty("Name").GetString());
    }

    [TestMethod]
    public async Task ListMembersAsync_WhenGroupExists_ReturnsUserDetails()
    {
        var groupId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var alice = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "alice",
            Email = "alice@example.com",
            DisplayName = "Alice",
            IsActive = true,
        };
        var bob = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "bob",
            Email = "bob@example.com",
            DisplayName = "Bob",
            IsActive = true,
        };

        _dbContext.Users.AddRange(alice, bob);
        await _dbContext.SaveChangesAsync();

        _groupDirectoryMock
            .Setup(service => service.GetGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGroupInfo(organizationId, groupId, GroupName("Editors"), memberCount: 2));
        _groupDirectoryMock
            .Setup(service => service.GetGroupMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new GroupMemberInfo { GroupId = groupId, UserId = alice.Id, AddedAt = DateTime.UtcNow.AddMinutes(-2), AddedByUserId = bob.Id },
                new GroupMemberInfo { GroupId = groupId, UserId = bob.Id, AddedAt = DateTime.UtcNow.AddMinutes(-1) },
            ]);

        var result = await _controller.ListMembersAsync(groupId, CancellationToken.None);

        var ok = AssertResult<OkObjectResult>(result);
        var payload = ToJson(ok.Value);
        Assert.AreEqual(2, payload.GetProperty("data").GetArrayLength());
        Assert.AreEqual("alice@example.com", payload.GetProperty("data")[0].GetProperty("UserEmail").GetString());
        Assert.AreEqual("Bob", payload.GetProperty("data")[1].GetProperty("UserDisplayName").GetString());
    }

    [TestMethod]
    public async Task AddMemberAsync_WhenAlreadyMember_ReturnsConflict()
    {
        var groupId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "alice",
            Email = "alice@example.com",
            DisplayName = "Alice",
            IsActive = true,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _groupDirectoryMock
            .Setup(service => service.GetGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGroupInfo(organizationId, groupId, GroupName("Editors"), memberCount: 1));
        _groupManagerMock
            .Setup(service => service.AddMemberAsync(groupId, user.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.AddMemberAsync(groupId, new AddGroupMemberDto { UserId = user.Id }, CancellationToken.None);

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    [TestMethod]
    public async Task AddMemberAsync_WhenSuccessful_UsesCurrentUserIdAsAdder()
    {
        var groupId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "alice",
            Email = "alice@example.com",
            DisplayName = "Alice",
            IsActive = true,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        SetCurrentUser(currentUserId);

        _groupDirectoryMock
            .Setup(service => service.GetGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGroupInfo(organizationId, groupId, GroupName("Editors"), memberCount: 1));
        _groupManagerMock
            .Setup(service => service.AddMemberAsync(groupId, user.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groupDirectoryMock
            .Setup(service => service.GetGroupMemberAsync(groupId, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupMemberInfo
            {
                GroupId = groupId,
                UserId = user.Id,
                AddedAt = DateTime.UtcNow,
                AddedByUserId = currentUserId,
            });

        var result = await _controller.AddMemberAsync(groupId, new AddGroupMemberDto { UserId = user.Id }, CancellationToken.None);

        var ok = AssertResult<OkObjectResult>(result);
        var payload = ToJson(ok.Value);
        Assert.AreEqual(user.Email, payload.GetProperty("data").GetProperty("UserEmail").GetString());

        _groupManagerMock.Verify(
            service => service.AddMemberAsync(groupId, user.Id, currentUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RemoveMemberAsync_WhenUserNotMember_ReturnsNotFound()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        _groupDirectoryMock
            .Setup(service => service.GetGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGroupInfo(organizationId, groupId, GroupName("Editors"), memberCount: 1));
        _groupDirectoryMock
            .Setup(service => service.GetGroupMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupMemberInfo?)null);

        var result = await _controller.RemoveMemberAsync(groupId, userId, CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    private static T AssertResult<T>(IActionResult result)
        where T : IActionResult
    {
        Assert.IsInstanceOfType<T>(result);
        return (T)result;
    }

    private static GroupInfo CreateGroupInfo(Guid organizationId, Guid? groupId = null, string? name = null, int memberCount = 0)
    {
        return new GroupInfo
        {
            Id = groupId ?? Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name ?? GroupName("Editors"),
            Description = "Example group",
            IsAllUsersGroup = false,
            MemberCount = memberCount,
            CreatedAt = DateTime.UtcNow,
        };
    }

    private static string GroupName(string value) => value;

    private static JsonElement ToJson(object? value)
    {
        return JsonSerializer.SerializeToElement(value);
    }

    private void SetCurrentUser(Guid userId)
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "Admin"),
        ],
        "TestAuth"));
    }
}
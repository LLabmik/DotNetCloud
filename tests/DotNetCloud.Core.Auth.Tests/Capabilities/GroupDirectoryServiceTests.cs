using DotNetCloud.Core.Auth.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Tests.Capabilities;

/// <summary>
/// Tests for <see cref="GroupDirectoryService"/>.
/// </summary>
[TestClass]
public class GroupDirectoryServiceTests
{
    private CoreDbContext _dbContext = null!;
    private GroupDirectoryService _service = null!;
    private Organization _organization = null!;
    private ApplicationUser _alice = null!;
    private ApplicationUser _bob = null!;
    private Group _allUsersGroup = null!;
    private Group _editorsGroup = null!;

    [TestInitialize]
    public async Task SetupAsync()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CoreDbContext(options, new PostgreSqlNamingStrategy());
        _service = new GroupDirectoryService(_dbContext);

        _organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            CreatedAt = DateTime.UtcNow
        };

        _alice = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "alice",
            Email = "alice@example.com",
            DisplayName = "Alice",
            IsActive = true
        };

        _bob = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "bob",
            Email = "bob@example.com",
            DisplayName = "Bob",
            IsActive = true
        };

        _editorsGroup = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = "Editors",
            Description = "Editorial permissions",
            CreatedAt = DateTime.UtcNow
        };

        _allUsersGroup = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = Group.AllUsersGroupName,
            Description = "Built-in group containing all active organization members.",
            IsAllUsersGroup = true,
            CreatedAt = DateTime.UtcNow.AddSeconds(-1)
        };

        var reviewersGroup = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = "Reviewers",
            CreatedAt = DateTime.UtcNow.AddMinutes(1)
        };

        _dbContext.Organizations.Add(_organization);
        _dbContext.Users.AddRange(_alice, _bob);
        _dbContext.OrganizationMembers.AddRange(
            new OrganizationMember
            {
                OrganizationId = _organization.Id,
                UserId = _alice.Id,
                JoinedAt = DateTime.UtcNow.AddMinutes(-5),
                InvitedByUserId = _bob.Id,
                IsActive = true
            },
            new OrganizationMember
            {
                OrganizationId = _organization.Id,
                UserId = _bob.Id,
                JoinedAt = DateTime.UtcNow.AddMinutes(-4),
                IsActive = true
            });
        _dbContext.Groups.AddRange(_allUsersGroup, _editorsGroup, reviewersGroup);
        _dbContext.GroupMembers.AddRange(
            new GroupMember
            {
                GroupId = _editorsGroup.Id,
                UserId = _alice.Id,
                AddedAt = DateTime.UtcNow,
                AddedByUserId = _bob.Id
            },
            new GroupMember
            {
                GroupId = _editorsGroup.Id,
                UserId = _bob.Id,
                AddedAt = DateTime.UtcNow.AddMinutes(1)
            },
            new GroupMember
            {
                GroupId = reviewersGroup.Id,
                UserId = _alice.Id,
                AddedAt = DateTime.UtcNow.AddMinutes(2)
            });

        await _dbContext.SaveChangesAsync();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task GetGroupAsync_WhenGroupExists_ReturnsGroupInfo()
    {
        var result = await _service.GetGroupAsync(_editorsGroup.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(_editorsGroup.Id, result.Id);
        Assert.AreEqual("Editors", result.Name);
        Assert.AreEqual(2, result.MemberCount);
    }

    [TestMethod]
    public async Task GetGroupAsync_WhenAllUsersGroupExists_UsesOrganizationMemberCount()
    {
        var result = await _service.GetGroupAsync(_allUsersGroup.Id);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsAllUsersGroup);
        Assert.AreEqual(2, result.MemberCount);
    }

    [TestMethod]
    public async Task GetGroupAsync_WhenGroupMissing_ReturnsNull()
    {
        var result = await _service.GetGroupAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetGroupsForOrganizationAsync_WhenOrganizationHasGroups_ReturnsSortedGroups()
    {
        var result = await _service.GetGroupsForOrganizationAsync(_organization.Id);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(Group.AllUsersGroupName, result[0].Name);
        Assert.AreEqual("Editors", result[1].Name);
        Assert.AreEqual("Reviewers", result[2].Name);
    }

    [TestMethod]
    public async Task GetGroupsForUserAsync_WhenUserHasMemberships_ReturnsUserGroups()
    {
        var result = await _service.GetGroupsForUserAsync(_alice.Id);

        Assert.AreEqual(3, result.Count);
        CollectionAssert.AreEqual(new[] { Group.AllUsersGroupName, "Editors", "Reviewers" }, result.Select(group => group.Name).ToList());
    }

    [TestMethod]
    public async Task IsGroupMemberAsync_WhenMembershipExists_ReturnsTrue()
    {
        var result = await _service.IsGroupMemberAsync(_editorsGroup.Id, _alice.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsGroupMemberAsync_WhenAllUsersGroupAndOrganizationMembershipExists_ReturnsTrue()
    {
        var result = await _service.IsGroupMemberAsync(_allUsersGroup.Id, _alice.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task GetGroupMemberAsync_WhenMembershipExists_ReturnsMembershipInfo()
    {
        var result = await _service.GetGroupMemberAsync(_editorsGroup.Id, _alice.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(_editorsGroup.Id, result.GroupId);
        Assert.AreEqual(_alice.Id, result.UserId);
        Assert.AreEqual(_bob.Id, result.AddedByUserId);
    }

    [TestMethod]
    public async Task GetGroupMembersAsync_WhenGroupHasMembers_ReturnsMembersInAddedOrder()
    {
        var result = await _service.GetGroupMembersAsync(_editorsGroup.Id);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(_alice.Id, result[0].UserId);
        Assert.AreEqual(_bob.Id, result[1].UserId);
    }

    [TestMethod]
    public async Task GetGroupMembersAsync_WhenAllUsersGroup_ReturnsOrganizationMembers()
    {
        var result = await _service.GetGroupMembersAsync(_allUsersGroup.Id);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(_alice.Id, result[0].UserId);
        Assert.AreEqual(_bob.Id, result[1].UserId);
        Assert.AreEqual(_bob.Id, result[0].AddedByUserId);
    }
}
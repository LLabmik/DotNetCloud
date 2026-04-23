using DotNetCloud.Core.Auth.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Tests.Capabilities;

/// <summary>
/// Tests for <see cref="GroupManagerService"/>.
/// </summary>
[TestClass]
public class GroupManagerServiceTests
{
    private CoreDbContext _dbContext = null!;
    private GroupManagerService _service = null!;
    private Organization _organization = null!;
    private ApplicationUser _alice = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CoreDbContext(options, new PostgreSqlNamingStrategy());
        _service = new GroupManagerService(_dbContext);

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

        _dbContext.Organizations.Add(_organization);
        _dbContext.Users.Add(_alice);
        _dbContext.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task CreateGroupAsync_WhenOrganizationSpecified_CreatesGroup()
    {
        var result = await _service.CreateGroupAsync(_organization.Id, " Editors ", " Editorial team ");

        Assert.AreEqual(_organization.Id, result.OrganizationId);
        Assert.AreEqual("Editors", result.Name);
        Assert.AreEqual("Editorial team", result.Description);
        Assert.AreEqual(0, result.MemberCount);
        Assert.AreEqual(1, await _dbContext.Groups.CountAsync());
    }

    [TestMethod]
    public async Task CreateGroupAsync_WhenOrganizationEmpty_UsesDefaultOrganization()
    {
        var result = await _service.CreateGroupAsync(Guid.Empty, "Editors", null);

        Assert.AreEqual(_organization.Id, result.OrganizationId);
    }

    [TestMethod]
    public async Task CreateGroupAsync_WhenDuplicateNameExists_ThrowsInvalidOperationException()
    {
        _dbContext.Groups.Add(new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = "Editors",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.CreateGroupAsync(_organization.Id, "Editors", null));
    }

    [TestMethod]
    public async Task CreateGroupAsync_WhenReservedAllUsersNameRequested_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.CreateGroupAsync(_organization.Id, Group.AllUsersGroupName, null));
    }

    [TestMethod]
    public async Task UpdateGroupAsync_WhenGroupExists_UpdatesFields()
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = "Editors",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync();

        var result = await _service.UpdateGroupAsync(group.Id, "Reviewers", "Review team");

        Assert.IsNotNull(result);
        Assert.AreEqual("Reviewers", result.Name);
        Assert.AreEqual("Review team", result.Description);
    }

    [TestMethod]
    public async Task DeleteGroupAsync_WhenGroupExists_SoftDeletesGroup()
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = "Editors",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync();

        var result = await _service.DeleteGroupAsync(group.Id);

        Assert.IsTrue(result);
        var deleted = await _dbContext.Groups.IgnoreQueryFilters().SingleAsync(g => g.Id == group.Id);
        Assert.IsTrue(deleted.IsDeleted);
        Assert.IsNotNull(deleted.DeletedAt);
    }

    [TestMethod]
    public async Task UpdateGroupAsync_WhenAllUsersGroup_ThrowsInvalidOperationException()
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = Group.AllUsersGroupName,
            IsAllUsersGroup = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.UpdateGroupAsync(group.Id, "Renamed", null));
    }

    [TestMethod]
    public async Task DeleteGroupAsync_WhenAllUsersGroup_ThrowsInvalidOperationException()
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = Group.AllUsersGroupName,
            IsAllUsersGroup = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.DeleteGroupAsync(group.Id));
    }

    [TestMethod]
    public async Task AddMemberAsync_WhenMembershipMissing_AddsMember()
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = "Editors",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync();

        var result = await _service.AddMemberAsync(group.Id, _alice.Id, addedByUserId: _alice.Id);

        Assert.IsTrue(result);
        var member = await _dbContext.GroupMembers.SingleAsync(m => m.GroupId == group.Id && m.UserId == _alice.Id);
        Assert.AreEqual(_alice.Id, member.AddedByUserId);
    }

    [TestMethod]
    public async Task AddMemberAsync_WhenAllUsersGroup_ThrowsInvalidOperationException()
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = Group.AllUsersGroupName,
            IsAllUsersGroup = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.AddMemberAsync(group.Id, _alice.Id));
    }

    [TestMethod]
    public async Task AddMemberAsync_WhenMembershipExists_ReturnsFalse()
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = "Editors",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Groups.Add(group);
        _dbContext.GroupMembers.Add(new GroupMember
        {
            GroupId = group.Id,
            UserId = _alice.Id,
            AddedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.AddMemberAsync(group.Id, _alice.Id);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task RemoveMemberAsync_WhenMembershipExists_RemovesMember()
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = "Editors",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Groups.Add(group);
        _dbContext.GroupMembers.Add(new GroupMember
        {
            GroupId = group.Id,
            UserId = _alice.Id,
            AddedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.RemoveMemberAsync(group.Id, _alice.Id);

        Assert.IsTrue(result);
        Assert.AreEqual(0, await _dbContext.GroupMembers.CountAsync());
    }

    [TestMethod]
    public async Task RemoveMemberAsync_WhenAllUsersGroup_ThrowsInvalidOperationException()
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organization.Id,
            Name = Group.AllUsersGroupName,
            IsAllUsersGroup = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.RemoveMemberAsync(group.Id, _alice.Id));
    }
}
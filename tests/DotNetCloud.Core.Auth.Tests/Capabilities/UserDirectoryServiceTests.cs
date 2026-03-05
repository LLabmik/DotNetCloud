using DotNetCloud.Core.Auth.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Naming;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DotNetCloud.Core.Auth.Tests.Capabilities;

/// <summary>
/// Tests for <see cref="UserDirectoryService"/>.
/// </summary>
[TestClass]
public class UserDirectoryServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
    private CoreDbContext _dbContext = null!;
    private UserDirectoryService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        _service = new UserDirectoryService(_userManagerMock.Object, _dbContext);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    // ---------------------------------------------------------------------------
    // FindUserIdByUsernameAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task FindUserIdByUsernameAsync_WhenUserExists_ReturnsUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "alice", DisplayName = "Alice" };
        _userManagerMock.Setup(x => x.FindByNameAsync("alice"))
            .ReturnsAsync(user);

        // Act
        var result = await _service.FindUserIdByUsernameAsync("alice");

        // Assert
        Assert.AreEqual(userId, result);
    }

    [TestMethod]
    public async Task FindUserIdByUsernameAsync_WhenUserNotFound_ReturnsNull()
    {
        // Arrange
        _userManagerMock.Setup(x => x.FindByNameAsync("nonexistent"))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.FindUserIdByUsernameAsync("nonexistent");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task FindUserIdByUsernameAsync_WhenUsernameIsEmpty_ReturnsNull()
    {
        // Act
        var result = await _service.FindUserIdByUsernameAsync("");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task FindUserIdByUsernameAsync_WhenUsernameIsWhitespace_ReturnsNull()
    {
        // Act
        var result = await _service.FindUserIdByUsernameAsync("   ");

        // Assert
        Assert.IsNull(result);
    }

    // ---------------------------------------------------------------------------
    // GetDisplayNamesAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task GetDisplayNamesAsync_WhenUsersExist_ReturnsDictionary()
    {
        // Arrange
        var user1 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "alice", DisplayName = "Alice Smith" };
        var user2 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "bob", DisplayName = "Bob Jones" };
        _dbContext.Users.AddRange(user1, user2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDisplayNamesAsync([user1.Id, user2.Id]);

        // Assert
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Alice Smith", result[user1.Id]);
        Assert.AreEqual("Bob Jones", result[user2.Id]);
    }

    [TestMethod]
    public async Task GetDisplayNamesAsync_WhenEmptyList_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _service.GetDisplayNamesAsync([]);

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetDisplayNamesAsync_WhenSomeIdsNotFound_ReturnsOnlyExistingUsers()
    {
        // Arrange
        var user1 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "alice", DisplayName = "Alice" };
        _dbContext.Users.Add(user1);
        await _dbContext.SaveChangesAsync();

        var unknownId = Guid.NewGuid();

        // Act
        var result = await _service.GetDisplayNamesAsync([user1.Id, unknownId]);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.ContainsKey(user1.Id));
        Assert.IsFalse(result.ContainsKey(unknownId));
    }

    [TestMethod]
    public async Task GetDisplayNamesAsync_WhenDuplicateIds_DeduplicatesInput()
    {
        // Arrange
        var user1 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "alice", DisplayName = "Alice" };
        _dbContext.Users.Add(user1);
        await _dbContext.SaveChangesAsync();

        // Act — same ID twice
        var result = await _service.GetDisplayNamesAsync([user1.Id, user1.Id]);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Alice", result[user1.Id]);
    }

    [TestMethod]
    public async Task GetDisplayNamesAsync_WhenNullArgument_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _service.GetDisplayNamesAsync(null!));
    }
}

using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Server.Initialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Initialization;

[TestClass]
public class AdminSeederTests
{
    private Mock<IUserStore<ApplicationUser>> _storeMock = null!;
    private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
    private Mock<IRoleStore<ApplicationRole>> _roleStoreMock = null!;
    private Mock<RoleManager<ApplicationRole>> _roleManagerMock = null!;
    private ILogger<AdminSeeder> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            _storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _roleStoreMock = new Mock<IRoleStore<ApplicationRole>>();
        _roleManagerMock = new Mock<RoleManager<ApplicationRole>>(
            _roleStoreMock.Object, null!, null!, null!, null!);
        _logger = NullLogger<AdminSeeder>.Instance;
    }

    [TestMethod]
    public async Task WhenUsersExist_ThenSkipsSeedingAsync()
    {
        // Arrange — simulate existing users
        var users = new List<ApplicationUser>
        {
            new() { UserName = "existing@test.com", Email = "existing@test.com", DisplayName = "Existing" }
        }.AsQueryable();

        _userManagerMock.Setup(m => m.Users).Returns(users);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DotNetCloud:AdminEmail"] = "admin@test.com",
                ["DotNetCloud:AdminPassword"] = "Str0ng!Pass99"
            })
            .Build();

        var seeder = new AdminSeeder(_userManagerMock.Object, _roleManagerMock.Object, null!, config, _logger);

        // Act
        await seeder.SeedAsync();

        // Assert — CreateAsync should never be called
        _userManagerMock.Verify(
            m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenNoCredentials_ThenSkipsSeedingAsync()
    {
        // Arrange — empty database, no credentials
        _userManagerMock.Setup(m => m.Users)
            .Returns(Enumerable.Empty<ApplicationUser>().AsQueryable());

        var config = new ConfigurationBuilder().Build();

        var seeder = new AdminSeeder(_userManagerMock.Object, _roleManagerMock.Object, null!, config, _logger);

        // Act
        await seeder.SeedAsync();

        // Assert — CreateAsync should never be called
        _userManagerMock.Verify(
            m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenEmailButNoPassword_ThenSkipsSeedingAsync()
    {
        // Arrange
        _userManagerMock.Setup(m => m.Users)
            .Returns(Enumerable.Empty<ApplicationUser>().AsQueryable());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DotNetCloud:AdminEmail"] = "admin@test.com"
            })
            .Build();

        var seeder = new AdminSeeder(_userManagerMock.Object, _roleManagerMock.Object, null!, config, _logger);

        // Act
        await seeder.SeedAsync();

        // Assert
        _userManagerMock.Verify(
            m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenCredentialsAndNoUsers_ThenCreatesAdminAsync()
    {
        // Arrange
        _userManagerMock.Setup(m => m.Users)
            .Returns(Enumerable.Empty<ApplicationUser>().AsQueryable());
        _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Administrator"))
            .ReturnsAsync(IdentityResult.Success);
        _roleManagerMock.Setup(m => m.RoleExistsAsync("Administrator"))
            .ReturnsAsync(true);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DotNetCloud:AdminEmail"] = "admin@test.com",
                ["DotNetCloud:AdminPassword"] = "Str0ng!Pass99"
            })
            .Build();

        var seeder = new AdminSeeder(_userManagerMock.Object, _roleManagerMock.Object, null!, config, _logger);

        // Act
        await seeder.SeedAsync();

        // Assert
        _userManagerMock.Verify(
            m => m.CreateAsync(
                It.Is<ApplicationUser>(u =>
                    u.Email == "admin@test.com" &&
                    u.UserName == "admin@test.com" &&
                    u.DisplayName == "Administrator" &&
                    u.EmailConfirmed &&
                    u.IsActive),
                "Str0ng!Pass99"),
            Times.Once);

        _userManagerMock.Verify(
            m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Administrator"),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenCreateFails_ThenThrowsAsync()
    {
        // Arrange
        _userManagerMock.Setup(m => m.Users)
            .Returns(Enumerable.Empty<ApplicationUser>().AsQueryable());
        _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateEmail",
                Description = "Email already taken."
            }));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DotNetCloud:AdminEmail"] = "admin@test.com",
                ["DotNetCloud:AdminPassword"] = "Str0ng!Pass99"
            })
            .Build();

        var seeder = new AdminSeeder(_userManagerMock.Object, _roleManagerMock.Object, null!, config, _logger);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => seeder.SeedAsync());
    }

    [TestMethod]
    public async Task WhenRoleAssignmentFails_ThenThrowsAsync()
    {
        // Arrange
        _userManagerMock.Setup(m => m.Users)
            .Returns(Enumerable.Empty<ApplicationUser>().AsQueryable());
        _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Administrator"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "RoleNotFound",
                Description = "Role does not exist."
            }));
        _roleManagerMock.Setup(m => m.RoleExistsAsync("Administrator"))
            .ReturnsAsync(true);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DotNetCloud:AdminEmail"] = "admin@test.com",
                ["DotNetCloud:AdminPassword"] = "Str0ng!Pass99"
            })
            .Build();

        var seeder = new AdminSeeder(_userManagerMock.Object, _roleManagerMock.Object, null!, config, _logger);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => seeder.SeedAsync());
    }

    [TestMethod]
    public async Task WhenSeedFileExists_ThenReadsPasswordFromFileAsync()
    {
        // Arrange — write a temp seed file
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnetcloud-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var seedFile = Path.Combine(tempDir, ".admin-seed");
        File.WriteAllText(seedFile, "FileP@ss99!x");

        _userManagerMock.Setup(m => m.Users)
            .Returns(Enumerable.Empty<ApplicationUser>().AsQueryable());
        _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Administrator"))
            .ReturnsAsync(IdentityResult.Success);
        _roleManagerMock.Setup(m => m.RoleExistsAsync("Administrator"))
            .ReturnsAsync(true);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DotNetCloud:AdminEmail"] = "admin@test.com",
                ["DOTNETCLOUD_CONFIG_DIR"] = tempDir
            })
            .Build();

        var seeder = new AdminSeeder(_userManagerMock.Object, _roleManagerMock.Object, null!, config, _logger);

        // Act
        await seeder.SeedAsync();

        // Assert — password came from seed file
        _userManagerMock.Verify(
            m => m.CreateAsync(It.IsAny<ApplicationUser>(), "FileP@ss99!x"),
            Times.Once);

        // Seed file should be deleted
        Assert.IsFalse(File.Exists(seedFile), "Seed file should be deleted after reading");

        // Cleanup
        Directory.Delete(tempDir, recursive: true);
    }
}

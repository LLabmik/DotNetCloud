using DotNetCloud.Core.Auth.Services;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Auth.Tests.Services;

/// <summary>
/// Tests for <see cref="UserSettingsService"/>.
/// </summary>
/// <remarks>
/// The service is scoped in DI but the Blazor circuit that resolves it is long-lived, and several
/// components' initializers interleave on it (<c>MainLayout</c> restores the collapsed sidebar while
/// <c>Home</c> loads its widget layout). A single captured <c>CoreDbContext</c> therefore receives
/// overlapping queries and EF throws "A second operation was started on this context instance".
/// These tests pin the contract that fixes it: one short-lived context per operation.
/// </remarks>
[TestClass]
public class UserSettingsServiceTests
{
    private CoreDbContext _dbContext = null!;
    private TestDbContextFactory _factory = null!;
    private Mock<ILogger<UserSettingsService>> _loggerMock = null!;
    private UserSettingsService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var databaseName = $"UserSettingsTests_{Guid.CreateVersion7()}";
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        _dbContext = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        _factory = new TestDbContextFactory(databaseName, new PostgreSqlNamingStrategy());
        _loggerMock = new Mock<ILogger<UserSettingsService>>();

        _service = new UserSettingsService(_factory, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task WhenSettingExistsForUserThenGetSettingReturnsDto()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        _dbContext.UserSettings.Add(
            new UserSetting
            {
                UserId = userId,
                Module = "dotnetcloud.ui",
                Key = "navbar.collapsed",
                Value = "true",
                Description = "Navbar state",
                IsEncrypted = false,
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual("dotnetcloud.ui", result.Module);
        Assert.AreEqual("navbar.collapsed", result.Key);
        Assert.AreEqual("true", result.Value);
    }

    [TestMethod]
    public async Task WhenSettingDoesNotExistForUserThenGetSettingReturnsNull()
    {
        // Act
        var result = await _service.GetSettingAsync(Guid.CreateVersion7(), "dotnetcloud.ui", "navbar.collapsed");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task WhenSettingDoesNotExistThenUpsertCreatesNewSetting()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var dto = new UpsertUserSettingDto
        {
            Value = "true",
            Description = "Navbar state",
            IsSensitive = false,
        };

        // Act
        var result = await _service.UpsertSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed", dto);

        // Assert
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual("true", result.Value);

        var dbSetting = await _dbContext.UserSettings.FirstOrDefaultAsync(
            s => s.UserId == userId && s.Module == "dotnetcloud.ui" && s.Key == "navbar.collapsed");
        Assert.IsNotNull(dbSetting);
        Assert.AreEqual("true", dbSetting.Value);
    }

    [TestMethod]
    public async Task WhenSettingExistsThenUpsertUpdatesExistingSetting()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        _dbContext.UserSettings.Add(
            new UserSetting
            {
                UserId = userId,
                Module = "dotnetcloud.ui",
                Key = "navbar.collapsed",
                Value = "false",
                Description = "Old",
                IsEncrypted = false,
            });
        await _dbContext.SaveChangesAsync();

        var dto = new UpsertUserSettingDto
        {
            Value = "true",
            Description = "Updated",
            IsSensitive = false,
        };

        // Act
        var result = await _service.UpsertSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed", dto);

        // Assert
        Assert.AreEqual("true", result.Value);
        Assert.AreEqual("Updated", result.Description);

        var count = await _dbContext.UserSettings.CountAsync(
            s => s.UserId == userId && s.Module == "dotnetcloud.ui" && s.Key == "navbar.collapsed");
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task GetSettingAsync_TwoCallsOnOneInstance_EachGetTheirOwnContext()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        _dbContext.UserSettings.Add(
            new UserSetting
            {
                UserId = userId,
                Module = "dotnetcloud.ui",
                Key = "navbar.collapsed",
                Value = "true",
                Description = "Navbar state",
                IsEncrypted = false,
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var first = await _service.GetSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed");
        var second = await _service.GetSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed");

        // Assert
        Assert.AreEqual("true", first?.Value);
        Assert.AreEqual("true", second?.Value);
        Assert.AreEqual(
            2,
            _factory.CreatedContexts.Count,
            "Each operation must use its own context so overlapping callers can never share one.");
        Assert.AreEqual(
            2,
            _factory.CreatedContexts.Distinct().Count(),
            "The two operations must not reuse the same context instance.");
    }

    [TestMethod]
    public async Task GetSettingAsync_OverlappingCallsOnOneInstance_DoNotShareAContext()
    {
        // Arrange - the layout and the home page initializers interleave on the same scoped instance.
        var userId = Guid.CreateVersion7();
        _dbContext.UserSettings.Add(
            new UserSetting
            {
                UserId = userId,
                Module = "home-widgets",
                Key = "preferences",
                Value = "{}",
                Description = "Widget layout",
                IsEncrypted = false,
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var overlapping = Enumerable
            .Range(0, 8)
            .Select(_ => _service.GetSettingAsync(userId, "home-widgets", "preferences"))
            .ToList();
        var results = await Task.WhenAll(overlapping);

        // Assert
        Assert.IsTrue(results.All(result => result is not null));
        Assert.AreEqual(8, _factory.CreatedContexts.Count);
        Assert.AreEqual(
            8,
            _factory.CreatedContexts.Distinct().Count(),
            "Overlapping callers must each work on a distinct context - sharing one is what throws.");
    }

    [TestMethod]
    public async Task UpsertSettingAsync_ReleasesTheContextItCreated()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var dto = new UpsertUserSettingDto
        {
            Value = "true",
            Description = "Navbar state",
            IsSensitive = false,
        };

        // Act
        await _service.UpsertSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed", dto);

        // Assert
        var created = _factory.CreatedContexts.Single();
        Assert.ThrowsExactly<ObjectDisposedException>(() => created.UserSettings.ToList());
    }

    /// <summary>
    /// Hands out one in-memory <see cref="CoreDbContext"/> per call and records them, so a test can
    /// prove that nothing is shared between operations.
    /// </summary>
    private sealed class TestDbContextFactory : IDbContextFactory
    {
        private readonly string _databaseName;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestDbContextFactory"/> class.
        /// </summary>
        /// <param name="databaseName">The shared in-memory database name.</param>
        /// <param name="namingStrategy">The naming strategy for the created contexts.</param>
        public TestDbContextFactory(string databaseName, ITableNamingStrategy namingStrategy)
        {
            _databaseName = databaseName;
            NamingStrategy = namingStrategy;
        }

        /// <summary>
        /// Gets every context handed out so far, in creation order.
        /// </summary>
        public List<CoreDbContext> CreatedContexts { get; } = [];

        /// <inheritdoc />
        public DatabaseProvider Provider => DatabaseProvider.PostgreSQL;

        /// <inheritdoc />
        public ITableNamingStrategy NamingStrategy { get; }

        /// <inheritdoc />
        public CoreDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;
            var context = new CoreDbContext(options, NamingStrategy);
            CreatedContexts.Add(context);
            return context;
        }
    }
}

using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Auth;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="ExpiredTokenPurgeHostedService"/>.
/// </summary>
[TestClass]
public sealed class ExpiredTokenPurgeHostedServiceTests
{
    private static CoreDbContext CreateContext(string databaseName) =>
        new(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options,
            new SqlServerNamingStrategy());

    private static ExpiredTokenPurgeHostedService CreateService(CoreDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        var provider = services.BuildServiceProvider();

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider);
        scope.Setup(s => s.Dispose());

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new ExpiredTokenPurgeHostedService(
            scopeFactory.Object,
            Mock.Of<IBackgroundServiceTracker>(),
            NullLogger<ExpiredTokenPurgeHostedService>.Instance);
    }

    private static OpenIddictToken CreateToken(DateTime? expiresAt) => new()
    {
        Id = Guid.CreateVersion7(),
        Status = "valid",
        Type = "refresh_token",
        Subject = "test-subject",
        ReferenceId = Guid.CreateVersion7().ToString("N"),
        CreationDate = DateTime.UtcNow.AddDays(-90),
        ExpirationDate = expiresAt,
    };

    [TestMethod]
    public async Task PurgeExpiredTokensAsync_ExpiredToken_DeletesRow()
    {
        var dbName = Guid.CreateVersion7().ToString();
        using var db = CreateContext(dbName);

        var expired = CreateToken(DateTime.UtcNow.AddDays(-30));
        var live = CreateToken(DateTime.UtcNow.AddHours(1));
        db.OpenIddictTokens.AddRange(expired, live);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var purged = await service.PurgeExpiredTokensAsync(CancellationToken.None);

        Assert.AreEqual(1L, purged);
        Assert.IsNull(await db.OpenIddictTokens.FindAsync(expired.Id));
        Assert.IsNotNull(await db.OpenIddictTokens.FindAsync(live.Id));
    }

    [TestMethod]
    public async Task PurgeExpiredTokensAsync_TokenWithoutExpiry_KeepsRow()
    {
        var dbName = Guid.CreateVersion7().ToString();
        using var db = CreateContext(dbName);

        var noExpiry = CreateToken(null);
        db.OpenIddictTokens.Add(noExpiry);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var purged = await service.PurgeExpiredTokensAsync(CancellationToken.None);

        Assert.AreEqual(0L, purged);
        Assert.IsNotNull(await db.OpenIddictTokens.FindAsync(noExpiry.Id));
    }

    [TestMethod]
    public async Task PurgeExpiredTokensAsync_NothingExpired_ReturnsZero()
    {
        var dbName = Guid.CreateVersion7().ToString();
        using var db = CreateContext(dbName);

        db.OpenIddictTokens.AddRange(
            CreateToken(DateTime.UtcNow.AddHours(2)),
            CreateToken(DateTime.UtcNow.AddMinutes(5)));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var purged = await service.PurgeExpiredTokensAsync(CancellationToken.None);

        Assert.AreEqual(0L, purged);
        Assert.AreEqual(2, await db.OpenIddictTokens.CountAsync());
    }
}

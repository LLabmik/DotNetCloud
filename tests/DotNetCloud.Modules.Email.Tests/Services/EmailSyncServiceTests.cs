using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.Email.Data.Services;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Email.Tests.Services;

/// <summary>
/// Tests for <see cref="EmailSyncService"/> — the sync work the core now drives.
/// </summary>
/// <remarks>
/// The module used to sweep every enabled account on its own timer. These tests pin the two
/// properties that replaced it: a sync is only performed for the account's own owner, and the
/// module reports which accounts are due so the core can vouch for each user itself.
/// </remarks>
[TestClass]
public class EmailSyncServiceTests
{
    private static readonly DateTime OldSync = DateTime.UtcNow.AddHours(-2);

    private static (EmailSyncService Service, ServiceProvider Provider) CreateService(
        string dbName,
        IEmailProvider? provider = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<EmailDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton(provider ?? Mock.Of<IEmailProvider>());

        var serviceProvider = services.BuildServiceProvider();

        var service = new EmailSyncService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSyncService>.Instance);

        return (service, serviceProvider);
    }

    private static EmailAccount CreateAccount(
        Guid ownerId,
        bool enabled = true,
        bool deleted = false,
        DateTime? updatedAt = null) => new()
        {
            OwnerId = ownerId,
            ProviderType = EmailProviderType.ImapSmtp,
            DisplayName = "Work",
            EmailAddress = "user@example.com",
            IsEnabled = enabled,
            IsDeleted = deleted,
            UpdatedAt = updatedAt ?? OldSync
        };

    private static async Task SeedAsync(string dbName, params EmailAccount[] accounts)
    {
        var options = new DbContextOptionsBuilder<EmailDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var db = new EmailDbContext(options);
        db.EmailAccounts.AddRange(accounts);
        await db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task SyncAccountAsync_AccountBelongsToAnotherUser_ReturnsFalseAndDoesNotSync()
    {
        var dbName = Guid.CreateVersion7().ToString();
        var ownerId = Guid.CreateVersion7();
        var otherUserId = Guid.CreateVersion7();
        var account = CreateAccount(ownerId);
        await SeedAsync(dbName, account);

        var provider = new Mock<IEmailProvider>();
        provider.SetupGet(p => p.ProviderType).Returns(EmailProviderType.ImapSmtp);

        var (service, services) = CreateService(dbName, provider.Object);
        await using (services)
        {
            var synced = await service.SyncAccountAsync(account.Id, otherUserId);

            Assert.IsFalse(synced);
            provider.Verify(
                p => p.ListMailboxesAsync(It.IsAny<EmailAccount>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    [TestMethod]
    public async Task SyncAccountAsync_EmptyUserId_ReturnsFalse()
    {
        var dbName = Guid.CreateVersion7().ToString();
        var account = CreateAccount(Guid.CreateVersion7());
        await SeedAsync(dbName, account);

        var (service, services) = CreateService(dbName);
        await using (services)
        {
            Assert.IsFalse(await service.SyncAccountAsync(account.Id, Guid.Empty));
        }
    }

    [TestMethod]
    public async Task SyncAccountAsync_OwnersAccount_SyncsAndReturnsTrue()
    {
        var dbName = Guid.CreateVersion7().ToString();
        var ownerId = Guid.CreateVersion7();
        var account = CreateAccount(ownerId);
        await SeedAsync(dbName, account);

        var provider = new Mock<IEmailProvider>();
        provider.SetupGet(p => p.ProviderType).Returns(EmailProviderType.ImapSmtp);
        provider
            .Setup(p => p.ListMailboxesAsync(It.IsAny<EmailAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmailMailbox>());

        var (service, services) = CreateService(dbName, provider.Object);
        await using (services)
        {
            var synced = await service.SyncAccountAsync(account.Id, ownerId);

            Assert.IsTrue(synced);
            provider.Verify(
                p => p.ListMailboxesAsync(
                    It.Is<EmailAccount>(a => a.Id == account.Id), It.IsAny<CancellationToken>()),
                Times.Once);

            var status = await service.GetSyncStatusAsync(account.Id);
            Assert.IsNotNull(status);
            Assert.AreEqual("Completed", status.Status);
        }
    }

    [TestMethod]
    public async Task ListSyncCandidatesAsync_ReturnsOnlyEnabledNonDeletedDueAccounts()
    {
        var dbName = Guid.CreateVersion7().ToString();

        var due = CreateAccount(Guid.CreateVersion7());
        var recentlySynced = CreateAccount(Guid.CreateVersion7(), updatedAt: DateTime.UtcNow);
        var disabled = CreateAccount(Guid.CreateVersion7(), enabled: false);
        var deleted = CreateAccount(Guid.CreateVersion7(), deleted: true);

        await SeedAsync(dbName, due, recentlySynced, disabled, deleted);

        var (service, services) = CreateService(dbName);
        await using (services)
        {
            var candidates = await service.ListSyncCandidatesAsync(TimeSpan.FromMinutes(5));

            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(due.Id, candidates[0].AccountId);
            Assert.AreEqual(due.OwnerId, candidates[0].UserId);
        }
    }

    [TestMethod]
    public async Task ListSyncCandidatesAsync_ZeroMinAge_ReportsRecentlySyncedAccountsToo()
    {
        var dbName = Guid.CreateVersion7().ToString();
        var account = CreateAccount(Guid.CreateVersion7(), updatedAt: DateTime.UtcNow);
        await SeedAsync(dbName, account);

        var (service, services) = CreateService(dbName);
        await using (services)
        {
            var candidates = await service.ListSyncCandidatesAsync(TimeSpan.Zero);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(account.Id, candidates[0].AccountId);
        }
    }
}

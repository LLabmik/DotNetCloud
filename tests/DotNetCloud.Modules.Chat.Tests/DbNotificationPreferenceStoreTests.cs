using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for the database-backed <see cref="DbNotificationPreferenceStore"/>,
/// verifying that preferences persist (survive across store instances) and that
/// a new user defaults to Do-Not-Disturb disabled.
/// </summary>
[TestClass]
public class DbNotificationPreferenceStoreTests
{
    private sealed class TestContextFactory : IDbContextFactory<ChatDbContext>
    {
        private readonly DbContextOptions<ChatDbContext> _options;

        public TestContextFactory(DbContextOptions<ChatDbContext> options) => _options = options;

        public ChatDbContext CreateDbContext() => new(_options);

        public Task<ChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private static IDbContextFactory<ChatDbContext> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        return new TestContextFactory(options);
    }

    [TestMethod]
    public void Get_NewUser_ReturnsDndDisabledByDefault()
    {
        var store = new DbNotificationPreferenceStore(CreateFactory());

        var prefs = store.Get(Guid.CreateVersion7());

        Assert.IsNotNull(prefs);
        Assert.IsFalse(prefs.DoNotDisturb, "A new user must default to Do-Not-Disturb disabled.");
        Assert.IsTrue(prefs.PushEnabled, "Push notifications should default to enabled.");
        Assert.IsNotNull(prefs.MutedChannelIds);
        Assert.AreEqual(0, prefs.MutedChannelIds.Count);
    }

    [TestMethod]
    public void Update_ThenGet_ReturnsPersistedPreferences()
    {
        var store = new DbNotificationPreferenceStore(CreateFactory());
        var userId = Guid.CreateVersion7();
        var mutedChannel = Guid.CreateVersion7();

        store.Update(userId, new UserNotificationPreferences
        {
            PushEnabled = false,
            DoNotDisturb = true,
            MutedChannelIds = new HashSet<Guid> { mutedChannel, mutedChannel }
        });

        var prefs = store.Get(userId);

        Assert.IsFalse(prefs.PushEnabled);
        Assert.IsTrue(prefs.DoNotDisturb);
        Assert.AreEqual(1, prefs.MutedChannelIds.Count, "Duplicate muted channel IDs should be normalized.");
        Assert.IsTrue(prefs.MutedChannelIds.Contains(mutedChannel));
    }

    [TestMethod]
    public void Get_AfterUpdate_IsPersistedAcrossStoreInstances()
    {
        // Two independent store instances over the same database — the analog of
        // reading the same state from another process or machine.
        var factory = CreateFactory();
        var writer = new DbNotificationPreferenceStore(factory);
        var reader = new DbNotificationPreferenceStore(factory);
        var userId = Guid.CreateVersion7();

        writer.Update(userId, new UserNotificationPreferences { DoNotDisturb = true });

        var prefs = reader.Get(userId);
        Assert.IsTrue(prefs.DoNotDisturb);
    }

    [TestMethod]
    public void Update_ThenDisableDnd_ReflectsLatestState()
    {
        var store = new DbNotificationPreferenceStore(CreateFactory());
        var userId = Guid.CreateVersion7();

        store.Update(userId, new UserNotificationPreferences { DoNotDisturb = true });
        Assert.IsTrue(store.Get(userId).DoNotDisturb);

        store.Update(userId, new UserNotificationPreferences { DoNotDisturb = false });
        Assert.IsFalse(store.Get(userId).DoNotDisturb);
    }
}

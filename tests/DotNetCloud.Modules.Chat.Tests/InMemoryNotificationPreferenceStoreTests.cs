using DotNetCloud.Modules.Chat.Services;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="InMemoryNotificationPreferenceStore"/> defaults and
/// update semantics, particularly that Do-Not-Disturb is disabled by default
/// for new users.
/// </summary>
[TestClass]
public class InMemoryNotificationPreferenceStoreTests
{
    [TestMethod]
    public void Get_NewUser_ReturnsDndDisabledByDefault()
    {
        var store = new InMemoryNotificationPreferenceStore();
        var newUserId = Guid.CreateVersion7();

        var prefs = store.Get(newUserId);

        Assert.IsNotNull(prefs);
        Assert.IsFalse(prefs.DoNotDisturb, "A new user must default to Do-Not-Disturb disabled.");
        Assert.IsTrue(prefs.PushEnabled, "Push notifications should default to enabled.");
        Assert.IsNotNull(prefs.MutedChannelIds);
        Assert.AreEqual(0, prefs.MutedChannelIds.Count);
    }

    [TestMethod]
    public void Get_UnknownUser_DoesNotAffectAnotherUsersDefaults()
    {
        var store = new InMemoryNotificationPreferenceStore();

        var first = store.Get(Guid.CreateVersion7());
        var second = store.Get(Guid.CreateVersion7());

        Assert.IsFalse(first.DoNotDisturb);
        Assert.IsFalse(second.DoNotDisturb);
    }

    [TestMethod]
    public void Update_ThenGet_ReturnsUpdatedPreferences()
    {
        var store = new InMemoryNotificationPreferenceStore();
        var userId = Guid.CreateVersion7();
        var mutedChannel = Guid.CreateVersion7();

        store.Update(userId, new UserNotificationPreferences
        {
            PushEnabled = false,
            DoNotDisturb = true,
            MutedChannelIds = new HashSet<Guid> { mutedChannel }
        });

        var prefs = store.Get(userId);

        Assert.IsFalse(prefs.PushEnabled);
        Assert.IsTrue(prefs.DoNotDisturb);
        Assert.AreEqual(1, prefs.MutedChannelIds.Count);
        Assert.IsTrue(prefs.MutedChannelIds.Contains(mutedChannel));
    }
}

using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.RealTime;

[TestClass]
public class PresenceAwareNotificationPreferenceStoreTests
{
    [TestMethod]
    public void Update_EnablingDnd_TransitionsOnlineUserToDoNotDisturb_AndRaisesEvent()
    {
        var userId = Guid.CreateVersion7();
        var tracker = new UserConnectionTracker();
        tracker.AddConnection(userId, "conn-1");
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        _ = presence.UserConnectedAsync(userId, "conn-1").GetAwaiter().GetResult();

        var inner = new InMemoryFakeStore();
        var store = new PresenceAwareNotificationPreferenceStore(inner, presence);

        PresenceState? raised = null;
        presence.PresenceStateChanged += (_, state) => raised = state;

        store.Update(userId, new UserNotificationPreferences { DoNotDisturb = true });

        Assert.AreEqual(PresenceState.DoNotDisturb, presence.GetDisplayState(userId));
        Assert.AreEqual(PresenceState.DoNotDisturb, raised);
        Assert.IsTrue(inner.Get(userId).DoNotDisturb);
    }

    [TestMethod]
    public void Update_DisablingDnd_ReturnsOnlineUserToOnline()
    {
        var userId = Guid.CreateVersion7();
        var tracker = new UserConnectionTracker();
        tracker.AddConnection(userId, "conn-1");
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        _ = presence.UserConnectedAsync(userId, "conn-1").GetAwaiter().GetResult();

        var inner = new InMemoryFakeStore();
        var store = new PresenceAwareNotificationPreferenceStore(inner, presence);

        var raised = new List<PresenceState>();
        presence.PresenceStateChanged += (_, state) => raised.Add(state);

        // Turn DND on through the bridge → red.
        store.Update(userId, new UserNotificationPreferences { DoNotDisturb = true });
        Assert.AreEqual(PresenceState.DoNotDisturb, presence.GetDisplayState(userId));

        // Turn DND off through the bridge → back to green (fresh activity at connect).
        store.Update(userId, new UserNotificationPreferences { DoNotDisturb = false });

        Assert.AreEqual(PresenceState.Online, presence.GetDisplayState(userId));
        Assert.AreEqual(PresenceState.Online, raised.Last());
        Assert.IsFalse(inner.Get(userId).DoNotDisturb);
    }

    [TestMethod]
    public void Update_UnchangedDnd_DoesNotRaiseEvent()
    {
        var userId = Guid.CreateVersion7();
        var tracker = new UserConnectionTracker();
        tracker.AddConnection(userId, "conn-1");
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        _ = presence.UserConnectedAsync(userId, "conn-1").GetAwaiter().GetResult();

        var inner = new InMemoryFakeStore();
        var store = new PresenceAwareNotificationPreferenceStore(inner, presence);

        var raiseCount = 0;
        presence.PresenceStateChanged += (_, _) => raiseCount++;

        // DND stays false — no flip, no broadcast.
        store.Update(userId, new UserNotificationPreferences { PushEnabled = true });

        Assert.AreEqual(0, raiseCount);
        Assert.AreEqual(PresenceState.Online, presence.GetDisplayState(userId));
    }

    /// <summary>
    /// Minimal in-memory <see cref="INotificationPreferenceStore"/> for bridge tests.
    /// </summary>
    private sealed class InMemoryFakeStore : INotificationPreferenceStore
    {
        private UserNotificationPreferences _preferences;

        public InMemoryFakeStore(bool initialDoNotDisturb = false)
        {
            _preferences = new UserNotificationPreferences { DoNotDisturb = initialDoNotDisturb };
        }

        public UserNotificationPreferences Get(Guid userId) => _preferences;

        public void Update(Guid userId, UserNotificationPreferences preferences)
        {
            _preferences = preferences;
        }
    }
}

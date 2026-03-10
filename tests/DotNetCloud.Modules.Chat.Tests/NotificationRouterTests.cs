using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

[TestClass]
public class NotificationRouterTests
{
    [TestMethod]
    public async Task SendAsync_WhenPushDisabled_ThenNotificationIsSuppressed()
    {
        var userId = Guid.NewGuid();
        var fcmProvider = new TestPushProvider(PushProvider.FCM);
        var prefs = new InMemoryNotificationPreferenceStore();
        prefs.Update(userId, new UserNotificationPreferences { PushEnabled = false });

        var router = new NotificationRouter(
            [fcmProvider],
            prefs,
            NullLogger<NotificationRouter>.Instance);

        await router.RegisterDeviceAsync(userId, new DeviceRegistration { Token = "token-1", Provider = PushProvider.FCM });
        await router.SendAsync(userId, new PushNotification { Title = "title", Body = "body" });

        Assert.AreEqual(0, fcmProvider.SendCount);
    }

    [TestMethod]
    public async Task SendAsync_WhenUserIsOnline_ThenNotificationIsSuppressed()
    {
        var userId = Guid.NewGuid();
        var fcmProvider = new TestPushProvider(PushProvider.FCM);
        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.IsOnlineAsync(userId)).ReturnsAsync(true);

        var router = new NotificationRouter(
            [fcmProvider],
            new InMemoryNotificationPreferenceStore(),
            NullLogger<NotificationRouter>.Instance,
            presence.Object);

        await router.RegisterDeviceAsync(userId, new DeviceRegistration { Token = "token-1", Provider = PushProvider.FCM });
        await router.SendAsync(userId, new PushNotification { Title = "title", Body = "body" });

        Assert.AreEqual(0, fcmProvider.SendCount);
    }

    [TestMethod]
    public async Task SendAsync_WhenChannelMuted_ThenNotificationIsSuppressed()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var fcmProvider = new TestPushProvider(PushProvider.FCM);
        var prefs = new InMemoryNotificationPreferenceStore();
        prefs.Update(userId, new UserNotificationPreferences
        {
            PushEnabled = true,
            DoNotDisturb = false,
            MutedChannelIds = new HashSet<Guid> { channelId }
        });

        var router = new NotificationRouter(
            [fcmProvider],
            prefs,
            NullLogger<NotificationRouter>.Instance);

        await router.RegisterDeviceAsync(userId, new DeviceRegistration { Token = "token-1", Provider = PushProvider.FCM });
        await router.SendAsync(userId, new PushNotification
        {
            Title = "title",
            Body = "body",
            Category = NotificationCategory.ChatMessage,
            Data = new Dictionary<string, string> { ["channelId"] = channelId.ToString() }
        });

        Assert.AreEqual(0, fcmProvider.SendCount);
    }

    [TestMethod]
    public async Task SendAsync_WhenEligible_ThenRoutesToRegisteredProviders()
    {
        var userId = Guid.NewGuid();
        var fcmProvider = new TestPushProvider(PushProvider.FCM);
        var unifiedProvider = new TestPushProvider(PushProvider.UnifiedPush);

        var router = new NotificationRouter(
            [fcmProvider, unifiedProvider],
            new InMemoryNotificationPreferenceStore(),
            NullLogger<NotificationRouter>.Instance);

        await router.RegisterDeviceAsync(userId, new DeviceRegistration { Token = "fcm-token", Provider = PushProvider.FCM });
        await router.RegisterDeviceAsync(userId, new DeviceRegistration { Token = "up-token", Provider = PushProvider.UnifiedPush, Endpoint = "https://example.com/up" });

        await router.SendAsync(userId, new PushNotification { Title = "title", Body = "body" });

        Assert.AreEqual(1, fcmProvider.SendCount);
        Assert.AreEqual(1, unifiedProvider.SendCount);
    }

    private sealed class TestPushProvider : IPushProviderEndpoint
    {
        public TestPushProvider(PushProvider provider)
        {
            Provider = provider;
        }

        public PushProvider Provider { get; }

        public int SendCount { get; private set; }

        public Task SendAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default)
        {
            SendCount++;
            return Task.CompletedTask;
        }

        public async Task SendToMultipleAsync(IEnumerable<Guid> userIds, PushNotification notification, CancellationToken cancellationToken = default)
        {
            foreach (var userId in userIds)
            {
                await SendAsync(userId, notification, cancellationToken);
            }
        }

        public Task RegisterDeviceAsync(Guid userId, DeviceRegistration registration, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UnregisterDeviceAsync(Guid userId, string deviceToken, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

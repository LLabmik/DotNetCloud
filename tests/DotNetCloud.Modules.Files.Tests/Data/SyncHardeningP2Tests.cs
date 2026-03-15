using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests.Data;

/// <summary>
/// Tests for P2 sync hardening: push change notifications (P2.1) and per-device cursor tracking (P2.2).
/// </summary>
[TestClass]
public class SyncHardeningP2Tests
{
    // ── P2.1: SyncChangeNotifier ─────────────────────────────────────────────

    [TestMethod]
    public async Task NotifyAsync_NoSubscribers_CompletesWithoutError()
    {
        var notifier = new SyncChangeNotifier(NullLoggerFactory.Instance.CreateLogger<SyncChangeNotifier>());
        var userId = Guid.NewGuid();

        // Should not throw when no one is listening
        await notifier.NotifyAsync(userId, 42);
    }

    [TestMethod]
    public async Task SubscribeAsync_ReceivesNotification()
    {
        var notifier = new SyncChangeNotifier(NullLoggerFactory.Instance.CreateLogger<SyncChangeNotifier>());
        var userId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        SyncChangeNotification? received = null;

        var listenTask = Task.Run(async () =>
        {
            await foreach (var n in notifier.SubscribeAsync(userId, cts.Token))
            {
                received = n;
                break; // Got one, stop
            }
        });

        // Give the subscriber a moment to register
        await Task.Delay(50);

        await notifier.NotifyAsync(userId, 7);

        // Wait for listener to process
        var completed = await Task.WhenAny(listenTask, Task.Delay(2000));
        await cts.CancelAsync();

        Assert.IsNotNull(received, "Subscriber should have received the notification.");
        Assert.AreEqual(userId, received!.UserId);
        Assert.AreEqual(7L, received.LatestSequence);
    }

    [TestMethod]
    public async Task SubscribeAsync_MultipleSubscribers_AllReceive()
    {
        var notifier = new SyncChangeNotifier(NullLoggerFactory.Instance.CreateLogger<SyncChangeNotifier>());
        var userId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        var receivedCounts = new int[3];

        var listeners = Enumerable.Range(0, 3).Select(i => Task.Run(async () =>
        {
            await foreach (var n in notifier.SubscribeAsync(userId, cts.Token))
            {
                Interlocked.Increment(ref receivedCounts[i]);
                break;
            }
        })).ToArray();

        await Task.Delay(100);

        await notifier.NotifyAsync(userId, 1);

        await Task.WhenAny(Task.WhenAll(listeners), Task.Delay(2000));
        await cts.CancelAsync();

        Assert.IsTrue(receivedCounts.All(c => c >= 1),
            "All 3 subscribers should have received the notification.");
    }

    [TestMethod]
    public async Task SubscribeAsync_DifferentUsers_IsolatedNotifications()
    {
        var notifier = new SyncChangeNotifier(NullLoggerFactory.Instance.CreateLogger<SyncChangeNotifier>());
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        int receivedA = 0, receivedB = 0;

        var listenerA = Task.Run(async () =>
        {
            await foreach (var _ in notifier.SubscribeAsync(userA, cts.Token))
            {
                Interlocked.Increment(ref receivedA);
                break;
            }
        });

        var listenerB = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in notifier.SubscribeAsync(userB, cts.Token))
                {
                    Interlocked.Increment(ref receivedB);
                    break;
                }
            }
            catch (OperationCanceledException) { }
        });

        await Task.Delay(100);

        // Only notify user A
        await notifier.NotifyAsync(userA, 10);

        await Task.WhenAny(listenerA, Task.Delay(2000));
        // Give a moment to see if B incorrectly receives
        await Task.Delay(100);
        await cts.CancelAsync();

        Assert.AreEqual(1, receivedA, "User A should have received exactly 1 notification.");
        Assert.AreEqual(0, receivedB, "User B should not have received any notification.");
    }

    [TestMethod]
    public void GetConnectionCount_NoSubscribers_ReturnsZero()
    {
        var notifier = new SyncChangeNotifier(NullLoggerFactory.Instance.CreateLogger<SyncChangeNotifier>());
        Assert.AreEqual(0, notifier.GetConnectionCount(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task GetConnectionCount_TracksActiveSubscribers()
    {
        var notifier = new SyncChangeNotifier(NullLoggerFactory.Instance.CreateLogger<SyncChangeNotifier>());
        var userId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        // Start 3 subscribers
        var listeners = Enumerable.Range(0, 3).Select(_ => Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in notifier.SubscribeAsync(userId, cts.Token)) { }
            }
            catch (OperationCanceledException) { }
        })).ToArray();

        await Task.Delay(100);

        Assert.AreEqual(3, notifier.GetConnectionCount(userId));

        await cts.CancelAsync();
        await Task.WhenAny(Task.WhenAll(listeners), Task.Delay(2000));

        // After cancellation, connections should be cleaned up
        Assert.AreEqual(0, notifier.GetConnectionCount(userId));
    }

    [TestMethod]
    public async Task SubscribeAsync_ExceedsMaxConnections_RejectsNewSubscription()
    {
        var notifier = new SyncChangeNotifier(NullLoggerFactory.Instance.CreateLogger<SyncChangeNotifier>());
        var userId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        // Fill up to MaxConnectionsPerUser (25)
        var listeners = Enumerable.Range(0, SyncChangeNotifier.MaxConnectionsPerUser).Select(_ => Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in notifier.SubscribeAsync(userId, cts.Token)) { }
            }
            catch (OperationCanceledException) { }
        })).ToArray();

        await Task.Delay(200);
        Assert.AreEqual(SyncChangeNotifier.MaxConnectionsPerUser, notifier.GetConnectionCount(userId));

        // 26th connection should be rejected (yields nothing)
        int overflowCount = 0;
        await foreach (var _ in notifier.SubscribeAsync(userId, cts.Token))
        {
            overflowCount++;
        }

        Assert.AreEqual(0, overflowCount, "Connection beyond the limit should yield no items.");

        await cts.CancelAsync();
        await Task.WhenAny(Task.WhenAll(listeners), Task.Delay(2000));
    }

    [TestMethod]
    public async Task SubscribeAsync_CancellationCleansUp()
    {
        var notifier = new SyncChangeNotifier(NullLoggerFactory.Instance.CreateLogger<SyncChangeNotifier>());
        var userId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        var listener = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in notifier.SubscribeAsync(userId, cts.Token)) { }
            }
            catch (OperationCanceledException) { }
        });

        await Task.Delay(100);
        Assert.AreEqual(1, notifier.GetConnectionCount(userId));

        await cts.CancelAsync();
        await Task.WhenAny(listener, Task.Delay(2000));

        Assert.AreEqual(0, notifier.GetConnectionCount(userId),
            "Cancelling should remove the subscription.");
    }
}

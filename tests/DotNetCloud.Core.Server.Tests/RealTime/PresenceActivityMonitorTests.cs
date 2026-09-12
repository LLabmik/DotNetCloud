using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.RealTime;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.RealTime;

[TestClass]
public class PresenceIdleTimeoutTests
{
    [TestMethod]
    public void Resolve_Absent_ReturnsDefault()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(3), PresenceIdleTimeout.Resolve(null));
        Assert.AreEqual(TimeSpan.FromMinutes(3), PresenceIdleTimeout.Resolve(string.Empty));
    }

    [TestMethod]
    public void Resolve_Invalid_ReturnsDefault()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(3), PresenceIdleTimeout.Resolve("abc"));
        Assert.AreEqual(TimeSpan.FromMinutes(3), PresenceIdleTimeout.Resolve("  "));
    }

    [TestMethod]
    public void Resolve_ValidValue_ReturnsThreshold()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(5), PresenceIdleTimeout.Resolve("5"));
        Assert.AreEqual(TimeSpan.FromMinutes(1), PresenceIdleTimeout.Resolve("1"));
        Assert.AreEqual(TimeSpan.FromMinutes(60), PresenceIdleTimeout.Resolve("60"));
    }

    [TestMethod]
    public void Resolve_OutOfRange_Clamps()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(1), PresenceIdleTimeout.Resolve("0"));
        Assert.AreEqual(TimeSpan.FromMinutes(1), PresenceIdleTimeout.Resolve("-5"));
        Assert.AreEqual(TimeSpan.FromMinutes(60), PresenceIdleTimeout.Resolve("120"));
        Assert.AreEqual(TimeSpan.FromMinutes(60), PresenceIdleTimeout.Resolve("1000"));
    }
}

[TestClass]
public class PresenceActivityMonitorTests
{
    private UserConnectionTracker _tracker = null!;
    private MutableTimeProvider _time = null!;
    private PresenceService _presenceService = null!;

    [TestInitialize]
    public void Setup()
    {
        _tracker = new UserConnectionTracker();
        _time = new MutableTimeProvider();
        _presenceService = new PresenceService(
            _tracker,
            NullLogger<PresenceService>.Instance,
            dbContextFactory: null,
            timeProvider: _time);
    }

    [TestMethod]
    public async Task Sweep_AppliesThresholdAndFlipsActiveUsersToAway()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        await _presenceService.UserConnectedAsync(userId, "conn-1");
        _presenceService.UpdateIdleThreshold(TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromMinutes(3));

        await _presenceService.RecomputeAsync(userId);

        Assert.AreEqual(PresenceState.Away, _presenceService.GetDisplayState(userId));
    }

    [TestMethod]
    public async Task Sweep_WithFreshActivity_StaysOnline()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        await _presenceService.UserConnectedAsync(userId, "conn-1");
        _presenceService.UpdateIdleThreshold(TimeSpan.FromMinutes(5));
        _time.Advance(TimeSpan.FromMinutes(1)); // still within threshold

        var state = await _presenceService.RecomputeAsync(userId);

        Assert.AreEqual(PresenceState.Online, state);
    }

    [TestMethod]
    public async Task Sweep_HonorsShrinkingAdminThreshold()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        await _presenceService.UserConnectedAsync(userId, "conn-1");

        // Admin lowers the threshold to 1 minute; after 2 minutes idle the user goes away.
        _presenceService.UpdateIdleThreshold(TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromMinutes(2));

        var state = await _presenceService.RecomputeAsync(userId);

        Assert.AreEqual(PresenceState.Away, state);
    }

    [TestMethod]
    public async Task Sweep_DndUserStaysDoNotDisturb_NotAway()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        await _presenceService.UserConnectedAsync(userId, "conn-1");
        _presenceService.CacheDoNotDisturb(userId, enabled: true);
        _presenceService.UpdateIdleThreshold(TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromMinutes(5));

        var state = await _presenceService.RecomputeAsync(userId);

        Assert.AreEqual(PresenceState.DoNotDisturb, state);
    }

    /// <summary>
    /// A simple controllable <see cref="TimeProvider"/> for deterministic idle/away tests.
    /// </summary>
    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }
}

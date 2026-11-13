using DotNetCloud.Core.Server.Quotas;

namespace DotNetCloud.Core.Server.Tests.Quotas;

/// <summary>
/// Tests for <see cref="ModuleRateLimiter"/>: fixed one-minute windows keyed by module and
/// category, with a single "first rejection" signal per window.
/// </summary>
[TestClass]
public class ModuleRateLimiterTests
{
    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private const string ModuleId = "dotnetcloud.rate-test";

    private static ManualTimeProvider CreateClock() =>
        new(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void TryAcquire_WithinLimit_AllowsEveryCall()
    {
        var limiter = new ModuleRateLimiter(CreateClock());

        for (var i = 0; i < 3; i++)
        {
            Assert.AreEqual(
                RateLimitOutcome.Allowed,
                limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Request, 3));
        }
    }

    [TestMethod]
    public void TryAcquire_AtLimit_RejectsAndFlagsFirstRejectionOnce()
    {
        var limiter = new ModuleRateLimiter(CreateClock());

        limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Request, 1);
        Assert.AreEqual(RateLimitOutcome.LimitedFirstInWindow, limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Request, 1));
        Assert.AreEqual(RateLimitOutcome.Limited, limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Request, 1));
    }

    [TestMethod]
    public void TryAcquire_AfterWindowRollover_ResetsTheCounter()
    {
        var clock = CreateClock();
        var limiter = new ModuleRateLimiter(clock);

        limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Event, 1);
        Assert.AreEqual(RateLimitOutcome.LimitedFirstInWindow, limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Event, 1));

        clock.Now = clock.Now.AddMinutes(1);

        Assert.AreEqual(RateLimitOutcome.Allowed, limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Event, 1));
    }

    [TestMethod]
    public void TryAcquire_WithNonPositiveLimit_IsUnlimited()
    {
        var limiter = new ModuleRateLimiter(CreateClock());

        for (var i = 0; i < 10; i++)
        {
            Assert.AreEqual(
                RateLimitOutcome.Allowed,
                limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Audit, 0));
        }
    }

    [TestMethod]
    public void TryAcquire_DifferentModulesAndKinds_AreIndependent()
    {
        var limiter = new ModuleRateLimiter(CreateClock());

        limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Request, 1);
        Assert.AreEqual(RateLimitOutcome.LimitedFirstInWindow, limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Request, 1));

        Assert.AreEqual(
            RateLimitOutcome.Allowed,
            limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Event, 1),
            "Event budget is independent of the request budget");

        Assert.AreEqual(
            RateLimitOutcome.Allowed,
            limiter.TryAcquire("dotnetcloud.other", ModuleRateLimitKind.Request, 1),
            "Counters must be keyed by module");
    }

    [TestMethod]
    public void TryAcquire_RepeatedlyBeyondTheLimit_SaturatesAndKeepsRejecting()
    {
        var limiter = new ModuleRateLimiter(CreateClock());

        for (var i = 0; i < 5; i++)
        {
            limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Notification, 2);
        }

        Assert.AreEqual(RateLimitOutcome.Limited, limiter.TryAcquire(ModuleId, ModuleRateLimitKind.Notification, 2));
        Assert.IsTrue(limiter.GetCurrentCount(ModuleId, ModuleRateLimitKind.Notification) <= 3);
    }
}

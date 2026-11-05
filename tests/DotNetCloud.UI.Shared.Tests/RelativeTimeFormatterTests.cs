using DotNetCloud.UI.Shared.Services;

namespace DotNetCloud.UI.Shared.Tests;

/// <summary>
/// Unit tests for <see cref="RelativeTimeFormatter"/>.
/// </summary>
[TestClass]
public class RelativeTimeFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Format_NullValue_ReturnsEmDash()
    {
        Assert.AreEqual("—", RelativeTimeFormatter.Format((DateTimeOffset?)null, now: Now));
    }

    [TestMethod]
    public void Format_LessThanOneMinute_ReturnsJustNow()
    {
        var value = Now.AddSeconds(-30);

        Assert.AreEqual("just now", RelativeTimeFormatter.Format(value, now: Now));
    }

    [TestMethod]
    public void Format_MinutesHoursAndDays_UseCompactUnits()
    {
        Assert.AreEqual("5m ago", RelativeTimeFormatter.Format(Now.AddMinutes(-5), now: Now));
        Assert.AreEqual("3h ago", RelativeTimeFormatter.Format(Now.AddHours(-3), now: Now));
        Assert.AreEqual("2d ago", RelativeTimeFormatter.Format(Now.AddDays(-2), now: Now));
    }

    [TestMethod]
    public void Format_CustomJustNowLabel_IsUsed()
    {
        var options = RelativeTimeOptions.Default with { JustNowLabel = "Just now" };

        Assert.AreEqual("Just now", RelativeTimeFormatter.Format(Now.AddSeconds(-10), options, Now));
    }

    [TestMethod]
    public void Format_IncludeSeconds_ReturnsSecondsAndRemainders()
    {
        var options = RelativeTimeOptions.Default with
        {
            IncludeSeconds = true,
            IncludeRemainder = true,
        };

        Assert.AreEqual("12s ago", RelativeTimeFormatter.Format(Now.AddSeconds(-12), options, Now));
        Assert.AreEqual("3h 12m ago", RelativeTimeFormatter.Format(Now.AddHours(-3).AddMinutes(-12), options, Now));
        Assert.AreEqual("2d 5h ago", RelativeTimeFormatter.Format(Now.AddDays(-2).AddHours(-5), options, Now));
    }

    [TestMethod]
    public void Format_PastAbsoluteThreshold_UsesFallback()
    {
        var options = RelativeTimeOptions.Default with
        {
            DaysBeforeAbsolute = 7,
            AbsoluteFormat = value => value.UtcDateTime.ToString("yyyy-MM-dd"),
        };

        Assert.AreEqual("6d ago", RelativeTimeFormatter.Format(Now.AddDays(-6), options, Now));
        Assert.AreEqual("2026-10-28", RelativeTimeFormatter.Format(Now.AddDays(-7), options, Now));
        Assert.AreEqual("2026-10-20", RelativeTimeFormatter.Format(Now.AddDays(-15), options, Now));
    }

    [TestMethod]
    public void Format_FutureValue_IsClampedToZero()
    {
        var options = RelativeTimeOptions.Default with { IncludeSeconds = true };

        Assert.AreEqual("0s ago", RelativeTimeFormatter.Format(Now.AddMinutes(5), options, Now));
    }

    [TestMethod]
    public void Format_DateTimeOverload_TreatsValueAsUtc()
    {
        var now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        Assert.AreEqual("5m ago", RelativeTimeFormatter.Format(now.AddMinutes(-5), nowUtc: now));
    }

    [TestMethod]
    public void Format_NullableDateTime_ReturnsConfiguredEmptyText()
    {
        var options = RelativeTimeOptions.Default with { EmptyText = "Never" };

        Assert.AreEqual("Never", RelativeTimeFormatter.Format((DateTime?)null, options));
    }
}

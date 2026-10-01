using DotNetCloud.UI.Shared.Components.Dialogs;

namespace DotNetCloud.UI.Shared.Tests;

/// <summary>
/// Unit tests for <see cref="ShareExpiryFormatter"/>, which backs the expiry labels in the
/// Files "Shared with me" / "Shared by me" views and the share dialog's "Current shares" list.
/// </summary>
[TestClass]
public class ShareExpiryFormatterTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A share without an expiration date must say so instead of rendering nothing.</summary>
    [TestMethod]
    public void Format_NullExpiry_ReturnsNeverExpiresLabel()
    {
        var result = ShareExpiryFormatter.Format(null, NowUtc);

        Assert.AreEqual("Never expires", result);
        Assert.AreEqual(ShareExpiryFormatter.NeverExpiresLabel, result);
    }

    [TestMethod]
    public void Format_PastExpiry_ReturnsExpired()
    {
        var result = ShareExpiryFormatter.Format(NowUtc.AddMinutes(-5), NowUtc);

        Assert.AreEqual("Expired", result);
    }

    [TestMethod]
    [DataRow(6, "Expires today")]
    [DataRow(24, "Expires tomorrow")]
    [DataRow(72, "Expires in 3 days")]
    [DataRow(240, "Expires in 1 week")]
    [DataRow(480, "Expires in 2 weeks")]
    public void Format_FutureExpiry_ReturnsRelativeLabel(int hoursFromNow, string expected)
    {
        var result = ShareExpiryFormatter.Format(NowUtc.AddHours(hoursFromNow), NowUtc);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Format_ExpiryBeyondAMonth_ReturnsAbsoluteDate()
    {
        var expiresAt = NowUtc.AddDays(45);

        var result = ShareExpiryFormatter.Format(expiresAt, NowUtc);

        Assert.AreEqual($"Expires on {expiresAt:MMM d, yyyy}", result);
    }

    /// <summary>
    /// The relative text is reused verbatim by the "Shared by me" view inside its
    /// "(expires …)" suffix, so it must stay lowercase and un-prefixed.
    /// </summary>
    [TestMethod]
    [DataRow(-1, "expired")]
    [DataRow(6, "today")]
    [DataRow(24, "tomorrow")]
    [DataRow(72, "in 3 days")]
    [DataRow(240, "in 1 week")]
    [DataRow(480, "in 2 weeks")]
    public void FormatRelative_DiffersByOffset_ReturnsExpectedText(int hoursFromNow, string expected)
    {
        var result = ShareExpiryFormatter.FormatRelative(NowUtc.AddHours(hoursFromNow), NowUtc);

        Assert.AreEqual(expected, result);
    }

    /// <summary>
    /// Any instant in the past reads as "expired", even when only a few hours have elapsed.
    /// </summary>
    [TestMethod]
    public void FormatRelative_ExpiredEarlierToday_ReturnsExpired()
    {
        var result = ShareExpiryFormatter.FormatRelative(NowUtc.AddHours(-3), NowUtc);

        Assert.AreEqual("expired", result);
    }

    [TestMethod]
    public void FormatRelative_BeyondAMonth_ReturnsAbsoluteDateWithOnPrefix()
    {
        var expiresAt = NowUtc.AddDays(45);

        var result = ShareExpiryFormatter.FormatRelative(expiresAt, NowUtc);

        Assert.AreEqual($"on {expiresAt:MMM d, yyyy}", result);
    }
}

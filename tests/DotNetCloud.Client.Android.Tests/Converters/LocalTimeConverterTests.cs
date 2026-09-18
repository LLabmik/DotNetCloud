using System.Globalization;
using DotNetCloud.Client.Android.Converters;

namespace DotNetCloud.Client.Android.Tests.Converters;

/// <summary>
/// Tests for the converter that renders calendar times in the device's local time zone. Calendar
/// DTOs carry UTC timestamps, and JSON deserialization drops <see cref="DateTime.Kind"/>, which
/// would otherwise be misread as local time and shift every event.
/// </summary>
[TestClass]
public sealed class LocalTimeConverterTests
{
    private readonly LocalTimeConverter _converter = new();

    [TestMethod]
    public void Convert_UtcDateTime_FormatsLocalTime()
    {
        var utc = new DateTime(2026, 9, 18, 14, 30, 0, DateTimeKind.Utc);

        Assert.AreEqual(
            utc.ToLocalTime().ToString("HH:mm"),
            _converter.Convert(utc, typeof(string), null!, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void Convert_UnspecifiedKind_IsTreatedAsUtc()
    {
        var utc = new DateTime(2026, 9, 18, 14, 30, 0, DateTimeKind.Utc);
        var fromJson = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);

        Assert.AreEqual(
            _converter.Convert(utc, typeof(string), null!, CultureInfo.InvariantCulture),
            _converter.Convert(fromJson, typeof(string), null!, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void Convert_CustomFormat_UsesIt()
    {
        var utc = new DateTime(2026, 9, 18, 14, 30, 0, DateTimeKind.Utc);

        Assert.AreEqual(
            utc.ToLocalTime().ToString("h:mm tt"),
            _converter.Convert(utc, typeof(string), "h:mm tt", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void Convert_EmptyFormat_FallsBackToDefault()
    {
        var utc = new DateTime(2026, 9, 18, 14, 30, 0, DateTimeKind.Utc);

        Assert.AreEqual(
            utc.ToLocalTime().ToString(LocalTimeConverter.DefaultFormat),
            _converter.Convert(utc, typeof(string), "", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void Convert_Null_ReturnsEmptyString()
    {
        Assert.AreEqual(string.Empty, _converter.Convert(null, typeof(string), null!, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ConvertBack_Throws()
    {
        Assert.ThrowsExactly<NotSupportedException>(() =>
            _converter.ConvertBack("14:30", typeof(DateTime), null!, CultureInfo.InvariantCulture));
    }
}

using DotNetCloud.UI.Shared.Services;
using Microsoft.JSInterop;
using Moq;

namespace DotNetCloud.UI.Shared.Tests;

/// <summary>
/// Unit tests for <see cref="BrowserTimeProvider"/>.
/// </summary>
[TestClass]
public class BrowserTimeProviderTests
{
    private Mock<IJSRuntime> _jsMock = null!;
    private BrowserTimeProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        _jsMock = new Mock<IJSRuntime>();
        _provider = new BrowserTimeProvider(_jsMock.Object);
    }

    // ─── Initialization ──────────────────────────────────────────────

    [TestMethod]
    public async Task EnsureInitializedAsync_FetchesTimezoneFromJs()
    {
        SetupJsTimezone("America/Chicago");

        await _provider.EnsureInitializedAsync();

        Assert.AreEqual("America/Chicago", _provider.IanaTimeZone);
    }

    [TestMethod]
    public async Task EnsureInitializedAsync_IsIdempotent()
    {
        SetupJsTimezone("Europe/London");

        await _provider.EnsureInitializedAsync();
        await _provider.EnsureInitializedAsync();
        await _provider.EnsureInitializedAsync();

        _jsMock.Verify(js => js.InvokeAsync<string>(
            "Intl.DateTimeFormat().resolvedOptions().timeZone",
            It.IsAny<object[]>()), Times.Once);
    }

    [TestMethod]
    public async Task EnsureInitializedAsync_FallsBackToUtc_WhenJsThrows()
    {
        _jsMock.Setup(js => js.InvokeAsync<string>(
                "Intl.DateTimeFormat().resolvedOptions().timeZone",
                It.IsAny<object[]>()))
            .ThrowsAsync(new InvalidOperationException("Prerendering"));

        await _provider.EnsureInitializedAsync();

        Assert.AreEqual("UTC", _provider.IanaTimeZone);
    }

    [TestMethod]
    public async Task EnsureInitializedAsync_FallsBackToUtc_WhenJsReturnsNull()
    {
        _jsMock.Setup(js => js.InvokeAsync<string>(
                "Intl.DateTimeFormat().resolvedOptions().timeZone",
                It.IsAny<object[]>()))
            .ReturnsAsync((string)null!);

        await _provider.EnsureInitializedAsync();

        Assert.AreEqual("UTC", _provider.IanaTimeZone);
    }

    [TestMethod]
    public async Task EnsureInitializedAsync_FallsBackToUtc_WhenJsReturnsWhitespace()
    {
        _jsMock.Setup(js => js.InvokeAsync<string>(
                "Intl.DateTimeFormat().resolvedOptions().timeZone",
                It.IsAny<object[]>()))
            .ReturnsAsync("   ");

        await _provider.EnsureInitializedAsync();

        Assert.AreEqual("UTC", _provider.IanaTimeZone);
    }

    // ─── IanaTimeZone property ───────────────────────────────────────

    [TestMethod]
    public void IanaTimeZone_DefaultsToUtc_BeforeInitialization()
    {
        Assert.AreEqual("UTC", _provider.IanaTimeZone);
    }

    // ─── ToLocal(DateTime) ───────────────────────────────────────────

    [TestMethod]
    public async Task ToLocal_ConvertsUtcToTimezone()
    {
        SetupJsTimezone("America/New_York");
        await _provider.EnsureInitializedAsync();

        // 2026-01-15 18:00 UTC = 2026-01-15 13:00 EST (UTC-5, standard time)
        var utc = new DateTime(2026, 1, 15, 18, 0, 0, DateTimeKind.Utc);
        var local = _provider.ToLocal(utc);

        Assert.AreEqual(13, local.Hour);
        Assert.AreEqual(15, local.Day);
    }

    [TestMethod]
    public async Task ToLocal_HandlesUnspecifiedKind()
    {
        SetupJsTimezone("America/New_York");
        await _provider.EnsureInitializedAsync();

        // Unspecified kind is treated as UTC by SpecifyKind in BrowserTimeProvider
        var unspecified = new DateTime(2026, 1, 15, 18, 0, 0, DateTimeKind.Unspecified);
        var local = _provider.ToLocal(unspecified);

        Assert.AreEqual(13, local.Hour);
    }

    [TestMethod]
    public void ToLocal_ReturnsOriginal_WhenNotInitialized()
    {
        var input = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var result = _provider.ToLocal(input);

        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public async Task ToLocal_ReturnsSameTime_WhenFallbackUtc()
    {
        _jsMock.Setup(js => js.InvokeAsync<string>(
                "Intl.DateTimeFormat().resolvedOptions().timeZone",
                It.IsAny<object[]>()))
            .ThrowsAsync(new InvalidOperationException("Prerendering"));
        await _provider.EnsureInitializedAsync();

        var utc = new DateTime(2026, 3, 10, 15, 30, 0, DateTimeKind.Utc);
        var result = _provider.ToLocal(utc);

        Assert.AreEqual(15, result.Hour);
        Assert.AreEqual(30, result.Minute);
    }

    [TestMethod]
    public async Task ToLocal_HandlesDaylightSavingTime()
    {
        SetupJsTimezone("America/New_York");
        await _provider.EnsureInitializedAsync();

        // July = EDT (UTC-4)
        var summerUtc = new DateTime(2026, 7, 15, 18, 0, 0, DateTimeKind.Utc);
        var summerLocal = _provider.ToLocal(summerUtc);
        Assert.AreEqual(14, summerLocal.Hour);

        // January = EST (UTC-5)
        var winterUtc = new DateTime(2026, 1, 15, 18, 0, 0, DateTimeKind.Utc);
        var winterLocal = _provider.ToLocal(winterUtc);
        Assert.AreEqual(13, winterLocal.Hour);
    }

    [TestMethod]
    public async Task ToLocal_PreservesDate_AcrossDayBoundary()
    {
        SetupJsTimezone("America/Los_Angeles");
        await _provider.EnsureInitializedAsync();

        // 2026-03-15 03:00 UTC = 2026-03-14 20:00 PDT (UTC-7 in March DST)
        var utc = new DateTime(2026, 3, 15, 3, 0, 0, DateTimeKind.Utc);
        var local = _provider.ToLocal(utc);

        Assert.AreEqual(14, local.Day);
        Assert.AreEqual(20, local.Hour);
    }

    // ─── ToLocal(DateTime?) ─────────────────────────────────────────

    [TestMethod]
    public async Task ToLocalNullable_ReturnsNull_ForNullInput()
    {
        SetupJsTimezone("America/Chicago");
        await _provider.EnsureInitializedAsync();

        DateTime? input = null;
        var result = _provider.ToLocal(input);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ToLocalNullable_ConvertsValue_ForNonNullInput()
    {
        SetupJsTimezone("America/Chicago");
        await _provider.EnsureInitializedAsync();

        // 2026-01-15 18:00 UTC = 2026-01-15 12:00 CST (UTC-6, standard time)
        DateTime? input = new DateTime(2026, 1, 15, 18, 0, 0, DateTimeKind.Utc);
        var result = _provider.ToLocal(input);

        Assert.IsNotNull(result);
        Assert.AreEqual(12, result.Value.Hour);
    }

    // ─── Multiple timezones ──────────────────────────────────────────

    [TestMethod]
    public async Task ToLocal_VariousTimezones_CorrectOffsets()
    {
        // Test with Europe/Berlin (CET = UTC+1 in winter)
        var jsMock = new Mock<IJSRuntime>();
        jsMock.Setup(js => js.InvokeAsync<string>(
                "Intl.DateTimeFormat().resolvedOptions().timeZone",
                It.IsAny<object[]>()))
            .ReturnsAsync("Europe/Berlin");

        var provider = new BrowserTimeProvider(jsMock.Object);
        await provider.EnsureInitializedAsync();

        var utc = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var local = provider.ToLocal(utc);

        Assert.AreEqual(13, local.Hour); // UTC+1 in January
    }

    // ─── Helper ─────────────────────────────────────────────────────

    private void SetupJsTimezone(string iana)
    {
        _jsMock.Setup(js => js.InvokeAsync<string>(
                "Intl.DateTimeFormat().resolvedOptions().timeZone",
                It.IsAny<object[]>()))
            .ReturnsAsync(iana);
    }
}

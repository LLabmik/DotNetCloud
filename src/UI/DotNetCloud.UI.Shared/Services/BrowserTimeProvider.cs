using Microsoft.JSInterop;

namespace DotNetCloud.UI.Shared.Services;

/// <summary>
/// Provides the browser's IANA timezone and converts UTC dates to the user's local time.
/// Uses JS interop to read <c>Intl.DateTimeFormat().resolvedOptions().timeZone</c> once per circuit.
/// </summary>
public sealed class BrowserTimeProvider
{
    /// <summary>
    /// LocalStorage key used as a fast, per-browser cache of the user's clock-format
    /// preference so separately-interactive components can format times consistently.
    /// </summary>
    public const string TimeFormatStorageKey = "dotnetcloud.timeFormat";

    private readonly IJSRuntime _js;
    private TimeZoneInfo? _timeZone;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of <see cref="BrowserTimeProvider"/>.
    /// </summary>
    public BrowserTimeProvider(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Gets the user's IANA timezone identifier (e.g. "America/Chicago").
    /// Returns "UTC" if not yet initialized or if detection failed.
    /// </summary>
    public string IanaTimeZone { get; private set; } = "UTC";

    /// <summary>
    /// Gets a value indicating whether times should be rendered with a 24-hour clock.
    /// Defaults to <see langword="false"/> (12-hour / AM-PM), matching the app's historical display.
    /// </summary>
    public bool Use24HourClock { get; private set; }

    /// <summary>
    /// Gets the clock-format pattern that matches <see cref="Use24HourClock"/>.
    /// </summary>
    public string TimeFormatPattern => Use24HourClock ? "HH:mm" : "h:mm tt";

    /// <summary>
    /// Applies a stored clock-format preference (see <see cref="UserTimeFormat"/>).
    /// A missing or unrecognised value selects the 12-hour default.
    /// </summary>
    public void ApplyTimeFormat(string? storedValue)
        => Use24HourClock = UserTimeFormat.IsTwentyFourHour(storedValue);

    /// <summary>
    /// Applies whether the user prefers a 24-hour clock.
    /// </summary>
    public void SetUse24HourClock(bool use24HourClock)
        => Use24HourClock = use24HourClock;

    /// <summary>
    /// Formats the time of day using the user's clock preference.
    /// </summary>
    public string FormatTime(DateTime dateTime)
        => dateTime.ToString(TimeFormatPattern);

    /// <summary>
    /// Formats the time of day using the user's clock preference, returning an empty
    /// string when <paramref name="dateTime"/> is <see langword="null"/>.
    /// </summary>
    public string FormatTime(DateTime? dateTime)
        => dateTime.HasValue ? FormatTime(dateTime.Value) : string.Empty;

    /// <summary>
    /// Formats a date and time using the user's clock preference.
    /// </summary>
    public string FormatDateTime(DateTime dateTime, string dateFormat = "MMM d, yyyy")
        => $"{dateTime.ToString(dateFormat)} {FormatTime(dateTime)}";

    /// <summary>
    /// Formats a nullable date and time using the user's clock preference, returning an
    /// empty string when <paramref name="dateTime"/> is <see langword="null"/>.
    /// </summary>
    public string FormatDateTime(DateTime? dateTime, string dateFormat = "MMM d, yyyy")
        => dateTime.HasValue ? FormatDateTime(dateTime.Value, dateFormat) : string.Empty;

    /// <summary>
    /// Formats a date and time including seconds using the user's clock preference.
    /// Intended for diagnostic surfaces that need second-level precision.
    /// </summary>
    public string FormatDateTimeWithSeconds(DateTime dateTime, string dateFormat = "yyyy-MM-dd")
        => $"{dateTime.ToString(dateFormat)} {dateTime.ToString(Use24HourClock ? "HH:mm:ss" : "h:mm:ss tt")}";

    /// <summary>
    /// Formats a <see cref="DateTimeOffset"/> in the browser's local timezone using the
    /// user's clock preference.
    /// </summary>
    public string FormatDateTime(DateTimeOffset dateTime, string dateFormat = "MMM d, yyyy")
        => FormatDateTime(dateTime.LocalDateTime, dateFormat);

    /// <summary>
    /// Formats a <see cref="DateTimeOffset"/> including seconds in the browser's local
    /// timezone using the user's clock preference.
    /// </summary>
    public string FormatDateTimeWithSeconds(DateTimeOffset dateTime, string dateFormat = "yyyy-MM-dd")
        => FormatDateTimeWithSeconds(dateTime.LocalDateTime, dateFormat);

    /// <summary>
    /// Ensures the browser timezone has been fetched. Safe to call multiple times.
    /// </summary>
    public async ValueTask EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        try
        {
            var iana = await _js.InvokeAsync<string>("Intl.DateTimeFormat().resolvedOptions().timeZone");
            if (!string.IsNullOrWhiteSpace(iana))
            {
                IanaTimeZone = iana;
                _timeZone = TimeZoneInfo.FindSystemTimeZoneById(iana);
            }
        }
        catch
        {
            // Fallback to UTC if JS interop fails (e.g. prerendering)
            _timeZone = TimeZoneInfo.Utc;
        }

        try
        {
            var storedFormat = await _js.InvokeAsync<string?>("localStorage.getItem", TimeFormatStorageKey);
            if (!string.IsNullOrWhiteSpace(storedFormat))
            {
                ApplyTimeFormat(storedFormat);
            }
        }
        catch
        {
            // localStorage is unavailable during prerendering; the server setting
            // remains the source of truth and is applied by the layout.
        }

        _timeZone ??= TimeZoneInfo.Utc;
        _initialized = true;
    }

    /// <summary>
    /// Persists the clock-format preference to local storage for subsequent renders.
    /// Failures (e.g. prerendering) are ignored.
    /// </summary>
    public async ValueTask PersistTimeFormatAsync(string? storedValue)
    {
        try
        {
            await _js.InvokeVoidAsync(
                "localStorage.setItem",
                TimeFormatStorageKey,
                string.IsNullOrWhiteSpace(storedValue) ? UserTimeFormat.TwelveHour : storedValue);
        }
        catch
        {
            // localStorage unavailable (prerender or blocked storage).
        }
    }

    /// <summary>
    /// Converts a UTC <see cref="DateTime"/> to the user's local time.
    /// </summary>
    public DateTime ToLocal(DateTime utcDateTime)
    {
        if (!_initialized || _timeZone is null)
            return utcDateTime;

        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);
    }

    /// <summary>
    /// Converts a nullable UTC <see cref="DateTime"/> to the user's local time.
    /// </summary>
    public DateTime? ToLocal(DateTime? utcDateTime)
    {
        return utcDateTime.HasValue ? ToLocal(utcDateTime.Value) : null;
    }
}

using Microsoft.JSInterop;

namespace DotNetCloud.UI.Shared.Services;

/// <summary>
/// Provides the browser's IANA timezone and converts UTC dates to the user's local time.
/// Uses JS interop to read <c>Intl.DateTimeFormat().resolvedOptions().timeZone</c> once per circuit.
/// </summary>
public sealed class BrowserTimeProvider
{
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
    /// Ensures the browser timezone has been fetched. Safe to call multiple times.
    /// </summary>
    public async ValueTask EnsureInitializedAsync()
    {
        if (_initialized) return;

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

        _timeZone ??= TimeZoneInfo.Utc;
        _initialized = true;
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

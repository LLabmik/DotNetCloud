using System.Globalization;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Persists the small amount of state the background chat-alert poll needs between runs: the entity-tag
/// it last received, the newest change it has already accounted for, and whether messages are waiting.
/// </summary>
/// <remarks>
/// Android-free by design (it goes through <see cref="IAppPreferences"/>), so its round-trip handling is
/// unit-testable on plain <c>net10.0</c>. Values are non-sensitive — no message content is ever stored.
/// </remarks>
public sealed class ChatAlertStateStore
{
    /// <summary>Preference key holding the last entity-tag.</summary>
    internal const string ETagKey = "chat_alert_etag";

    /// <summary>Preference key holding the newest accounted-for change (ISO-8601 UTC).</summary>
    internal const string LastAcknowledgedKey = "chat_alert_last_acknowledged_utc";

    /// <summary>Preference key holding whether unmuted unread messages were outstanding.</summary>
    internal const string HasUnreadKey = "chat_alert_has_unread";

    private readonly IAppPreferences _preferences;

    /// <summary>Initializes a new <see cref="ChatAlertStateStore"/>.</summary>
    /// <param name="preferences">Preference store to persist into.</param>
    public ChatAlertStateStore(IAppPreferences preferences)
    {
        _preferences = preferences;
    }

    /// <summary>Gets the entity-tag returned by the previous poll, or null when there is none.</summary>
    /// <returns>The stored entity-tag, or null.</returns>
    public string? GetETag()
    {
        var value = _preferences.Get(ETagKey, string.Empty);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Stores the entity-tag from the latest poll; null clears it.</summary>
    /// <param name="etag">Entity-tag to store.</param>
    public void SetETag(string? etag) => _preferences.Set(ETagKey, etag ?? string.Empty);

    /// <summary>Gets the newest change the app has accounted for, or null when it never has.</summary>
    /// <returns>The high-water mark in UTC, or null.</returns>
    public DateTime? GetLastAcknowledgedChangedAtUtc() =>
        ParseUtc(_preferences.Get(LastAcknowledgedKey, string.Empty));

    /// <summary>Stores the newest change the app has accounted for; null clears it.</summary>
    /// <param name="value">High-water mark to store.</param>
    public void SetLastAcknowledgedChangedAtUtc(DateTime? value) =>
        _preferences.Set(
            LastAcknowledgedKey,
            value is { } utc
                ? utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : string.Empty);

    /// <summary>
    /// Gets whether unmuted unread messages were outstanding at the last full read.
    /// </summary>
    /// <remarks>
    /// Needed because a <c>304</c> carries no counts: without this the cadence would drop to the idle
    /// interval while the user still has messages waiting to be noticed.
    /// </remarks>
    /// <returns>True when unread messages were outstanding.</returns>
    public bool GetHasUnread() => _preferences.Get(HasUnreadKey, false);

    /// <summary>Stores whether unmuted unread messages are outstanding.</summary>
    /// <param name="hasUnread">Whether unread messages are outstanding.</param>
    public void SetHasUnread(bool hasUnread) => _preferences.Set(HasUnreadKey, hasUnread);

    /// <summary>
    /// Parses a round-tripped UTC timestamp, tolerating anything unparsable.
    /// </summary>
    /// <param name="raw">Stored value.</param>
    /// <returns>The timestamp in UTC, or null.</returns>
    internal static DateTime? ParseUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc)
            : null;
    }
}

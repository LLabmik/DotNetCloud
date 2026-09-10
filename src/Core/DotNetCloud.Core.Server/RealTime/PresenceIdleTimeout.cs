namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Resolves the admin-configured presence idle threshold (in minutes) from the raw
/// <c>PresenceIdleTimeoutMinutes</c> system-setting value with sane clamping.
/// </summary>
internal static class PresenceIdleTimeout
{
    /// <summary>
    /// Minimum allowed idle threshold in minutes.
    /// </summary>
    internal const int MinMinutes = 1;

    /// <summary>
    /// Maximum allowed idle threshold in minutes.
    /// </summary>
    internal const int MaxMinutes = 60;

    /// <summary>
    /// Resolves a raw setting value into an idle threshold.
    /// </summary>
    /// <param name="rawValue">The raw string value from the system setting, or <c>null</c> when absent.</param>
    /// <returns>
    /// The clamped threshold. When the value is absent, non-numeric, or out of range the
    /// <see cref="PresenceService.DefaultIdleThreshold"/> is returned (3 minutes).
    /// </returns>
    internal static TimeSpan Resolve(string? rawValue)
    {
        if (!int.TryParse(rawValue, out var minutes))
        {
            return PresenceService.DefaultIdleThreshold;
        }

        if (minutes < MinMinutes)
        {
            return TimeSpan.FromMinutes(MinMinutes);
        }

        if (minutes > MaxMinutes)
        {
            return TimeSpan.FromMinutes(MaxMinutes);
        }

        return TimeSpan.FromMinutes(minutes);
    }
}

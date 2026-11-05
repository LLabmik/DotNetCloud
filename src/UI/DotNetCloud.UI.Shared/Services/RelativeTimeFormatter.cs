namespace DotNetCloud.UI.Shared.Services;

/// <summary>
/// Optional shaping for <see cref="RelativeTimeFormatter"/> output. The defaults render
/// "just now", "5m ago", "3h ago" and "2d ago".
/// </summary>
public sealed record RelativeTimeOptions
{
    /// <summary>The default compact style used across most of the UI.</summary>
    public static readonly RelativeTimeOptions Default = new();

    /// <summary>Label shown for values less than a minute old. Defaults to "just now".</summary>
    public string JustNowLabel { get; init; } = "just now";

    /// <summary>
    /// When <see langword="true"/>, sub-minute values render as "Ns ago" and larger
    /// buckets include the smaller remainder ("3h 12m ago", "2d 5h ago").
    /// </summary>
    public bool IncludeSeconds { get; init; }

    /// <summary>
    /// When <see langword="true"/>, hour and day buckets include the smaller unit
    /// ("3h 12m ago", "2d 5h ago"). Ignored unless <see cref="IncludeSeconds"/> is set.
    /// </summary>
    public bool IncludeRemainder { get; init; }

    /// <summary>
    /// Number of days after which <see cref="AbsoluteFormat"/> is used. Defaults to
    /// <see cref="int.MaxValue"/>, which always keeps the relative day count.
    /// </summary>
    public int DaysBeforeAbsolute { get; init; } = int.MaxValue;

    /// <summary>
    /// Optional absolute fallback invoked once the relative age reaches
    /// <see cref="DaysBeforeAbsolute"/>. Receives the original value.
    /// </summary>
    public Func<DateTimeOffset, string>? AbsoluteFormat { get; init; }

    /// <summary>Text returned for <see langword="null"/> or default values. Defaults to an em dash.</summary>
    public string EmptyText { get; init; } = "—";
}

/// <summary>
/// Formats UTC instants as relative ages ("just now", "5m ago", "3h ago", "2d ago").
/// Consolidates the per-page helpers that previously duplicated these thresholds.
/// </summary>
public static class RelativeTimeFormatter
{
    /// <summary>
    /// Formats a nullable <see cref="DateTimeOffset"/>; null/default values render as
    /// <see cref="RelativeTimeOptions.EmptyText"/>.
    /// </summary>
    public static string Format(
        DateTimeOffset? value,
        RelativeTimeOptions? options = null,
        DateTimeOffset? now = null)
    {
        var effectiveOptions = options ?? RelativeTimeOptions.Default;
        if (value is null || value.Value == default || value.Value == DateTimeOffset.MinValue)
        {
            return effectiveOptions.EmptyText;
        }

        return Format(value.Value, effectiveOptions, now);
    }

    /// <summary>
    /// Formats a <see cref="DateTimeOffset"/> as a relative age.
    /// </summary>
    public static string Format(
        DateTimeOffset value,
        RelativeTimeOptions? options = null,
        DateTimeOffset? now = null)
    {
        var effectiveOptions = options ?? RelativeTimeOptions.Default;
        var elapsed = (now ?? DateTimeOffset.UtcNow) - value;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (effectiveOptions.IncludeSeconds && elapsed.TotalSeconds < 60)
        {
            return $"{(int)elapsed.TotalSeconds}s ago";
        }

        if (!effectiveOptions.IncludeSeconds && elapsed.TotalMinutes < 1)
        {
            return effectiveOptions.JustNowLabel;
        }

        if (elapsed.TotalMinutes < 60)
        {
            return $"{(int)elapsed.TotalMinutes}m ago";
        }

        if (elapsed.TotalHours < 24)
        {
            return effectiveOptions.IncludeRemainder
                ? $"{(int)elapsed.TotalHours}h {(int)elapsed.Minutes % 60}m ago"
                : $"{(int)elapsed.TotalHours}h ago";
        }

        if (elapsed.TotalDays < effectiveOptions.DaysBeforeAbsolute)
        {
            return effectiveOptions.IncludeRemainder
                ? $"{(int)elapsed.TotalDays}d {(int)elapsed.TotalHours % 24}h ago"
                : $"{(int)elapsed.TotalDays}d ago";
        }

        return effectiveOptions.AbsoluteFormat?.Invoke(value)
            ?? $"{(int)elapsed.TotalDays}d ago";
    }

    /// <summary>
    /// Formats a nullable UTC <see cref="DateTime"/>; null values render as
    /// <see cref="RelativeTimeOptions.EmptyText"/>.
    /// </summary>
    public static string Format(
        DateTime? utc,
        RelativeTimeOptions? options = null,
        DateTime? nowUtc = null)
    {
        var effectiveOptions = options ?? RelativeTimeOptions.Default;
        return utc.HasValue ? Format(utc.Value, effectiveOptions, nowUtc) : effectiveOptions.EmptyText;
    }

    /// <summary>
    /// Formats a UTC <see cref="DateTime"/> as a relative age.
    /// </summary>
    public static string Format(
        DateTime utc,
        RelativeTimeOptions? options = null,
        DateTime? nowUtc = null)
        => Format(ToOffset(utc), options, nowUtc.HasValue ? ToOffset(nowUtc.Value) : null);

    private static DateTimeOffset ToOffset(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

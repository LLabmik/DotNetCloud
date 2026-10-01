namespace DotNetCloud.UI.Shared.Components.Dialogs;

/// <summary>
/// Formats share expiration dates for share listings — the Files "Shared with me" / "Shared by me"
/// views and the share dialog's "Current shares" list.
/// </summary>
/// <remarks>
/// A share without an expiration date is a first-class state in DotNetCloud (shares default to
/// never expiring), so the listings must say so explicitly instead of rendering nothing.
/// </remarks>
public static class ShareExpiryFormatter
{
    /// <summary>Label shown when a share has no expiration date.</summary>
    public const string NeverExpiresLabel = "Never expires";

    /// <summary>
    /// Formats a share's expiration as a complete label: <see cref="NeverExpiresLabel"/> when
    /// <paramref name="expiresAtUtc"/> is <see langword="null"/>, "Expired" when it is in the past,
    /// otherwise "Expires today", "Expires in 3 days", "Expires in 2 weeks", or "Expires on Oct 1, 2026".
    /// </summary>
    /// <param name="expiresAtUtc">Expiration instant (UTC), or <see langword="null"/> for a share that never expires.</param>
    /// <param name="nowUtc">Current instant (UTC) used as the relative-time baseline.</param>
    /// <returns>The label to display.</returns>
    public static string Format(DateTime? expiresAtUtc, DateTime nowUtc)
    {
        if (expiresAtUtc is not { } expiresAt)
        {
            return NeverExpiresLabel;
        }

        if (expiresAt < nowUtc)
        {
            return "Expired";
        }

        var relative = FormatRelative(expiresAt, nowUtc);
        return $"Expires {relative}";
    }

    /// <summary>
    /// Formats an expiration instant relative to <paramref name="nowUtc"/>: "expired", "today",
    /// "tomorrow", "in N days", "in N weeks", or "on MMM d, yyyy" once it is more than a month away.
    /// </summary>
    /// <param name="expiresAtUtc">Expiration instant (UTC).</param>
    /// <param name="nowUtc">Current instant (UTC) used as the relative-time baseline.</param>
    /// <returns>The relative (or absolute) expiration text.</returns>
    public static string FormatRelative(DateTime expiresAtUtc, DateTime nowUtc)
    {
        var diff = expiresAtUtc - nowUtc;
        if (diff.TotalDays < 0)
        {
            return "expired";
        }

        if (diff.TotalDays < 1)
        {
            return "today";
        }

        if (diff.TotalDays < 2)
        {
            return "tomorrow";
        }

        if (diff.TotalDays < 7)
        {
            return $"in {(int)diff.TotalDays} days";
        }

        if (diff.TotalDays < 30)
        {
            var weeks = (int)(diff.TotalDays / 7);
            return weeks == 1 ? "in 1 week" : $"in {weeks} weeks";
        }

        return $"on {expiresAtUtc:MMM d, yyyy}";
    }
}

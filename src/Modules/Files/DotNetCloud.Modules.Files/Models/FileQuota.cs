namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Represents a storage quota for a user.
/// Quotas limit total storage usage and can be configured per-user by administrators.
/// </summary>
public sealed class FileQuota
{
    /// <summary>Unique identifier for this quota record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User this quota applies to.</summary>
    public Guid UserId { get; set; }

    /// <summary>Maximum allowed storage in bytes. 0 = unlimited.</summary>
    public long MaxBytes { get; set; }

    /// <summary>Current storage used in bytes.</summary>
    [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
    public long UsedBytes { get; set; }

    /// <summary>When the quota was last recalculated (UTC).</summary>
    public DateTime LastCalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the quota record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the quota was last updated (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Percentage of quota used (0.0 to 100.0+).</summary>
    public double UsagePercent => MaxBytes > 0 ? (double)UsedBytes / MaxBytes * 100.0 : 0.0;

    /// <summary>Remaining bytes available.</summary>
    public long RemainingBytes => MaxBytes > 0 ? Math.Max(0, MaxBytes - UsedBytes) : long.MaxValue;
}

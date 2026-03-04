using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Files.Events;

/// <summary>
/// Published when a user's storage usage crosses the critical threshold (default 95%).
/// </summary>
public sealed record QuotaCriticalEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The user whose quota crossed the critical threshold.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Current used storage in bytes.</summary>
    public long UsedBytes { get; init; }

    /// <summary>Maximum storage in bytes (0 = unlimited).</summary>
    public long MaxBytes { get; init; }

    /// <summary>Current usage as a percentage of the quota.</summary>
    public double UsagePercent { get; init; }
}

namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Tracks the last acknowledged sync sequence per device, enabling server-side cursor recovery
/// and admin visibility into per-device sync lag.
/// </summary>
public sealed class SyncDeviceCursor
{
    /// <summary>Device that owns this cursor (FK → <see cref="SyncDevice"/>).</summary>
    public Guid DeviceId { get; set; }

    /// <summary>User that owns the device (FK → Users).</summary>
    public Guid UserId { get; set; }

    /// <summary>The last sync sequence the device acknowledged processing.</summary>
    public long LastAcknowledgedSequence { get; set; }

    /// <summary>When the cursor was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the owning device.</summary>
    public SyncDevice? Device { get; set; }
}

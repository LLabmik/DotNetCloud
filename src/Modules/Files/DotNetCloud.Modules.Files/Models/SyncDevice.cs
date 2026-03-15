namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Represents a registered sync device for a user.
/// Each physical device (desktop, phone, etc.) gets a unique entry, enabling
/// echo suppression, per-device rate limiting, and audit trails.
/// </summary>
public sealed class SyncDevice
{
    /// <summary>Unique device identifier (client-generated GUID, stable across sessions).</summary>
    public Guid Id { get; set; }

    /// <summary>The user who owns this device.</summary>
    public Guid UserId { get; set; }

    /// <summary>Human-readable device name (e.g., "benk-desktop-linux"), typically the hostname.</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Operating system platform (e.g., "Linux", "Windows", "Android").</summary>
    public string? Platform { get; set; }

    /// <summary>Client application version (e.g., "0.1.0-alpha").</summary>
    public string? ClientVersion { get; set; }

    /// <summary>When this device was first seen (UTC).</summary>
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>When this device last contacted the server (UTC).</summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether this device is active (false = admin-disabled).</summary>
    public bool IsActive { get; set; } = true;
}

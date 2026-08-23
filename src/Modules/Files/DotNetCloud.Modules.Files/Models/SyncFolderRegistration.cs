namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Represents a user-registered sync folder on the server.
/// Each entry maps a user's sync target to a remote folder node (<see cref="FileNode.Id"/>),
/// enabling cross-device visibility and admin auditing of which remote folders are actively
/// synced by clients.
/// </summary>
/// <remarks>
/// Only the remote registration is stored server-side; the local path on each device stays
/// device-local in the client's <c>contexts.json</c>.
/// </remarks>
public sealed class SyncFolderRegistration
{
    /// <summary>Unique identifier (server-generated GUID v7).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The user who owns this registration.</summary>
    public Guid UserId { get; set; }

    /// <summary>The <see cref="FileNode.Id"/> of the chosen remote folder (the sync target).</summary>
    public Guid RemoteFolderNodeId { get; set; }

    /// <summary>Denormalized human-readable path of the remote folder at registration time (e.g. "/Documents/Work").</summary>
    public string RemoteFolderPath { get; set; } = string.Empty;

    /// <summary>When the registration was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the registration was last updated (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether this registration is active (false = removed).</summary>
    public bool IsActive { get; set; } = true;
}

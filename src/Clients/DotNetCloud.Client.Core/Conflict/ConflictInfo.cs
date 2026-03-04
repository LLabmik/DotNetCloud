namespace DotNetCloud.Client.Core.Conflict;

/// <summary>
/// Describes a conflict between a local file and its remote counterpart.
/// </summary>
public sealed class ConflictInfo
{
    /// <summary>Full local file path of the conflicting file.</summary>
    public required string LocalPath { get; init; }

    /// <summary>Server node ID of the conflicting file.</summary>
    public required Guid NodeId { get; init; }

    /// <summary>Timestamp of the remote version.</summary>
    public DateTime RemoteUpdatedAt { get; init; }

    /// <summary>Content hash of the remote version.</summary>
    public string? RemoteContentHash { get; init; }
}

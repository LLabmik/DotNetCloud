namespace DotNetCloud.Core.Data.Entities.Admin;

/// <summary>
/// Records that a user dismissed an <see cref="AdminBroadcast"/>, so the modal is
/// never shown to that user again.
/// </summary>
/// <remarks>
/// Rows are cascade-deleted when the parent broadcast is deleted — a dismissed
/// broadcast can never be re-shown, so the flags are dead data once it is gone.
/// </remarks>
public sealed class AdminBroadcastDismissal
{
    /// <summary>Unique identifier for this dismissal record.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The broadcast that was dismissed.</summary>
    public Guid BroadcastId { get; set; }

    /// <summary>The user who dismissed the broadcast.</summary>
    public Guid UserId { get; set; }

    /// <summary>When the user dismissed the broadcast (UTC).</summary>
    public DateTime DismissedAtUtc { get; set; } = DateTime.UtcNow;
}

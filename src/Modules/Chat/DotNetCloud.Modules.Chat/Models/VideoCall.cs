namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents a video/audio call within a chat channel.
/// </summary>
public sealed class VideoCall
{
    /// <summary>Unique identifier for this call.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Channel this call belongs to (FK → Channel).</summary>
    public Guid ChannelId { get; set; }

    /// <summary>Navigation property to the channel.</summary>
    public Channel? Channel { get; set; }

    /// <summary>User who initiated the call.</summary>
    public Guid InitiatorUserId { get; set; }

    /// <summary>Current state of the call.</summary>
    public VideoCallState State { get; set; } = VideoCallState.Ringing;

    /// <summary>Type of media for this call.</summary>
    public CallMediaType MediaType { get; set; } = CallMediaType.Video;

    /// <summary>When the call started (first participant connected), UTC.</summary>
    public DateTime? StartedAtUtc { get; set; }

    /// <summary>When the call ended, UTC.</summary>
    public DateTime? EndedAtUtc { get; set; }

    /// <summary>Reason the call ended.</summary>
    public VideoCallEndReason? EndReason { get; set; }

    /// <summary>Maximum number of simultaneous participants during this call.</summary>
    public int MaxParticipants { get; set; }

    /// <summary>Whether this is a group call (more than two participants invited).</summary>
    public bool IsGroupCall { get; set; }

    /// <summary>LiveKit room identifier for SFU-based calls (null for P2P).</summary>
    public string? LiveKitRoomId { get; set; }

    /// <summary>When the call record was created, UTC.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the call record is soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the call record was soft-deleted, UTC.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Participants in this call.</summary>
    public ICollection<CallParticipant> Participants { get; set; } = [];
}

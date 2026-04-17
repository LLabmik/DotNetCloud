namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents a participant in a video call.
/// </summary>
public sealed class CallParticipant
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Video call this participant belongs to (FK → VideoCall).</summary>
    public Guid VideoCallId { get; set; }

    /// <summary>Navigation property to the video call.</summary>
    public VideoCall? VideoCall { get; set; }

    /// <summary>User ID of the participant.</summary>
    public Guid UserId { get; set; }

    /// <summary>Role of the participant (Host or Participant).</summary>
    public CallParticipantRole Role { get; set; } = CallParticipantRole.Participant;

    /// <summary>When the participant joined the call, UTC.</summary>
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When the participant left the call, UTC. Null if still in call.</summary>
    public DateTime? LeftAtUtc { get; set; }

    /// <summary>Whether the participant currently has audio enabled.</summary>
    public bool HasAudio { get; set; } = true;

    /// <summary>Whether the participant currently has video enabled.</summary>
    public bool HasVideo { get; set; }

    /// <summary>Whether the participant is currently screen sharing.</summary>
    public bool HasScreenShare { get; set; }
}

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Service for managing LiveKit SFU rooms for video calls with 4+ participants.
/// When LiveKit is not configured, use <see cref="NullLiveKitService"/> for graceful degradation.
/// </summary>
public interface ILiveKitService
{
    /// <summary>
    /// Gets whether LiveKit is available and configured.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the maximum number of participants allowed in a P2P call (without LiveKit).
    /// When LiveKit is not available, calls are limited to this number.
    /// </summary>
    int MaxP2PParticipants { get; }

    /// <summary>
    /// Creates a new LiveKit room for a video call.
    /// </summary>
    /// <param name="callId">The video call ID (used as room name).</param>
    /// <param name="maxParticipants">Maximum number of participants in the room.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The LiveKit room ID/name.</returns>
    Task<string> CreateRoomAsync(Guid callId, int maxParticipants, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a LiveKit access token for a participant to join a room.
    /// </summary>
    /// <param name="roomName">The room name (typically the call ID).</param>
    /// <param name="participantIdentity">The unique identity of the participant (user ID).</param>
    /// <param name="participantName">Display name of the participant.</param>
    /// <param name="canPublish">Whether the participant can publish media tracks.</param>
    /// <param name="canSubscribe">Whether the participant can subscribe to other tracks.</param>
    /// <returns>A JWT access token for the participant.</returns>
    string GenerateToken(string roomName, string participantIdentity, string participantName, bool canPublish = true, bool canSubscribe = true);

    /// <summary>
    /// Deletes a LiveKit room, disconnecting all participants.
    /// </summary>
    /// <param name="roomName">The room name to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteRoomAsync(string roomName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of participant identities currently in a room.
    /// </summary>
    /// <param name="roomName">The room name to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of participant identities in the room.</returns>
    Task<IReadOnlyList<string>> GetRoomParticipantsAsync(string roomName, CancellationToken cancellationToken = default);
}

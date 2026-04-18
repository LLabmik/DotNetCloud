using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Null-object implementation of <see cref="ILiveKitService"/> used when LiveKit is not configured.
/// Gracefully degrades by limiting calls to P2P mesh (max 3 participants) and rejecting
/// any SFU operations with descriptive exceptions.
/// </summary>
public sealed class NullLiveKitService : ILiveKitService
{
    private readonly ILogger<NullLiveKitService> _logger;
    private readonly int _maxP2PParticipants;

    /// <summary>
    /// Initializes a new instance of <see cref="NullLiveKitService"/>.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="maxP2PParticipants">Maximum participants in P2P mode. Defaults to 3.</param>
    public NullLiveKitService(ILogger<NullLiveKitService> logger, int maxP2PParticipants = 3)
    {
        _logger = logger;
        _maxP2PParticipants = maxP2PParticipants;
    }

    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public int MaxP2PParticipants => _maxP2PParticipants;

    /// <inheritdoc />
    public Task<string> CreateRoomAsync(Guid callId, int maxParticipants, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "LiveKit is not configured. Cannot create room for call {CallId}. Calls are limited to {MaxP2P} participants (P2P mesh).",
            callId, _maxP2PParticipants);

        throw new InvalidOperationException(
            $"LiveKit is not configured. Video calls are limited to {_maxP2PParticipants} participants. " +
            "Configure LiveKit in appsettings.json under Chat:LiveKit to enable larger calls.");
    }

    /// <inheritdoc />
    public string GenerateToken(string roomName, string participantIdentity, string participantName, bool canPublish = true, bool canSubscribe = true)
    {
        _logger.LogWarning(
            "LiveKit is not configured. Cannot generate token for room {RoomName}, participant {ParticipantIdentity}.",
            roomName, participantIdentity);

        throw new InvalidOperationException(
            "LiveKit is not configured. Cannot generate participant tokens without a LiveKit server.");
    }

    /// <inheritdoc />
    public Task DeleteRoomAsync(string roomName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LiveKit is not configured. Ignoring delete request for room {RoomName}.", roomName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetRoomParticipantsAsync(string roomName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LiveKit is not configured. Returning empty participants for room {RoomName}.", roomName);
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}

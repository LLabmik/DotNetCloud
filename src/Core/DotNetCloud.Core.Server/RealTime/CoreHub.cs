using System.Security.Claims;
using DotNetCloud.Core.Server.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// The primary SignalR hub for the DotNetCloud platform.
/// Manages real-time client connections, presence tracking, and group membership.
/// All clients must be authenticated to connect.
/// </summary>
[Authorize]
internal sealed class CoreHub : Hub
{
    private readonly UserConnectionTracker _connectionTracker;
    private readonly PresenceService _presenceService;
    private readonly ILogger<CoreHub> _logger;

    public CoreHub(
        UserConnectionTracker connectionTracker,
        PresenceService presenceService,
        ILogger<CoreHub> logger)
    {
        _connectionTracker = connectionTracker;
        _presenceService = presenceService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;

        var isFirstConnection = _connectionTracker.AddConnection(userId, connectionId);

        _logger.LogInformation(
            "User {UserId} connected with connection {ConnectionId} (first: {IsFirst})",
            userId, connectionId, isFirstConnection);

        if (isFirstConnection)
        {
            await _presenceService.UserConnectedAsync(userId, connectionId);

            // Notify other clients that this user is now online
            await Clients.Others.SendAsync("UserOnline", new { UserId = userId, Timestamp = DateTime.UtcNow });
        }

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var result = _connectionTracker.RemoveConnection(connectionId);

        if (result is not null)
        {
            var (userId, isLastConnection) = result.Value;

            _logger.LogInformation(
                "User {UserId} disconnected connection {ConnectionId} (last: {IsLast})",
                userId, connectionId, isLastConnection);

            if (isLastConnection)
            {
                await _presenceService.UserDisconnectedAsync(userId, connectionId);

                // Notify other clients that this user is now offline
                await Clients.Others.SendAsync("UserOffline", new { UserId = userId, Timestamp = DateTime.UtcNow });
            }
        }

        if (exception is not null)
        {
            _logger.LogWarning(exception, "Connection {ConnectionId} disconnected with error", connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Joins the calling user to a broadcast group (e.g., a chat channel or room).
    /// </summary>
    /// <param name="groupName">The group to join.</param>
    public async Task JoinGroupAsync(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new HubException("Group name cannot be empty.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug(
            "User {UserId} joined group {Group} via connection {ConnectionId}",
            GetUserId(), groupName, Context.ConnectionId);
    }

    /// <summary>
    /// Removes the calling user from a broadcast group.
    /// </summary>
    /// <param name="groupName">The group to leave.</param>
    public async Task LeaveGroupAsync(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new HubException("Group name cannot be empty.");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug(
            "User {UserId} left group {Group} via connection {ConnectionId}",
            GetUserId(), groupName, Context.ConnectionId);
    }

    /// <summary>
    /// Pings the server to keep the connection alive and update presence.
    /// Clients can call this periodically to signal activity.
    /// </summary>
    public async Task PingAsync()
    {
        var userId = GetUserId();
        await _presenceService.UpdateLastSeenAsync(userId);
    }

    private Guid GetUserId()
    {
        var nameIdentifier = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(nameIdentifier) || !Guid.TryParse(nameIdentifier, out var userId))
        {
            throw new HubException("Unable to determine authenticated user identity.");
        }

        return userId;
    }
}

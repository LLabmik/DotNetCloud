using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Scoped circuit handler that tracks Blazor Server circuit connections for presence.
/// Each Blazor circuit gets its own instance. When the circuit opens, the authenticated
/// user is registered with the connection tracker so they appear online.
/// </summary>
internal sealed class PresenceCircuitHandler : CircuitHandler
{
    private readonly UserConnectionTracker _connectionTracker;
    private readonly PresenceService _presenceService;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IChatMessageNotifier? _chatMessageNotifier;
    private readonly ILogger<PresenceCircuitHandler> _logger;

    private string? _connectionId;
    private Guid _userId;

    public PresenceCircuitHandler(
        UserConnectionTracker connectionTracker,
        PresenceService presenceService,
        AuthenticationStateProvider authStateProvider,
        ILogger<PresenceCircuitHandler> logger,
        IChatMessageNotifier? chatMessageNotifier = null)
    {
        _connectionTracker = connectionTracker;
        _presenceService = presenceService;
        _authStateProvider = authStateProvider;
        _chatMessageNotifier = chatMessageNotifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                _userId = userId;
                _connectionId = $"blazor-{circuit.Id}";

                var isFirstConnection = _connectionTracker.AddConnection(userId, _connectionId);

                _logger.LogInformation(
                    "Blazor circuit opened for user {UserId} (circuit: {CircuitId}, first: {IsFirst})",
                    userId, circuit.Id, isFirstConnection);

                if (isFirstConnection)
                {
                    await _presenceService.UserConnectedAsync(userId, _connectionId);
                    _chatMessageNotifier?.NotifyUserPresenceChanged(
                        new UserPresenceChangedNotification(userId, IsOnline: true));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register Blazor circuit for presence tracking");
        }

        await base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (_connectionId is not null)
        {
            var result = _connectionTracker.RemoveConnection(_connectionId);

            if (result is not null)
            {
                var (userId, isLastConnection) = result.Value;

                _logger.LogInformation(
                    "Blazor circuit closed for user {UserId} (circuit: {CircuitId}, last: {IsLast})",
                    userId, circuit.Id, isLastConnection);

                if (isLastConnection)
                {
                    await _presenceService.UserDisconnectedAsync(userId, _connectionId);
                    _chatMessageNotifier?.NotifyUserPresenceChanged(
                        new UserPresenceChangedNotification(userId, IsOnline: false));
                }
            }
        }

        await base.OnCircuitClosedAsync(circuit, cancellationToken);
    }
}

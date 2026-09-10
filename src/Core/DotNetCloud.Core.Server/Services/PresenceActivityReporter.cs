using System.Security.Claims;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Scoped (per-circuit) implementation of <see cref="IPresenceActivityReporter"/> that resolves
/// the circuit's signed-in user and reports their activity to the singleton
/// <see cref="PresenceService"/>.
/// </summary>
internal sealed class PresenceActivityReporter : IPresenceActivityReporter
{
    private readonly PresenceService _presenceService;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<PresenceActivityReporter> _logger;

    public PresenceActivityReporter(
        PresenceService presenceService,
        AuthenticationStateProvider authStateProvider,
        ILogger<PresenceActivityReporter> logger)
    {
        _presenceService = presenceService;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ReportActivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var userId = ResolveUserId(authState.User);
            if (userId is null)
            {
                return;
            }

            await _presenceService.ReportActivityAsync(userId.Value);
        }
        catch (OperationCanceledException)
        {
            // Expected during circuit shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to report web activity for presence");
        }
    }

    private static Guid? ResolveUserId(ClaimsPrincipal user)
    {
        var claimValue = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}

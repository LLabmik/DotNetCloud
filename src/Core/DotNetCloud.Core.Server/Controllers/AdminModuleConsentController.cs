using System.Security.Claims;
using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Administrator view of the consent plane and the account-erasure queue.
/// </summary>
/// <remarks>
/// Installing a module is an administrator decision; giving it a user's personal data is not. These
/// endpoints let an administrator see how many users approved what, revoke on a user's behalf (for
/// example after a support request), and follow up on erasures a module never acknowledged. Every
/// write is audited.
/// </remarks>
[ApiController]
[Route("api/v1/core/admin")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public class AdminModuleConsentController : ControllerBase
{
    private readonly IModuleConsentService _consent;
    private readonly IUserErasureService _erasure;
    private readonly IAuditLogger _audit;
    private readonly ILogger<AdminModuleConsentController> _logger;

    /// <summary>Initializes a new instance of the <see cref="AdminModuleConsentController"/> class.</summary>
    /// <param name="consent">Consent store.</param>
    /// <param name="erasure">Erasure orchestrator.</param>
    /// <param name="audit">Audit logger.</param>
    /// <param name="logger">Logger.</param>
    public AdminModuleConsentController(
        IModuleConsentService consent,
        IUserErasureService erasure,
        IAuditLogger audit,
        ILogger<AdminModuleConsentController> logger)
    {
        _consent = consent;
        _erasure = erasure;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>Gets the consent overview for one module.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-scope user counts and the list of consenting users.</returns>
    [HttpGet("modules/{moduleId}/consent")]
    public async Task<IActionResult> GetConsentOverviewAsync(
        string moduleId,
        CancellationToken cancellationToken)
    {
        var overview = await _consent.GetOverviewAsync(moduleId, cancellationToken);
        var users = await _consent.ListForModuleAsync(moduleId, cancellationToken);

        var dto = new ModuleConsentOverviewDto
        {
            ModuleId = moduleId,
            ConsentingUsers = overview.ConsentingUsers,
            Scopes = overview.ScopeCounts
                .Select(count => new ModuleScopeConsentDto
                {
                    Scope = count.Scope,
                    Description = ModuleScopes.Describe(count.Scope),
                    UserCount = count.UserCount,
                })
                .ToArray(),
            Users = users
                .Select(user => new ModuleConsentUserDto
                {
                    UserId = user.UserId,
                    Scopes = user.Scopes,
                    GrantedAtUtc = user.GrantedAtUtc,
                    GrantedByAdmin = user.GrantedByAdmin,
                })
                .ToArray(),
        };

        return Ok(new { success = true, data = dto });
    }

    /// <summary>Revokes every scope one user granted to a module.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many records were removed.</returns>
    [HttpDelete("modules/{moduleId}/consent/{userId:guid}")]
    public async Task<IActionResult> RevokeConsentAsync(
        string moduleId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetAdminUserId();
        var removed = await _consent.RevokeAllAsync(moduleId, userId, adminUserId, cancellationToken);

        if (removed > 0)
        {
            await _audit.LogAsync(
                new AuditEntry
                {
                    Caller = CallerContext.CreateSystemContext(),
                    ModuleId = moduleId,
                    Action = AuditAction.ModuleConsentRevoked,
                    EntityType = "ModuleUserConsent",
                    EntityId = userId,
                    Description = $"Administrator {adminUserId} revoked {removed} consent scope(s).",
                },
                cancellationToken);
        }

        _logger.LogInformation(
            "Administrator {AdminUserId} revoked {Count} consent scope(s) of user {UserId} for module {ModuleId}",
            adminUserId,
            removed,
            userId,
            LogSanitizer.Sanitize(moduleId));

        return Ok(new { success = true, data = new { revoked = removed } });
    }

    /// <summary>Lists account-erasure requests.</summary>
    /// <param name="includeCompleted">Whether terminal requests are included.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The requests, newest first.</returns>
    [HttpGet("erasure")]
    public async Task<IActionResult> ListErasuresAsync(
        [FromQuery] bool includeCompleted,
        CancellationToken cancellationToken)
    {
        var requests = await _erasure.ListAsync(includeCompleted, cancellationToken);
        return Ok(new { success = true, data = requests });
    }

    /// <summary>
    /// Force-completes an erasure a module never acknowledged. Audited.
    /// </summary>
    /// <param name="requestId">The erasure request id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the request was overridden.</returns>
    [HttpPost("erasure/{requestId:guid}/override")]
    public async Task<IActionResult> OverrideErasureAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetAdminUserId();
        var overridden = await _erasure.OverrideAsync(requestId, adminUserId, cancellationToken);

        if (!overridden)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "ERASURE_NOT_FOUND", message = "No outstanding erasure request with that id." },
            });
        }

        return Ok(new { success = true, data = new { overridden } });
    }

    private Guid GetAdminUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }
}

using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// The signed-in user's own consent records ("Connected apps").
/// </summary>
/// <remarks>
/// Consent is the user's decision, so these endpoints are scoped to the caller: there is no user id
/// in the route, and a user can only ever list or revoke their own grants. An administrator acting
/// on someone's behalf uses the admin endpoints, which are audited separately.
/// </remarks>
[ApiController]
[Route("api/v1/core/account/consents")]
[Authorize]
public class AccountConsentController : ControllerBase
{
    private readonly IModuleConsentService _consent;
    private readonly IConsentScopeRegistry _manifests;
    private readonly IAuditLogger _audit;
    private readonly ILogger<AccountConsentController> _logger;

    /// <summary>Initializes a new instance of the <see cref="AccountConsentController"/> class.</summary>
    /// <param name="consent">Consent store.</param>
    /// <param name="manifests">Registry of the manifests the supervisor loaded.</param>
    /// <param name="audit">Audit logger.</param>
    /// <param name="logger">Logger.</param>
    public AccountConsentController(
        IModuleConsentService consent,
        IConsentScopeRegistry manifests,
        IAuditLogger audit,
        ILogger<AccountConsentController> logger)
    {
        _consent = consent;
        _manifests = manifests;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>Lists the modules the signed-in user has approved.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connected apps.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var summaries = await _consent.ListForUserAsync(userId, cancellationToken);

        var apps = summaries.Select(summary => new ConnectedAppDto
        {
            ModuleId = summary.ModuleId,
            ModuleName = summary.ModuleName,
            GrantedAtUtc = summary.GrantedAtUtc,
            IsModuleInstalled = summary.IsModuleInstalled,
            Scopes = summary.Scopes
                .Select(scope => new ConsentScopeDto(scope, ModuleScopes.Describe(scope)))
                .ToArray(),
        }).ToArray();

        return Ok(new { success = true, data = apps });
    }

    /// <summary>Revokes every scope the signed-in user granted to one module.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many scopes were revoked.</returns>
    [HttpDelete("{moduleId}")]
    public async Task<IActionResult> RevokeModuleAsync(string moduleId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var removed = await _consent.RevokeAllAsync(moduleId, userId, revokedByUserId: null, cancellationToken);

        await AuditAsync(moduleId, userId, removed, "the user");

        _logger.LogInformation(
            "User {UserId} revoked {Count} consent scope(s) for module {ModuleId}",
            userId,
            removed,
            LogSanitizer.Sanitize(moduleId));

        return Ok(new { success = true, data = new { revoked = removed } });
    }

    /// <summary>Revokes a single scope for the signed-in user.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="scope">The scope to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many records were removed.</returns>
    [HttpDelete("{moduleId}/{scope}")]
    public async Task<IActionResult> RevokeScopeAsync(
        string moduleId,
        string scope,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var removed = await _consent.RevokeAsync(moduleId, userId, scope, revokedByUserId: null, cancellationToken);

        await AuditAsync(moduleId, userId, removed, "the user");

        return Ok(new { success = true, data = new { revoked = removed } });
    }

    /// <summary>
    /// Approves the scopes a module asked the user for through its in-app consent prompt.
    /// </summary>
    /// <remarks>
    /// The module cannot grant itself anything: the request is validated against the manifest the
    /// supervisor loaded, so only scopes the module actually declared can be approved, and only
    /// scopes from the core's consent catalog are written.
    /// </remarks>
    /// <param name="moduleId">The module id.</param>
    /// <param name="request">The scopes to approve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The approved scopes.</returns>
    [HttpPost("{moduleId}")]
    public async Task<IActionResult> GrantModuleAsync(
        string moduleId,
        [FromBody] GrantModuleConsentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var requested = (request.Scopes ?? [])
            .Where(scope => !string.IsNullOrWhiteSpace(scope) && ModuleScopes.IsConsentScope(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (requested.Length == 0)
        {
            return BadRequest(new { success = false, error = "NO_VALID_SCOPES" });
        }

        var declared = _manifests.GetUserConsentScopes(moduleId);
        if (declared is null)
        {
            // Not installed, not running, or not a sandboxed module: there is nothing to approve.
            return NotFound(new { success = false, error = "MODULE_NOT_FOUND" });
        }

        var undeclared = requested
            .Where(scope => !declared.Contains(scope, StringComparer.Ordinal))
            .ToArray();

        if (undeclared.Length > 0)
        {
            _logger.LogWarning(
                "Refused consent request from user {UserId} for undeclared scope(s) {Scopes} of module {ModuleId}",
                userId,
                LogSanitizer.Sanitize(string.Join(",", undeclared)),
                LogSanitizer.Sanitize(moduleId));

            return BadRequest(new { success = false, error = "UNDECLARED_SCOPE", scopes = undeclared });
        }

        await _consent.GrantAsync(moduleId, userId, requested, grantedByUserId: null, cancellationToken: cancellationToken);

        await _audit.LogAsync(new AuditEntry
        {
            Caller = new CallerContext(
                userId,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
                CallerType.User),
            ModuleId = moduleId,
            Action = AuditAction.ModuleConsentGranted,
            EntityType = "ModuleUserConsent",
            EntityId = userId,
            Description = $"The user approved {requested.Length} consent scope(s): {string.Join(",", requested)}.",
        });

        _logger.LogInformation(
            "User {UserId} approved {Count} consent scope(s) for module {ModuleId}",
            userId,
            requested.Length,
            LogSanitizer.Sanitize(moduleId));

        return Ok(new { success = true, data = new { scopes = requested } });
    }

    private async Task AuditAsync(string moduleId, Guid userId, int removed, string actor)
    {
        if (removed == 0)
        {
            return;
        }

        await _audit.LogAsync(new AuditEntry
        {
            Caller = new CallerContext(
                userId,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
                CallerType.User),
            ModuleId = moduleId,
            Action = AuditAction.ModuleConsentRevoked,
            EntityType = "ModuleUserConsent",
            EntityId = userId,
            Description = $"{actor} revoked {removed} consent scope(s).",
        });
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }
}

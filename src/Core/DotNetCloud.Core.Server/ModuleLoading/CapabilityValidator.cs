using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.ModuleLoading;

/// <summary>
/// Result of capability validation for a module.
/// </summary>
internal sealed record CapabilityValidationResult
{
    /// <summary>Whether all required capabilities are valid and can be granted.</summary>
    public bool IsValid { get; init; }

    /// <summary>Capabilities that are granted (Public tier auto-granted + approved Restricted/Privileged).</summary>
    public IReadOnlyList<string> GrantedCapabilities { get; init; } = [];

    /// <summary>Capabilities that are pending approval.</summary>
    public IReadOnlyList<string> PendingCapabilities { get; init; } = [];

    /// <summary>Capabilities that are forbidden and will never be granted.</summary>
    public IReadOnlyList<string> ForbiddenCapabilities { get; init; } = [];

    /// <summary>Capabilities that are unknown (not defined in the system).</summary>
    public IReadOnlyList<string> UnknownCapabilities { get; init; } = [];

    /// <summary>Validation error messages.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Whether the module can start (all required capabilities are granted).</summary>
    public bool CanStart => IsValid && PendingCapabilities.Count == 0 && ForbiddenCapabilities.Count == 0;

    public static CapabilityValidationResult Success(
        IReadOnlyList<string> granted,
        IReadOnlyList<string> pending,
        IReadOnlyList<string> forbidden,
        IReadOnlyList<string> unknown)
        => new()
        {
            IsValid = forbidden.Count == 0 && unknown.Count == 0,
            GrantedCapabilities = granted,
            PendingCapabilities = pending,
            ForbiddenCapabilities = forbidden,
            UnknownCapabilities = unknown
        };

    public static CapabilityValidationResult Failure(params string[] errors)
        => new() { IsValid = false, Errors = errors };
}

/// <summary>
/// Validates module capability requests against the capability tier system
/// and checks database grants for Restricted/Privileged capabilities.
/// </summary>
internal sealed class CapabilityValidator
{
    private readonly ILogger<CapabilityValidator> _logger;
    private readonly CoreDbContext _dbContext;

    // Known capability interfaces organized by tier
    private static readonly HashSet<string> PublicCapabilities =
    [
        nameof(IUserDirectory),
        nameof(ICurrentUserContext),
        nameof(INotificationService),
        nameof(IEventBus)
    ];

    private static readonly HashSet<string> RestrictedCapabilities =
    [
        nameof(IStorageProvider),
        nameof(IModuleSettings),
        nameof(ITeamDirectory),
        nameof(IOrganizationDirectory)
    ];

    private static readonly HashSet<string> PrivilegedCapabilities =
    [
        nameof(IUserManager),
        nameof(IBackupProvider)
    ];

    private static readonly HashSet<string> ForbiddenCapabilities =
    [
        "CoreDbContext",
        "IConfiguration",
        "IServiceProvider",
        "PasswordHasher",
        "SigningKey"
    ];

    public CapabilityValidator(
        ILogger<CapabilityValidator> logger,
        CoreDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Validates that all requested capabilities are known and determines which are granted.
    /// Checks database for Restricted/Privileged grants.
    /// </summary>
    /// <param name="moduleId">The module requesting capabilities.</param>
    /// <param name="requestedCapabilities">The capability names from the module manifest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result with granted, pending, forbidden, and unknown capabilities.</returns>
    public async Task<CapabilityValidationResult> ValidateAsync(
        string moduleId,
        IReadOnlyList<string> requestedCapabilities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(requestedCapabilities);

        var granted = new List<string>();
        var pending = new List<string>();
        var forbidden = new List<string>();
        var unknown = new List<string>();

        // Load existing grants from database
        var existingGrants = await _dbContext.ModuleCapabilityGrants
            .Where(g => g.ModuleId == moduleId)
            .Select(g => g.CapabilityName)
            .ToListAsync(cancellationToken);

        var existingGrantsSet = new HashSet<string>(existingGrants, StringComparer.Ordinal);

        foreach (var capability in requestedCapabilities)
        {
            if (string.IsNullOrWhiteSpace(capability))
                continue;

            // Check if forbidden
            if (ForbiddenCapabilities.Contains(capability))
            {
                forbidden.Add(capability);
                _logger.LogWarning(
                    "Module {ModuleId} requested forbidden capability {Capability}",
                    moduleId, capability);
                continue;
            }

            // Check if public tier (auto-granted)
            if (PublicCapabilities.Contains(capability))
            {
                granted.Add(capability);
                continue;
            }

            // Check if restricted or privileged (requires database grant)
            if (RestrictedCapabilities.Contains(capability) || PrivilegedCapabilities.Contains(capability))
            {
                if (existingGrantsSet.Contains(capability))
                {
                    granted.Add(capability);
                }
                else
                {
                    pending.Add(capability);
                    _logger.LogInformation(
                        "Module {ModuleId} requested {Tier} capability {Capability} (pending approval)",
                        moduleId,
                        RestrictedCapabilities.Contains(capability) ? "Restricted" : "Privileged",
                        capability);
                }
                continue;
            }

            // Unknown capability
            unknown.Add(capability);
            _logger.LogWarning(
                "Module {ModuleId} requested unknown capability {Capability}",
                moduleId, capability);
        }

        var result = CapabilityValidationResult.Success(granted, pending, forbidden, unknown);

        _logger.LogInformation(
            "Capability validation for {ModuleId}: {Granted} granted, {Pending} pending, {Forbidden} forbidden, {Unknown} unknown",
            moduleId, granted.Count, pending.Count, forbidden.Count, unknown.Count);

        return result;
    }

    /// <summary>
    /// Gets the capability tier for a given capability name.
    /// </summary>
    /// <param name="capabilityName">The capability interface name.</param>
    /// <returns>The capability tier, or null if unknown.</returns>
    public static CapabilityTier? GetCapabilityTier(string capabilityName)
    {
        if (PublicCapabilities.Contains(capabilityName))
            return CapabilityTier.Public;

        if (RestrictedCapabilities.Contains(capabilityName))
            return CapabilityTier.Restricted;

        if (PrivilegedCapabilities.Contains(capabilityName))
            return CapabilityTier.Privileged;

        if (ForbiddenCapabilities.Contains(capabilityName))
            return CapabilityTier.Forbidden;

        return null;
    }

    /// <summary>
    /// Creates grants in the database for pending capabilities (admin approval simulation).
    /// In production, this would be done through an admin UI.
    /// </summary>
    /// <param name="moduleId">The module to grant capabilities to.</param>
    /// <param name="capabilityNames">The capability names to grant.</param>
    /// <param name="grantedByUserId">The user ID performing the grant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task GrantCapabilitiesAsync(
        string moduleId,
        IEnumerable<string> capabilityNames,
        Guid grantedByUserId,
        CancellationToken cancellationToken = default)
    {
        foreach (var capabilityName in capabilityNames)
        {
            // Check if already granted
            var exists = await _dbContext.ModuleCapabilityGrants
                .AnyAsync(g => g.ModuleId == moduleId && g.CapabilityName == capabilityName, cancellationToken);

            if (exists)
                continue;

            var grant = new Data.Entities.Modules.ModuleCapabilityGrant
            {
                ModuleId = moduleId,
                CapabilityName = capabilityName,
                GrantedAt = DateTime.UtcNow,
                GrantedByUserId = grantedByUserId
            };

            _dbContext.ModuleCapabilityGrants.Add(grant);

            _logger.LogInformation(
                "Granted capability {Capability} to module {ModuleId} by user {UserId}",
                capabilityName, moduleId, grantedByUserId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

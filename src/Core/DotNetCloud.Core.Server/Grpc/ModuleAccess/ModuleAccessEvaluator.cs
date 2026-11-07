using System.Collections.Concurrent;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.ModuleAccess;

/// <summary>
/// Evaluates whether an authenticated module may call a gRPC method.
/// </summary>
/// <remarks>
/// <para>
/// The evaluator owns the enforcement decision so it can be used by gRPC, the internal REST facade
/// and tests without going through an interceptor. The mapping itself lives in
/// <see cref="CapabilityCatalog"/> (shared with the SDK), keyed by the fully-qualified method path.
/// </para>
/// <para>
/// Grants are cached per module for a short TTL; grant/revoke operations must call
/// <see cref="Invalidate(string)"/> so a revocation takes effect immediately rather than after the
/// cache expires.
/// </para>
/// </remarks>
public interface IModuleAccessEvaluator
{
    /// <summary>
    /// Evaluates a call.
    /// </summary>
    /// <param name="moduleId">The verified module id from the authenticated transport.</param>
    /// <param name="rpcMethod">The fully-qualified gRPC method path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decision; never throws for a deny.</returns>
    Task<ModuleAccessDecision> EvaluateAsync(
        string? moduleId,
        string? rpcMethod,
        CancellationToken cancellationToken = default);

    /// <summary>Drops the cached grants for a module (grant or revoke).</summary>
    /// <param name="moduleId">The module whose cache entry should be removed.</param>
    void Invalidate(string moduleId);

    /// <summary>Drops every cached grant set.</summary>
    void InvalidateAll();
}

/// <summary>Why a call was denied.</summary>
public enum ModuleAccessDenialReason
{
    /// <summary>The module does not hold the capability grant.</summary>
    CapabilityNotGranted,

    /// <summary>The capability is unknown or forbidden, so it can never be granted.</summary>
    CapabilityForbidden,

    /// <summary>The RPC has no enforcement decision (mapping gap) and fails closed.</summary>
    UnmappedRpc,

    /// <summary>The call carried no verified module identity.</summary>
    UnknownModule,

    /// <summary>The grant lookup failed; the call fails closed.</summary>
    EvaluationFailed,
}

/// <summary>Outcome of a module access evaluation.</summary>
/// <param name="IsAllowed">Whether the call may proceed.</param>
/// <param name="Capability">The capability the method exercises, when it has one.</param>
/// <param name="DenialReason">Why the call was denied; <see langword="null"/> when allowed.</param>
public sealed record ModuleAccessDecision(
    bool IsAllowed,
    string? Capability,
    ModuleAccessDenialReason? DenialReason)
{
    /// <summary>Creates an allow decision.</summary>
    /// <param name="capability">The capability exercised, if any.</param>
    /// <returns>The decision.</returns>
    public static ModuleAccessDecision Allow(string? capability) => new(true, capability, null);

    /// <summary>Creates a deny decision.</summary>
    /// <param name="capability">The capability exercised, if any.</param>
    /// <param name="reason">Why the call is denied.</param>
    /// <returns>The decision.</returns>
    public static ModuleAccessDecision Deny(string? capability, ModuleAccessDenialReason reason) =>
        new(false, capability, reason);
}

/// <inheritdoc />
internal sealed class ModuleAccessEvaluator : IModuleAccessEvaluator
{
    /// <summary>How long a module's grant set is cached.</summary>
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ModuleAccessEvaluator> _logger;
    private readonly ConcurrentDictionary<string, GrantSnapshot> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a new instance of the <see cref="ModuleAccessEvaluator"/> class.</summary>
    /// <param name="scopeFactory">Scope factory used to read grants.</param>
    /// <param name="timeProvider">Clock used for cache expiry.</param>
    /// <param name="logger">Logger.</param>
    public ModuleAccessEvaluator(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<ModuleAccessEvaluator> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ModuleAccessDecision> EvaluateAsync(
        string? moduleId,
        string? rpcMethod,
        CancellationToken cancellationToken = default)
    {
        // The interceptor is registered on the module listener, which also hosts non-module
        // services (upload streaming, token introspection). Those authenticate differently and are
        // out of scope here.
        if (!CapabilityCatalog.IsModuleFacingRpc(rpcMethod))
        {
            return ModuleAccessDecision.Allow(null);
        }

        if (CapabilityCatalog.IsSelfServiceRpc(rpcMethod))
        {
            return ModuleAccessDecision.Allow(null);
        }

        var capability = CapabilityCatalog.ResolveCapability(rpcMethod);
        if (capability is null)
        {
            // Mapping gap: a module-facing RPC with no decision is denied rather than silently
            // allowed. Adding an RPC to a module-facing proto without a catalog entry fails closed.
            _logger.LogError(
                "Module-facing RPC {Method} has no capability mapping; denying",
                rpcMethod);
            return ModuleAccessDecision.Deny(null, ModuleAccessDenialReason.UnmappedRpc);
        }

        var tier = CapabilityCatalog.GetCapabilityTier(capability);
        if (tier is CapabilityTier.Public)
        {
            return ModuleAccessDecision.Allow(capability);
        }

        if (tier is null or CapabilityTier.Forbidden)
        {
            return ModuleAccessDecision.Deny(capability, ModuleAccessDenialReason.CapabilityForbidden);
        }

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return ModuleAccessDecision.Deny(capability, ModuleAccessDenialReason.UnknownModule);
        }

        HashSet<string> grants;
        try
        {
            grants = await GetGrantsAsync(moduleId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail closed: an unavailable grant store must not turn into a permission bypass.
            _logger.LogError(
                ex,
                "Could not read capability grants for module {ModuleId}; denying {Capability}",
                LogSanitizer.Sanitize(moduleId),
                capability);

            return ModuleAccessDecision.Deny(capability, ModuleAccessDenialReason.EvaluationFailed);
        }

        return grants.Contains(capability)
            ? ModuleAccessDecision.Allow(capability)
            : ModuleAccessDecision.Deny(capability, ModuleAccessDenialReason.CapabilityNotGranted);
    }

    /// <inheritdoc />
    public void Invalidate(string moduleId)
    {
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            _cache.TryRemove(moduleId, out _);
        }
    }

    /// <inheritdoc />
    public void InvalidateAll() => _cache.Clear();

    private async Task<HashSet<string>> GetGrantsAsync(string moduleId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        if (_cache.TryGetValue(moduleId, out var cached) && cached.ExpiresAtUtc > now)
        {
            return cached.Capabilities;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var granted = await dbContext.ModuleCapabilityGrants
            .AsNoTracking()
            .Where(grant => grant.ModuleId == moduleId)
            .Select(grant => grant.CapabilityName)
            .ToListAsync(cancellationToken);

        var capabilities = new HashSet<string>(granted, StringComparer.OrdinalIgnoreCase);
        _cache[moduleId] = new GrantSnapshot(capabilities, now + CacheTtl);
        return capabilities;
    }

    private sealed record GrantSnapshot(HashSet<string> Capabilities, DateTimeOffset ExpiresAtUtc);
}

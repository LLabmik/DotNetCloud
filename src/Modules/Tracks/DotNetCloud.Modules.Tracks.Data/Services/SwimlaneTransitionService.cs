using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Manages swimlane transition rules — which swimlanes can transition to which.
/// Supports both per-rule CRUD and move validation.
/// </summary>
public sealed class SwimlaneTransitionService
{
    private readonly TracksDbContext _db;

    public SwimlaneTransitionService(TracksDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns the full transition matrix for a product: a list of (FromSwimlaneId, ToSwimlaneId, IsAllowed) tuples.
    /// </summary>
    public async Task<List<SwimlaneTransitionRuleDto>> GetTransitionMatrixAsync(Guid productId, CancellationToken ct)
    {
        var rules = await _db.SwimlaneTransitionRules
            .Where(r => r.ProductId == productId)
            .ToListAsync(ct);

        return rules.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Sets the transition matrix for a product. Replaces all existing rules.
    /// </summary>
    public async Task<List<SwimlaneTransitionRuleDto>> SetTransitionMatrixAsync(
        Guid productId, List<SetTransitionRuleDto> rules, CancellationToken ct)
    {
        // Delete existing rules for this product
        var existing = await _db.SwimlaneTransitionRules
            .Where(r => r.ProductId == productId)
            .ToListAsync(ct);

        _db.SwimlaneTransitionRules.RemoveRange(existing);

        // Create new rules
        var newRules = new List<SwimlaneTransitionRule>();
        foreach (var dto in rules)
        {
            newRules.Add(new SwimlaneTransitionRule
            {
                ProductId = productId,
                FromSwimlaneId = dto.FromSwimlaneId,
                ToSwimlaneId = dto.ToSwimlaneId,
                IsAllowed = dto.IsAllowed
            });
        }

        _db.SwimlaneTransitionRules.AddRange(newRules);
        await _db.SaveChangesAsync(ct);

        return newRules.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Validates whether a work item can move from one swimlane to another.
    /// Returns true if the transition is allowed (or no rules exist — backward compatible).
    /// Returns false and populates allowedTargetIds if the transition is blocked.
    /// </summary>
    public async Task<(bool IsAllowed, List<Guid> AllowedTargetIds)> ValidateTransitionAsync(
        Guid productId, Guid fromSwimlaneId, Guid toSwimlaneId, CancellationToken ct)
    {
        // Check if any rules exist for this product
        var hasRules = await _db.SwimlaneTransitionRules
            .AnyAsync(r => r.ProductId == productId, ct);

        // No rules = all transitions allowed (backward compatible)
        if (!hasRules)
            return (true, []);

        // Check specific rule
        var rule = await _db.SwimlaneTransitionRules
            .FirstOrDefaultAsync(r =>
                r.ProductId == productId &&
                r.FromSwimlaneId == fromSwimlaneId &&
                r.ToSwimlaneId == toSwimlaneId, ct);

        if (rule is not null)
            return (rule.IsAllowed, rule.IsAllowed ? [toSwimlaneId] : []);

        // No explicit rule = not allowed when rules are configured
        var allowedTargets = await _db.SwimlaneTransitionRules
            .Where(r => r.ProductId == productId && r.FromSwimlaneId == fromSwimlaneId && r.IsAllowed)
            .Select(r => r.ToSwimlaneId)
            .ToListAsync(ct);

        return (false, allowedTargets);
    }

    /// <summary>
    /// Gets the list of allowed target swimlane IDs for a given source swimlane.
    /// Returns empty if no rules exist (all transitions allowed).
    /// </summary>
    public async Task<List<Guid>> GetAllowedTargetsAsync(Guid productId, Guid fromSwimlaneId, CancellationToken ct)
    {
        var hasRules = await _db.SwimlaneTransitionRules
            .AnyAsync(r => r.ProductId == productId, ct);

        if (!hasRules)
            return []; // Empty means all allowed

        return await _db.SwimlaneTransitionRules
            .Where(r => r.ProductId == productId && r.FromSwimlaneId == fromSwimlaneId && r.IsAllowed)
            .Select(r => r.ToSwimlaneId)
            .ToListAsync(ct);
    }

    private static SwimlaneTransitionRuleDto MapToDto(SwimlaneTransitionRule rule) => new()
    {
        Id = rule.Id,
        ProductId = rule.ProductId,
        FromSwimlaneId = rule.FromSwimlaneId,
        ToSwimlaneId = rule.ToSwimlaneId,
        IsAllowed = rule.IsAllowed,
        CreatedAt = rule.CreatedAt
    };
}

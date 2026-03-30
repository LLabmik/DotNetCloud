using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing card dependencies with cycle detection.
/// </summary>
public sealed class DependencyService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly ILogger<DependencyService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DependencyService"/> class.
    /// </summary>
    public DependencyService(TracksDbContext db, BoardService boardService, ActivityService activityService, ILogger<DependencyService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Adds a dependency between two cards. Detects cycles for BlockedBy relationships.
    /// </summary>
    public async Task<CardDependencyDto> AddDependencyAsync(Guid cardId, Guid dependsOnCardId, CardDependencyType type, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (cardId == dependsOnCardId)
            throw new ValidationException(ErrorCodes.DependencyCycleDetected, "A card cannot depend on itself.");

        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var dependsOnCard = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == dependsOnCardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Depends-on card not found.");

        // Check for duplicate
        var exists = await _db.CardDependencies
            .AnyAsync(d => d.CardId == cardId && d.DependsOnCardId == dependsOnCardId, cancellationToken);

        if (exists)
            throw new ValidationException(ErrorCodes.DependencyCycleDetected, "This dependency already exists.");

        // Cycle detection for BlockedBy dependencies
        if (type == CardDependencyType.BlockedBy)
        {
            if (await HasCycleAsync(cardId, dependsOnCardId, cancellationToken))
                throw new ValidationException(ErrorCodes.DependencyCycleDetected,
                    "Adding this dependency would create a circular dependency.");
        }

        var dependency = new CardDependency
        {
            CardId = cardId,
            DependsOnCardId = dependsOnCardId,
            Type = type
        };

        _db.CardDependencies.Add(dependency);
        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Dependency added: Card {CardId} {Type} Card {DependsOnCardId} by user {UserId}",
            cardId, type, dependsOnCardId, caller.UserId);

        await _activityService.LogAsync(card.Swimlane.BoardId, caller.UserId, "dependency.added", "CardDependency", dependency.Id,
            $"{{\"cardId\":\"{cardId}\",\"dependsOnCardId\":\"{dependsOnCardId}\",\"type\":\"{type}\"}}", cancellationToken);

        return new CardDependencyDto
        {
            CardId = cardId,
            DependsOnCardId = dependsOnCardId,
            DependsOnCardTitle = dependsOnCard.Title,
            Type = type
        };
    }

    /// <summary>
    /// Gets all dependencies for a card.
    /// </summary>
    public async Task<IReadOnlyList<CardDependencyDto>> GetDependenciesAsync(Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .AsNoTracking()
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardMemberAsync(card.Swimlane!.BoardId, caller.UserId, cancellationToken);

        var dependencies = await _db.CardDependencies
            .AsNoTracking()
            .Include(d => d.DependsOnCard)
            .Where(d => d.CardId == cardId)
            .ToListAsync(cancellationToken);

        return dependencies.Select(d => new CardDependencyDto
        {
            CardId = d.CardId,
            DependsOnCardId = d.DependsOnCardId,
            DependsOnCardTitle = d.DependsOnCard?.Title,
            Type = d.Type
        }).ToList();
    }

    /// <summary>
    /// Removes a dependency. Requires Member role or higher.
    /// </summary>
    public async Task RemoveDependencyAsync(Guid cardId, Guid dependsOnCardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var dependency = await _db.CardDependencies
            .FirstOrDefaultAsync(d => d.CardId == cardId && d.DependsOnCardId == dependsOnCardId, cancellationToken);

        if (dependency is null)
            return; // Idempotent

        _db.CardDependencies.Remove(dependency);
        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Dependency removed: Card {CardId} no longer depends on Card {DependsOnCardId}",
            cardId, dependsOnCardId);

        await _activityService.LogAsync(card.Swimlane.BoardId, caller.UserId, "dependency.removed", "CardDependency", dependency.Id,
            $"{{\"cardId\":\"{cardId}\",\"dependsOnCardId\":\"{dependsOnCardId}\"}}", cancellationToken);
    }

    /// <summary>
    /// Detects if adding a BlockedBy dependency from cardId → dependsOnCardId would create a cycle.
    /// Uses BFS to walk the existing BlockedBy graph from dependsOnCardId looking for cardId.
    /// </summary>
    internal async Task<bool> HasCycleAsync(Guid cardId, Guid dependsOnCardId, CancellationToken cancellationToken = default)
    {
        // If dependsOnCardId transitively depends on cardId (BlockedBy chain), adding cardId→dependsOnCardId creates a cycle
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(dependsOnCardId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == cardId)
                return true;

            if (!visited.Add(current))
                continue;

            var nextDeps = await _db.CardDependencies
                .Where(d => d.CardId == current && d.Type == CardDependencyType.BlockedBy)
                .Select(d => d.DependsOnCardId)
                .ToListAsync(cancellationToken);

            foreach (var dep in nextDeps)
            {
                queue.Enqueue(dep);
            }
        }

        return false;
    }
}

using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class PokerService
{
    private readonly TracksDbContext _db;

    public PokerService(TracksDbContext db) => _db = db;

    public async Task<PokerSessionDto> StartSessionAsync(Guid epicId, Guid createdByUserId, CreatePokerSessionDto dto, CancellationToken ct)
    {
        var epic = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == epicId && wi.Type == WorkItemType.Epic && !wi.IsDeleted, ct)
            ?? throw new ValidationException("EpicId", "Epic not found or is not an Epic.");

        // Validate item belongs to Epic's tree
        var item = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == dto.ItemId && !wi.IsDeleted, ct)
            ?? throw new ValidationException("ItemId", "Item not found.");

        if (item.Type != WorkItemType.Item)
            throw new ValidationException("ItemId", "Poker sessions are only valid for Item-type work items.");

        // Walk up the tree to verify the item belongs to this epic
        var isInEpicTree = await IsItemInEpicTreeAsync(dto.ItemId, epicId, ct);
        if (!isInEpicTree)
            throw new ValidationException("ItemId", "Item does not belong to the specified Epic's hierarchy.");

        var session = new PokerSession
        {
            EpicId = epicId,
            ItemId = dto.ItemId,
            CreatedByUserId = createdByUserId,
            Scale = dto.Scale,
            CustomScaleValues = dto.CustomScaleValues,
            Status = PokerSessionStatus.Voting,
            Round = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.PokerSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return MapToDto(session);
    }

    public async Task<PokerSessionDto?> GetSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _db.PokerSessions
            .Include(ps => ps.Votes)
            .FirstOrDefaultAsync(ps => ps.Id == sessionId, ct);

        return session is null ? null : MapToDto(session);
    }

    public async Task<PokerSessionDto> SubmitVoteAsync(Guid sessionId, Guid userId, SubmitPokerVoteDto dto, CancellationToken ct)
    {
        var session = await _db.PokerSessions
            .Include(ps => ps.Votes)
            .FirstOrDefaultAsync(ps => ps.Id == sessionId, ct)
            ?? throw new NotFoundException("PokerSession", sessionId);

        if (session.Status != PokerSessionStatus.Voting)
            throw new System.InvalidOperationException("Votes can only be submitted while the session is in Voting status.");

        var existingVote = session.Votes
            .FirstOrDefault(v => v.UserId == userId && v.Round == session.Round);

        if (existingVote is not null)
        {
            existingVote.Estimate = dto.Estimate;
            existingVote.VotedAt = DateTime.UtcNow;
        }
        else
        {
            var vote = new PokerVote
            {
                SessionId = sessionId,
                UserId = userId,
                Estimate = dto.Estimate,
                Round = session.Round,
                VotedAt = DateTime.UtcNow
            };
            _db.PokerVotes.Add(vote);
        }

        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return MapToDto(session);
    }

    public async Task<PokerSessionDto> RevealVotesAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _db.PokerSessions
            .Include(ps => ps.Votes)
            .FirstOrDefaultAsync(ps => ps.Id == sessionId, ct)
            ?? throw new NotFoundException("PokerSession", sessionId);

        if (session.Status != PokerSessionStatus.Voting)
            throw new System.InvalidOperationException("Only sessions in Voting status can be revealed.");

        session.Status = PokerSessionStatus.Revealed;
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return MapToDto(session);
    }

    public async Task<PokerSessionDto> AcceptEstimateAsync(Guid sessionId, string estimate, CancellationToken ct)
    {
        var session = await _db.PokerSessions
            .Include(ps => ps.Votes)
            .FirstOrDefaultAsync(ps => ps.Id == sessionId, ct)
            ?? throw new NotFoundException("PokerSession", sessionId);

        if (session.Status != PokerSessionStatus.Revealed)
            throw new System.InvalidOperationException("Estimates can only be accepted after votes are revealed.");

        session.AcceptedEstimate = estimate;
        session.Status = PokerSessionStatus.Completed;
        session.UpdatedAt = DateTime.UtcNow;

        // Update the Item's StoryPoints if the estimate is a valid integer
        if (int.TryParse(estimate, out var storyPoints))
        {
            var item = await _db.WorkItems.FindAsync([session.ItemId], ct);
            if (item is not null)
            {
                item.StoryPoints = storyPoints;
                item.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        return MapToDto(session);
    }

    public async Task<PokerSessionDto> NewRoundAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _db.PokerSessions
            .Include(ps => ps.Votes)
            .FirstOrDefaultAsync(ps => ps.Id == sessionId, ct)
            ?? throw new NotFoundException("PokerSession", sessionId);

        session.Round++;
        session.Status = PokerSessionStatus.Voting;
        session.AcceptedEstimate = null;
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return MapToDto(session);
    }

    public async Task<List<PokerVoteStatusDto>> GetVoteStatusAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _db.PokerSessions
            .Include(ps => ps.Votes)
            .FirstOrDefaultAsync(ps => ps.Id == sessionId, ct)
            ?? throw new NotFoundException("PokerSession", sessionId);

        var currentRoundVotes = session.Votes
            .Where(v => v.Round == session.Round)
            .ToList();

        var participantIds = await _db.ReviewSessionParticipants
            .Where(rsp => rsp.ReviewSessionId == session.ReviewSessionId && rsp.IsConnected)
            .Select(rsp => rsp.UserId)
            .Distinct()
            .ToListAsync(ct);

        // If no review session participants, return just the vote statuses
        if (participantIds.Count == 0)
        {
            return currentRoundVotes.Select(v => new PokerVoteStatusDto
            {
                HasVoted = true,
                Estimate = null // Don't reveal estimates in status
            }).ToList();
        }

        var result = new List<PokerVoteStatusDto>();
        foreach (var participantId in participantIds)
        {
            var vote = currentRoundVotes.FirstOrDefault(v => v.UserId == participantId);
            result.Add(new PokerVoteStatusDto
            {
                HasVoted = vote is not null,
                Estimate = null
            });
        }

        return result;
    }

    private async Task<bool> IsItemInEpicTreeAsync(Guid itemId, Guid epicId, CancellationToken ct)
    {
        // Walk up the parent chain to see if the item is under this epic
        var currentId = itemId;
        var visited = new HashSet<Guid>();
        const int maxDepth = 20;

        for (int i = 0; i < maxDepth; i++)
        {
            if (!visited.Add(currentId))
                return false; // Cycle detected

            if (currentId == epicId)
                return true;

            var workItem = await _db.WorkItems
                .Where(wi => wi.Id == currentId && !wi.IsDeleted)
                .Select(wi => new { wi.ParentWorkItemId })
                .FirstOrDefaultAsync(ct);

            if (workItem?.ParentWorkItemId is null)
                return false;

            currentId = workItem.ParentWorkItemId.Value;
        }

        return false;
    }

    private static PokerSessionDto MapToDto(PokerSession session)
    {
        return new PokerSessionDto
        {
            Id = session.Id,
            EpicId = session.EpicId,
            ItemId = session.ItemId,
            CreatedByUserId = session.CreatedByUserId,
            Scale = session.Scale,
            CustomScaleValues = session.CustomScaleValues,
            Status = session.Status,
            AcceptedEstimate = session.AcceptedEstimate,
            Round = session.Round,
            ReviewSessionId = session.ReviewSessionId,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt
        };
    }
}

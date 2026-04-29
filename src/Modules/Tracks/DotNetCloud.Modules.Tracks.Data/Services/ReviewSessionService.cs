using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class ReviewSessionService
{
    private readonly TracksDbContext _db;

    public ReviewSessionService(TracksDbContext db) => _db = db;

    public async Task<ReviewSessionDto> StartReviewSessionAsync(Guid epicId, Guid hostUserId, CancellationToken ct)
    {
        var epic = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == epicId && wi.Type == WorkItemType.Epic && !wi.IsDeleted, ct)
            ?? throw new ValidationException("EpicId", "Epic not found or is not an Epic.");

        var session = new ReviewSession
        {
            EpicId = epicId,
            HostUserId = hostUserId,
            Status = ReviewSessionStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        _db.ReviewSessions.Add(session);

        var participant = new ReviewSessionParticipant
        {
            ReviewSessionId = session.Id,
            UserId = hostUserId,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        };

        _db.ReviewSessionParticipants.Add(participant);
        await _db.SaveChangesAsync(ct);

        return MapToDto(session, 1);
    }

    public async Task<ReviewSessionDto?> GetReviewSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _db.ReviewSessions
            .Include(rs => rs.Participants)
            .FirstOrDefaultAsync(rs => rs.Id == sessionId, ct);

        return session is null ? null : MapToDto(session, session.Participants.Count);
    }

    public async Task<List<ReviewSessionParticipantDto>> GetParticipantsAsync(Guid sessionId, CancellationToken ct)
    {
        var participants = await _db.ReviewSessionParticipants
            .Where(rsp => rsp.ReviewSessionId == sessionId)
            .OrderBy(rsp => rsp.JoinedAt)
            .ToListAsync(ct);

        return participants.Select(MapParticipantToDto).ToList();
    }

    public async Task<ReviewSessionParticipantDto> JoinSessionAsync(Guid sessionId, Guid userId, CancellationToken ct)
    {
        var session = await _db.ReviewSessions.FindAsync([sessionId], ct)
            ?? throw new NotFoundException("ReviewSession", sessionId);

        if (session.Status == ReviewSessionStatus.Ended)
            throw new System.InvalidOperationException("Cannot join a session that has ended.");

        var existing = await _db.ReviewSessionParticipants
            .FirstOrDefaultAsync(rsp => rsp.ReviewSessionId == sessionId && rsp.UserId == userId, ct);

        if (existing is not null)
        {
            existing.IsConnected = true;
            await _db.SaveChangesAsync(ct);
            return MapParticipantToDto(existing);
        }

        var participant = new ReviewSessionParticipant
        {
            ReviewSessionId = sessionId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        };

        _db.ReviewSessionParticipants.Add(participant);
        await _db.SaveChangesAsync(ct);

        return MapParticipantToDto(participant);
    }

    public async Task LeaveSessionAsync(Guid sessionId, Guid userId, CancellationToken ct)
    {
        var participant = await _db.ReviewSessionParticipants
            .FirstOrDefaultAsync(rsp => rsp.ReviewSessionId == sessionId && rsp.UserId == userId, ct)
            ?? throw new NotFoundException("ReviewSessionParticipant", $"{sessionId}/{userId}");

        participant.IsConnected = false;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ReviewSessionDto> SetCurrentItemAsync(Guid sessionId, Guid itemId, CancellationToken ct)
    {
        var session = await _db.ReviewSessions
            .Include(rs => rs.Participants)
            .FirstOrDefaultAsync(rs => rs.Id == sessionId, ct)
            ?? throw new NotFoundException("ReviewSession", sessionId);

        if (session.Status == ReviewSessionStatus.Ended)
            throw new System.InvalidOperationException("Cannot modify a session that has ended.");

        var item = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == itemId && !wi.IsDeleted, ct)
            ?? throw new ValidationException("ItemId", "Item not found.");

        session.CurrentItemId = itemId;
        await _db.SaveChangesAsync(ct);

        return MapToDto(session, session.Participants.Count);
    }

    public async Task<ReviewSessionDto> EndSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _db.ReviewSessions
            .Include(rs => rs.Participants)
            .FirstOrDefaultAsync(rs => rs.Id == sessionId, ct)
            ?? throw new NotFoundException("ReviewSession", sessionId);

        if (session.Status == ReviewSessionStatus.Ended)
            throw new System.InvalidOperationException("Session has already ended.");

        session.Status = ReviewSessionStatus.Ended;
        session.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return MapToDto(session, session.Participants.Count);
    }

    private static ReviewSessionDto MapToDto(ReviewSession session, int participantCount)
    {
        return new ReviewSessionDto
        {
            Id = session.Id,
            EpicId = session.EpicId,
            HostUserId = session.HostUserId,
            CurrentItemId = session.CurrentItemId,
            Status = session.Status,
            ParticipantCount = participantCount,
            CreatedAt = session.CreatedAt,
            EndedAt = session.EndedAt
        };
    }

    private static ReviewSessionParticipantDto MapParticipantToDto(ReviewSessionParticipant participant)
    {
        return new ReviewSessionParticipantDto
        {
            UserId = participant.UserId,
            DisplayName = null,
            IsConnected = participant.IsConnected,
            JoinedAt = participant.JoinedAt
        };
    }
}

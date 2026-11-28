using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class ReviewSessionService
{
    private readonly TracksDbContext _db;
    private readonly DotNetCloud.Core.Capabilities.IUserDirectory? _userDirectory;

    public ReviewSessionService(TracksDbContext db, IServiceProvider? serviceProvider = null)
    {
        _db = db;

        // Optional capability, resolved with GetService so a test or minimal host container that
        // has no user directory still resolves sessions (participant names then stay blank).
        _userDirectory = serviceProvider?.GetService<DotNetCloud.Core.Capabilities.IUserDirectory>();
    }

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

        var names = await UserDisplayNames.LookupAsync(
            participants.Select(p => p.UserId), _userDirectory, ct);

        return participants.Select(p => MapParticipantToDto(p, names)).ToList();
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
            return await ResolveParticipantAsync(existing, ct);
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

        return await ResolveParticipantAsync(participant, ct);
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

    private static ReviewSessionParticipantDto MapParticipantToDto(
        ReviewSessionParticipant participant,
        IReadOnlyDictionary<Guid, string>? names = null)
    {
        return new ReviewSessionParticipantDto
        {
            UserId = participant.UserId,
            DisplayName = names is null ? null : names.GetValueOrDefault(participant.UserId),
            IsConnected = participant.IsConnected,
            JoinedAt = participant.JoinedAt
        };
    }

    private async Task<ReviewSessionParticipantDto> ResolveParticipantAsync(
        ReviewSessionParticipant participant, CancellationToken ct)
    {
        var names = await UserDisplayNames.LookupAsync([participant.UserId], _userDirectory, ct);
        return MapParticipantToDto(participant, names);
    }
}

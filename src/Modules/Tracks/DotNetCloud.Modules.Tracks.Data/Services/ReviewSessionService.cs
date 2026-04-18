using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing live review sessions where a host navigates cards
/// and participants follow in real-time with integrated planning poker.
/// Only available on Team-mode boards.
/// </summary>
public sealed class ReviewSessionService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly PokerService _pokerService;
    private readonly ITracksRealtimeService _realtimeService;
    private readonly ILogger<ReviewSessionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewSessionService"/> class.
    /// </summary>
    public ReviewSessionService(TracksDbContext db, BoardService boardService, PokerService pokerService, ITracksRealtimeService realtimeService, ILogger<ReviewSessionService> logger)
    {
        _db = db;
        _boardService = boardService;
        _pokerService = pokerService;
        _realtimeService = realtimeService;
        _logger = logger;
    }

    /// <summary>
    /// Starts a new review session on a board. Requires Admin role and Team-mode board.
    /// Only one active session per board at a time.
    /// </summary>
    public async Task<ReviewSessionDto> StartSessionAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);
        await _boardService.EnsureTeamModeAsync(boardId, cancellationToken);

        // Check for existing active session
        var activeExists = await _db.ReviewSessions
            .AnyAsync(rs => rs.BoardId == boardId && rs.Status != ReviewSessionStatus.Ended, cancellationToken);
        if (activeExists)
            throw new ValidationException(ErrorCodes.ReviewSessionAlreadyActive, "This board already has an active review session.");

        var session = new ReviewSession
        {
            BoardId = boardId,
            HostUserId = caller.UserId
        };

        // Host auto-joins as participant
        session.Participants.Add(new ReviewSessionParticipant
        {
            ReviewSessionId = session.Id,
            UserId = caller.UserId
        });

        _db.ReviewSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review session {SessionId} started on board {BoardId} by user {UserId}",
            session.Id, boardId, caller.UserId);

        // Add host to review SignalR group and broadcast session started
        await _realtimeService.AddUserToReviewGroupAsync(caller.UserId, session.Id, cancellationToken);
        await _realtimeService.BroadcastReviewSessionStateAsync(session.Id, boardId, "started", cancellationToken);

        return await GetSessionStateAsync(session.Id, caller, cancellationToken)
            ?? throw new System.InvalidOperationException("Review session was created but could not be retrieved.");
    }

    /// <summary>
    /// Joins an existing review session.
    /// </summary>
    public async Task<ReviewSessionDto> JoinSessionAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var session = await GetActiveSessionOrThrowAsync(sessionId, cancellationToken);
        await _boardService.EnsureBoardMemberAsync(session.BoardId, caller.UserId, cancellationToken);

        // Check if user already a participant
        var existing = await _db.ReviewSessionParticipants
            .FirstOrDefaultAsync(p => p.ReviewSessionId == sessionId && p.UserId == caller.UserId, cancellationToken);

        if (existing is not null)
        {
            // Reconnect
            existing.IsConnected = true;
        }
        else
        {
            _db.ReviewSessionParticipants.Add(new ReviewSessionParticipant
            {
                ReviewSessionId = sessionId,
                UserId = caller.UserId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} joined review session {SessionId}", caller.UserId, sessionId);

        // Add user to review SignalR group and broadcast participant joined
        await _realtimeService.AddUserToReviewGroupAsync(caller.UserId, sessionId, cancellationToken);
        await _realtimeService.BroadcastReviewParticipantChangedAsync(sessionId, caller.UserId, "joined", cancellationToken);

        return await GetSessionStateAsync(sessionId, caller, cancellationToken)
            ?? throw new System.InvalidOperationException("Review session could not be retrieved.");
    }

    /// <summary>
    /// Marks a participant as disconnected from the review session.
    /// </summary>
    public async Task LeaveSessionAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var participant = await _db.ReviewSessionParticipants
            .FirstOrDefaultAsync(p => p.ReviewSessionId == sessionId && p.UserId == caller.UserId, cancellationToken);

        if (participant is not null)
        {
            participant.IsConnected = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("User {UserId} left review session {SessionId}", caller.UserId, sessionId);

        // Remove user from review SignalR group and broadcast participant left
        await _realtimeService.RemoveUserFromReviewGroupAsync(caller.UserId, sessionId, cancellationToken);
        await _realtimeService.BroadcastReviewParticipantChangedAsync(sessionId, caller.UserId, "left", cancellationToken);
    }

    /// <summary>
    /// Sets the current card being reviewed. Host only.
    /// </summary>
    public async Task<ReviewSessionDto> SetCurrentCardAsync(Guid sessionId, Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var session = await GetActiveSessionOrThrowAsync(sessionId, cancellationToken);
        EnsureHost(session, caller.UserId);

        // Verify card exists and belongs to the same board
        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        if (card.Swimlane!.BoardId != session.BoardId)
            throw new ValidationException(ErrorCodes.CardNotFound, "Card does not belong to this board.");

        session.CurrentCardId = cardId;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review session {SessionId}: current card set to {CardId} by host {UserId}",
            sessionId, cardId, caller.UserId);

        // Broadcast card change to all review participants
        await _realtimeService.BroadcastReviewCardChangedAsync(sessionId, session.BoardId, cardId, cancellationToken);

        return await GetSessionStateAsync(sessionId, caller, cancellationToken)
            ?? throw new System.InvalidOperationException("Review session could not be retrieved.");
    }

    /// <summary>
    /// Starts a planning poker session for the current card in the review. Host only.
    /// One active poker per review session at a time.
    /// </summary>
    public async Task<ReviewSessionDto> StartPokerForCurrentCardAsync(Guid sessionId, StartReviewPokerDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var session = await GetActiveSessionOrThrowAsync(sessionId, cancellationToken);
        EnsureHost(session, caller.UserId);

        if (!session.CurrentCardId.HasValue)
            throw new ValidationException(ErrorCodes.CardNotFound, "No card is currently selected for review.");

        // Check for active poker in this review session
        var activePoker = await _db.PokerSessions
            .AnyAsync(ps => ps.ReviewSessionId == sessionId && ps.Status == PokerSessionStatus.Voting, cancellationToken);
        if (activePoker)
            throw new ValidationException(ErrorCodes.ReviewPokerStillActive, "There is already an active poker session in this review. Accept or cancel it first.");

        // Cancel any orphaned poker sessions for this card (e.g. from a previous review or standalone poker)
        var orphanedSessions = await _db.PokerSessions
            .Where(ps => ps.CardId == session.CurrentCardId.Value
                      && ps.Status == PokerSessionStatus.Voting
                      && ps.ReviewSessionId != sessionId)
            .ToListAsync(cancellationToken);
        foreach (var orphan in orphanedSessions)
        {
            orphan.Status = PokerSessionStatus.Cancelled;
            _logger.LogWarning("Cancelled orphaned poker session {PokerId} for card {CardId}", orphan.Id, orphan.CardId);
        }
        if (orphanedSessions.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        // Create poker session linked to review
        var pokerSession = await _pokerService.StartSessionAsync(session.CurrentCardId.Value,
            new CreatePokerSessionDto
            {
                Scale = dto.Scale,
                CustomScaleValues = dto.CustomScaleValues
            }, caller, cancellationToken);

        // Link to review session
        var poker = await _db.PokerSessions.FindAsync([pokerSession.Id], cancellationToken);
        if (poker is not null)
        {
            poker.ReviewSessionId = sessionId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Poker session {PokerId} started in review {SessionId} for card {CardId}",
            pokerSession.Id, sessionId, session.CurrentCardId.Value);

        // Broadcast poker started in review
        await _realtimeService.BroadcastReviewPokerStateAsync(sessionId, pokerSession.Id, session.BoardId, "started", cancellationToken);

        return await GetSessionStateAsync(sessionId, caller, cancellationToken)
            ?? throw new System.InvalidOperationException("Review session could not be retrieved.");
    }

    /// <summary>
    /// Gets the current state of a review session.
    /// </summary>
    public async Task<ReviewSessionDto?> GetSessionStateAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var session = await _db.ReviewSessions
            .AsNoTracking()
            .Include(rs => rs.Participants)
            .FirstOrDefaultAsync(rs => rs.Id == sessionId, cancellationToken);

        if (session is null) return null;

        await _boardService.EnsureBoardMemberAsync(session.BoardId, caller.UserId, cancellationToken);

        // Get active poker session if any
        var activePoker = await _db.PokerSessions
            .AsNoTracking()
            .Include(ps => ps.Votes)
            .FirstOrDefaultAsync(ps => ps.ReviewSessionId == sessionId
                && (ps.Status == PokerSessionStatus.Voting || ps.Status == PokerSessionStatus.Revealed), cancellationToken);

        PokerSessionDto? pokerDto = null;
        if (activePoker is not null)
        {
            pokerDto = new PokerSessionDto
            {
                Id = activePoker.Id,
                CardId = activePoker.CardId,
                BoardId = activePoker.BoardId,
                CreatedByUserId = activePoker.CreatedByUserId,
                Scale = activePoker.Scale,
                CustomScaleValues = activePoker.CustomScaleValues,
                Status = activePoker.Status,
                AcceptedEstimate = activePoker.AcceptedEstimate,
                Round = activePoker.Round,
                CreatedAt = activePoker.CreatedAt,
                UpdatedAt = activePoker.UpdatedAt,
                // Only include votes if revealed
                Votes = activePoker.Status == PokerSessionStatus.Revealed
                    ? activePoker.Votes.Select(v => new PokerVoteDto
                    {
                        UserId = v.UserId,
                        Estimate = v.Estimate,
                        Round = v.Round,
                        VotedAt = v.VotedAt
                    }).ToList()
                    : []
            };
        }

        return new ReviewSessionDto
        {
            Id = session.Id,
            BoardId = session.BoardId,
            HostUserId = session.HostUserId,
            CurrentCardId = session.CurrentCardId,
            Status = session.Status,
            CreatedAt = session.CreatedAt,
            EndedAt = session.EndedAt,
            Participants = session.Participants.Select(p => new ReviewSessionParticipantDto
            {
                Id = p.Id,
                UserId = p.UserId,
                JoinedAt = p.JoinedAt,
                IsConnected = p.IsConnected
            }).ToList(),
            ActivePokerSession = pokerDto
        };
    }

    /// <summary>
    /// Ends a review session. Host only.
    /// </summary>
    public async Task EndSessionAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var session = await GetActiveSessionOrThrowAsync(sessionId, cancellationToken);
        EnsureHost(session, caller.UserId);

        session.Status = ReviewSessionStatus.Ended;
        session.EndedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review session {SessionId} ended by host {UserId}", sessionId, caller.UserId);

        // Broadcast session ended
        await _realtimeService.BroadcastReviewSessionStateAsync(sessionId, session.BoardId, "ended", cancellationToken);
    }

    /// <summary>
    /// Gets the active review session for a board, if any.
    /// </summary>
    public async Task<ReviewSessionDto?> GetActiveSessionForBoardAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardMemberAsync(boardId, caller.UserId, cancellationToken);

        var session = await _db.ReviewSessions
            .AsNoTracking()
            .Where(rs => rs.BoardId == boardId && rs.Status != ReviewSessionStatus.Ended)
            .Select(rs => rs.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == Guid.Empty) return null;

        return await GetSessionStateAsync(session, caller, cancellationToken);
    }

    private async Task<ReviewSession> GetActiveSessionOrThrowAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _db.ReviewSessions
            .FirstOrDefaultAsync(rs => rs.Id == sessionId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.ReviewSessionNotFound, "Review session not found.");

        if (session.Status == ReviewSessionStatus.Ended)
            throw new ValidationException(ErrorCodes.ReviewSessionEnded, "This review session has already ended.");

        return session;
    }

    private static void EnsureHost(ReviewSession session, Guid userId)
    {
        if (session.HostUserId != userId)
            throw new ValidationException(ErrorCodes.ReviewSessionNotHost, "Only the session host can perform this action.");
    }
}

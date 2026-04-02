using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for planning poker (estimation) sessions on cards.
/// </summary>
public sealed class PokerService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly ILogger<PokerService> _logger;

    // Valid values for built-in scales
    private static readonly IReadOnlySet<string> FibonacciValues =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "0", "1", "2", "3", "5", "8", "13", "21", "34", "?" };

    private static readonly IReadOnlySet<string> TShirtValues =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "XS", "S", "M", "L", "XL", "XXL", "?" };

    private static readonly IReadOnlySet<string> PowersOfTwoValues =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "0", "1", "2", "4", "8", "16", "32", "?" };

    /// <summary>
    /// Initializes a new instance of the <see cref="PokerService"/> class.
    /// </summary>
    public PokerService(TracksDbContext db, BoardService boardService, ActivityService activityService, ILogger<PokerService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Starts a new planning poker session for a card. Only one active session per card is allowed.
    /// </summary>
    public async Task<PokerSessionDto> StartSessionAsync(Guid cardId, CreatePokerSessionDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        // Check for active session
        var activeExists = await _db.PokerSessions
            .AnyAsync(s => s.CardId == cardId && s.Status == PokerSessionStatus.Voting, cancellationToken);
        if (activeExists)
            throw new ValidationException(ErrorCodes.PokerSessionAlreadyActive, "This card already has an active poker session.");

        if (dto.Scale == PokerScale.Custom && string.IsNullOrWhiteSpace(dto.CustomScaleValues))
            throw new ValidationException(ErrorCodes.PokerInvalidEstimate, "Custom scale requires CustomScaleValues to be provided.");

        var session = new PokerSession
        {
            CardId = cardId,
            BoardId = card.Swimlane.BoardId,
            CreatedByUserId = caller.UserId,
            Scale = dto.Scale,
            CustomScaleValues = dto.CustomScaleValues,
            Status = PokerSessionStatus.Voting,
            Round = 1
        };

        _db.PokerSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Poker session {SessionId} started for card {CardId} by user {UserId}",
            session.Id, cardId, caller.UserId);

        await _activityService.LogAsync(card.Swimlane.BoardId, caller.UserId, "poker.started", "PokerSession", session.Id,
            $"{{\"cardId\":\"{cardId}\",\"scale\":\"{dto.Scale}\"}}", cancellationToken);

        return MapToDto(session);
    }

    /// <summary>
    /// Gets a poker session by ID.
    /// </summary>
    public async Task<PokerSessionDto?> GetSessionAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var session = await _db.PokerSessions
            .AsNoTracking()
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
            return null;

        await _boardService.EnsureBoardMemberAsync(session.BoardId, caller.UserId, cancellationToken);

        return MapToDto(session);
    }

    /// <summary>
    /// Gets active or completed sessions for a card.
    /// </summary>
    public async Task<IReadOnlyList<PokerSessionDto>> GetCardSessionsAsync(Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardMemberAsync(card.Swimlane!.BoardId, caller.UserId, cancellationToken);

        var sessions = await _db.PokerSessions
            .AsNoTracking()
            .Include(s => s.Votes)
            .Where(s => s.CardId == cardId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return sessions.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Submits or updates a vote in a session. Voting must be open (Status = Voting).
    /// </summary>
    public async Task<PokerSessionDto> SubmitVoteAsync(Guid sessionId, SubmitPokerVoteDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var session = await _db.PokerSessions
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.PokerSessionNotFound, "Poker session not found.");

        if (session.Status != PokerSessionStatus.Voting)
            throw new ValidationException(ErrorCodes.PokerSessionNotVoting,
                $"Session is in {session.Status} state. Voting is closed.");

        await _boardService.EnsureBoardMemberAsync(session.BoardId, caller.UserId, cancellationToken);

        ValidateEstimate(session, dto.Estimate);

        // Upsert vote: remove existing vote for this round, then add new one
        var existingVote = session.Votes.FirstOrDefault(v => v.UserId == caller.UserId && v.Round == session.Round);
        if (existingVote is not null)
            _db.PokerVotes.Remove(existingVote);

        var vote = new PokerVote
        {
            SessionId = sessionId,
            UserId = caller.UserId,
            Estimate = dto.Estimate,
            Round = session.Round,
            VotedAt = DateTime.UtcNow
        };
        _db.PokerVotes.Add(vote);
        session.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} voted '{Estimate}' in session {SessionId} round {Round}",
            caller.UserId, dto.Estimate, sessionId, session.Round);

        return MapToDto(session);
    }

    /// <summary>
    /// Reveals all votes in the session (Admin or session creator required).
    /// </summary>
    public async Task<PokerSessionDto> RevealSessionAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var session = await _db.PokerSessions
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.PokerSessionNotFound, "Poker session not found.");

        if (session.Status != PokerSessionStatus.Voting)
            throw new ValidationException(ErrorCodes.PokerSessionNotVoting, "Session votes are already revealed or session is completed.");

        await _boardService.EnsureBoardRoleAsync(session.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        session.Status = PokerSessionStatus.Revealed;
        session.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Poker session {SessionId} votes revealed by user {UserId}", sessionId, caller.UserId);

        return MapToDto(session);
    }

    /// <summary>
    /// Accepts an estimate, applies it to the card's story points, and completes the session.
    /// </summary>
    public async Task<PokerSessionDto> AcceptEstimateAsync(Guid sessionId, AcceptPokerEstimateDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var session = await _db.PokerSessions
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.PokerSessionNotFound, "Poker session not found.");

        if (session.Status == PokerSessionStatus.Completed)
            throw new ValidationException(ErrorCodes.PokerSessionNotVoting, "Session is already completed.");

        await _boardService.EnsureBoardRoleAsync(session.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        session.Status = PokerSessionStatus.Completed;
        session.AcceptedEstimate = dto.AcceptedEstimate;
        session.UpdatedAt = DateTime.UtcNow;

        // Apply to card if numeric story points provided
        if (dto.StoryPoints.HasValue && dto.StoryPoints.Value > 0)
        {
            var card = await _db.Cards.FindAsync([session.CardId], cancellationToken);
            if (card is not null)
            {
                card.StoryPoints = dto.StoryPoints.Value;
                card.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Poker session {SessionId} completed with estimate '{Estimate}' by user {UserId}",
            sessionId, dto.AcceptedEstimate, caller.UserId);

        await _activityService.LogAsync(session.BoardId, caller.UserId, "poker.completed", "PokerSession", sessionId,
            $"{{\"acceptedEstimate\":\"{dto.AcceptedEstimate}\",\"storyPoints\":{dto.StoryPoints ?? 0}}}",
            cancellationToken);

        return MapToDto(session);
    }

    /// <summary>
    /// Restarts voting for a new round (Admin required). Clears current-round votes and increments round.
    /// </summary>
    public async Task<PokerSessionDto> StartNewRoundAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var session = await _db.PokerSessions
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.PokerSessionNotFound, "Poker session not found.");

        if (session.Status == PokerSessionStatus.Completed)
            throw new ValidationException(ErrorCodes.PokerSessionNotVoting, "Session is already completed.");

        await _boardService.EnsureBoardRoleAsync(session.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        // Remove current-round votes
        var currentVotes = session.Votes.Where(v => v.Round == session.Round).ToList();
        _db.PokerVotes.RemoveRange(currentVotes);

        session.Round++;
        session.Status = PokerSessionStatus.Voting;
        session.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Poker session {SessionId} restarted as round {Round} by user {UserId}",
            sessionId, session.Round, caller.UserId);

        return MapToDto(session);
    }

    /// <summary>
    /// Gets the vote status for each participant (who voted, who hasn't) without revealing actual vote values.
    /// </summary>
    public async Task<IReadOnlyList<PokerVoteStatusDto>> GetVoteStatusAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var session = await _db.PokerSessions
            .AsNoTracking()
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.PokerSessionNotFound, "Poker session not found.");

        await _boardService.EnsureBoardMemberAsync(session.BoardId, caller.UserId, cancellationToken);

        // Get all board members who could vote
        var boardMembers = await _db.BoardMembers
            .AsNoTracking()
            .Where(m => m.BoardId == session.BoardId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        var currentRoundVoters = session.Votes
            .Where(v => v.Round == session.Round)
            .Select(v => v.UserId)
            .ToHashSet();

        return boardMembers.Select(userId => new PokerVoteStatusDto
        {
            UserId = userId,
            HasVoted = currentRoundVoters.Contains(userId)
        }).ToList();
    }

    // ─── Private Helpers ─────────────────────────────────────────────

    private static void ValidateEstimate(PokerSession session, string estimate)
    {
        var valid = session.Scale switch
        {
            PokerScale.Fibonacci => FibonacciValues.Contains(estimate),
            PokerScale.TShirt => TShirtValues.Contains(estimate),
            PokerScale.PowersOfTwo => PowersOfTwoValues.Contains(estimate),
            PokerScale.Custom => IsValidCustomEstimate(session.CustomScaleValues, estimate),
            _ => false
        };

        if (!valid)
            throw new ValidationException(ErrorCodes.PokerInvalidEstimate,
                $"'{estimate}' is not a valid estimate for the {session.Scale} scale.");
    }

    private static bool IsValidCustomEstimate(string? customValuesJson, string estimate)
    {
        if (string.IsNullOrEmpty(customValuesJson))
            return false;

        try
        {
            var values = JsonSerializer.Deserialize<string[]>(customValuesJson);
            return values?.Contains(estimate, StringComparer.OrdinalIgnoreCase) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private static PokerSessionDto MapToDto(PokerSession s) => new()
    {
        Id = s.Id,
        CardId = s.CardId,
        BoardId = s.BoardId,
        CreatedByUserId = s.CreatedByUserId,
        Scale = s.Scale,
        CustomScaleValues = s.CustomScaleValues,
        Status = s.Status,
        AcceptedEstimate = s.AcceptedEstimate,
        Round = s.Round,
        Votes = s.Votes
            .Where(v => s.Status != PokerSessionStatus.Voting || v.UserId == s.CreatedByUserId)
            .Select(v => new PokerVoteDto
            {
                UserId = v.UserId,
                // Only reveal estimates when the session is Revealed or Completed
                Estimate = s.Status is PokerSessionStatus.Revealed or PokerSessionStatus.Completed
                    ? v.Estimate
                    : "?",
                Round = v.Round,
                VotedAt = v.VotedAt
            }).ToList(),
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
}

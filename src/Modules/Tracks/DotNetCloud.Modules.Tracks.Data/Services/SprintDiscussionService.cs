using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using ApiSprintDiscussionDto = DotNetCloud.Core.Services.ModuleApis.SprintDiscussionDto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class SprintDiscussionService
{
    private readonly TracksDbContext _db;
    private readonly ITracksRealtimeService _realtimeService;
    private readonly ILogger<SprintDiscussionService> _logger;

    public SprintDiscussionService(
        TracksDbContext db,
        ITracksRealtimeService realtimeService,
        ILogger<SprintDiscussionService> logger)
    {
        _db = db;
        _realtimeService = realtimeService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ApiSprintDiscussionDto>> GetSprintMessagesAsync(
        Guid sprintId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var messages = await _db.SprintDiscussions
            .AsNoTracking()
            .Where(m => m.SprintId == sprintId)
            .OrderBy(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(m => Map(m))
            .ToListAsync(ct);

        return messages;
    }

    public async Task<IReadOnlyList<ApiSprintDiscussionDto>> GetReviewSessionMessagesAsync(
        Guid reviewSessionId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var messages = await _db.SprintDiscussions
            .AsNoTracking()
            .Where(m => m.ReviewSessionId == reviewSessionId)
            .OrderBy(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(m => Map(m))
            .ToListAsync(ct);

        return messages;
    }

    public async Task<ApiSprintDiscussionDto> SendSprintMessageAsync(
        Guid sprintId, Guid userId, string userDisplayName, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("Content", "Content is required.");

        if (content.Length > 2000)
            throw new ValidationException("Content", "Content must be 2000 characters or fewer.");

        var message = new SprintDiscussion
        {
            SprintId = sprintId,
            ReviewSessionId = null,
            UserId = userId,
            UserDisplayName = userDisplayName,
            Content = content.Trim()
        };

        _db.SprintDiscussions.Add(message);
        await _db.SaveChangesAsync(ct);

        var dto = Map(message);

        await _realtimeService.BroadcastSprintDiscussionMessageAsync(sprintId, dto, ct);

        return dto;
    }

    public async Task<ApiSprintDiscussionDto> SendReviewSessionMessageAsync(
        Guid reviewSessionId, Guid userId, string userDisplayName, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("Content", "Content is required.");

        if (content.Length > 2000)
            throw new ValidationException("Content", "Content must be 2000 characters or fewer.");

        var message = new SprintDiscussion
        {
            SprintId = null,
            ReviewSessionId = reviewSessionId,
            UserId = userId,
            UserDisplayName = userDisplayName,
            Content = content.Trim()
        };

        _db.SprintDiscussions.Add(message);
        await _db.SaveChangesAsync(ct);

        var dto = Map(message);

        await _realtimeService.BroadcastReviewDiscussionMessageAsync(reviewSessionId, dto, ct);

        return dto;
    }

    private static ApiSprintDiscussionDto Map(SprintDiscussion m) => new(
        m.Id,
        m.SprintId,
        m.ReviewSessionId,
        m.UserId,
        m.UserDisplayName,
        m.Content,
        m.CreatedAt
    );
}

using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Manages user storage quotas backed by the Files database.
/// </summary>
internal sealed class QuotaService : IQuotaService
{
    private readonly FilesDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly IOptions<QuotaOptions> _options;
    private readonly ILogger<QuotaService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuotaService"/> class.
    /// </summary>
    public QuotaService(
        FilesDbContext db,
        IEventBus eventBus,
        IOptions<QuotaOptions> options,
        ILogger<QuotaService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QuotaDto> GetQuotaAsync(Guid userId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var quota = await _db.FileQuotas
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.UserId == userId, cancellationToken);

        if (quota is null)
            throw new NotFoundException("FileQuota", userId);

        return ToDto(quota);
    }

    /// <inheritdoc />
    public async Task<QuotaDto> GetOrCreateQuotaAsync(Guid userId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var quota = await _db.FileQuotas
            .FirstOrDefaultAsync(q => q.UserId == userId, cancellationToken);

        if (quota is null)
        {
            var defaultBytes = _options.Value.DefaultQuotaBytes;
            quota = new FileQuota
            {
                UserId = userId,
                MaxBytes = defaultBytes
            };
            _db.FileQuotas.Add(quota);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created default quota for user {UserId}: {MaxBytes} bytes", userId, defaultBytes);
        }

        return ToDto(quota);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuotaDto>> GetAllQuotasAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var quotas = await _db.FileQuotas
            .AsNoTracking()
            .OrderBy(q => q.UserId)
            .ToListAsync(cancellationToken);

        return quotas.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<QuotaDto> SetQuotaAsync(Guid userId, long maxBytes, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var quota = await _db.FileQuotas
            .FirstOrDefaultAsync(q => q.UserId == userId, cancellationToken);

        if (quota is null)
        {
            quota = new FileQuota
            {
                UserId = userId,
                MaxBytes = maxBytes
            };
            _db.FileQuotas.Add(quota);
        }
        else
        {
            quota.MaxBytes = maxBytes;
            quota.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Quota set for user {UserId}: {MaxBytes} bytes by {CallerId}",
            userId, maxBytes, caller.UserId);

        return ToDto(quota);
    }

    /// <inheritdoc />
    public async Task<bool> HasSufficientQuotaAsync(Guid userId, long requiredBytes, CancellationToken cancellationToken = default)
    {
        var quota = await _db.FileQuotas
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.UserId == userId, cancellationToken);

        if (quota is null)
            return false;

        // Unlimited quota
        if (quota.MaxBytes == 0)
            return true;

        return quota.UsedBytes + requiredBytes <= quota.MaxBytes;
    }

    /// <inheritdoc />
    public async Task AdjustUsedBytesAsync(Guid userId, long delta, CancellationToken cancellationToken = default)
    {
        if (delta == 0)
            return;

        var quota = await _db.FileQuotas
            .FirstOrDefaultAsync(q => q.UserId == userId, cancellationToken);

        if (quota is null)
            return;

        quota.UsedBytes = Math.Max(0, quota.UsedBytes + delta);
        quota.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await PublishQuotaNotificationAsync(quota, cancellationToken);

        _logger.LogDebug("Adjusted used bytes for user {UserId} by {Delta}: {UsedBytes}/{MaxBytes}",
            userId, delta, quota.UsedBytes, quota.MaxBytes);
    }

    /// <inheritdoc />
    public async Task RecalculateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var quota = await _db.FileQuotas
            .FirstOrDefaultAsync(q => q.UserId == userId, cancellationToken);

        if (quota is null)
            return;

        // When ExcludeTrashedFromQuota is true (default), the global query filter already
        // excludes soft-deleted nodes — use a normal query.
        // When false, bypass the filter so trashed items are counted against the quota too.
        var baseQuery = _options.Value.ExcludeTrashedFromQuota
            ? _db.FileNodes.AsNoTracking()
            : _db.FileNodes.AsNoTracking().IgnoreQueryFilters();

        var usedBytes = await baseQuery
            .Where(n => n.OwnerId == userId && n.NodeType == FileNodeType.File)
            .SumAsync(n => n.Size, cancellationToken);

        quota.UsedBytes = usedBytes;
        quota.LastCalculatedAt = DateTime.UtcNow;
        quota.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await PublishQuotaNotificationAsync(quota, cancellationToken);

        _logger.LogDebug("Quota recalculated for user {UserId}: {UsedBytes} bytes", userId, usedBytes);
    }

    private async Task PublishQuotaNotificationAsync(FileQuota quota, CancellationToken cancellationToken)
    {
        // Unlimited quota — no notifications needed
        if (quota.MaxBytes == 0)
            return;

        var usagePercent = quota.UsagePercent;
        var opts = _options.Value;
        var systemCaller = CallerContext.CreateSystemContext();

        if (usagePercent >= 100.0)
        {
            await _eventBus.PublishAsync(new QuotaExceededEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UserId = quota.UserId,
                UsedBytes = quota.UsedBytes,
                MaxBytes = quota.MaxBytes,
                UsagePercent = usagePercent
            }, systemCaller, cancellationToken);
        }
        else if (usagePercent >= opts.CriticalAtPercent)
        {
            await _eventBus.PublishAsync(new QuotaCriticalEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UserId = quota.UserId,
                UsedBytes = quota.UsedBytes,
                MaxBytes = quota.MaxBytes,
                UsagePercent = usagePercent
            }, systemCaller, cancellationToken);
        }
        else if (usagePercent >= opts.WarnAtPercent)
        {
            await _eventBus.PublishAsync(new QuotaWarningEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UserId = quota.UserId,
                UsedBytes = quota.UsedBytes,
                MaxBytes = quota.MaxBytes,
                UsagePercent = usagePercent
            }, systemCaller, cancellationToken);
        }
    }

    private static QuotaDto ToDto(FileQuota quota) => new()
    {
        UserId = quota.UserId,
        MaxBytes = quota.MaxBytes,
        UsedBytes = quota.UsedBytes,
        RemainingBytes = quota.RemainingBytes,
        UsagePercent = quota.UsagePercent
    };
}

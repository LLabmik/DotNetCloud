using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Manages user storage quotas backed by the Files database.
/// </summary>
internal sealed class QuotaService : IQuotaService
{
    private readonly FilesDbContext _db;
    private readonly ILogger<QuotaService> _logger;

    public QuotaService(FilesDbContext db, ILogger<QuotaService> logger)
    {
        _db = db;
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
    public async Task RecalculateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var quota = await _db.FileQuotas
            .FirstOrDefaultAsync(q => q.UserId == userId, cancellationToken);

        if (quota is null)
            return;

        var usedBytes = await _db.FileNodes
            .AsNoTracking()
            .Where(n => n.OwnerId == userId && n.NodeType == FileNodeType.File)
            .SumAsync(n => n.Size, cancellationToken);

        quota.UsedBytes = usedBytes;
        quota.LastCalculatedAt = DateTime.UtcNow;
        quota.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Quota recalculated for user {UserId}: {UsedBytes} bytes", userId, usedBytes);
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

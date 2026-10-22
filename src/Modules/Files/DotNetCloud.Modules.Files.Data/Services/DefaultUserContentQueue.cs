using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Enqueues per-user convergence work. Rows are the queue: the retry worker claims them with a
/// lease, and the unique per-user key makes duplicate enqueues harmless.
/// </summary>
public sealed class DefaultUserContentQueue
{
    /// <summary>Retries allowed before a user is surfaced as failed.</summary>
    public const int MaxAttempts = 5;

    private readonly FilesDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserContentQueue"/> class.
    /// </summary>
    public DefaultUserContentQueue(FilesDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Queues a convergence pass for each user. Users already queued or running are reported as
    /// skipped; failed rows are re-queued (resetting the retry counter only when
    /// <paramref name="resetAttempts"/> is true, i.e. an explicit administrator action).
    /// </summary>
    /// <param name="userIds">Users to converge.</param>
    /// <param name="resetAttempts">Whether to clear the retry counter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<(int Queued, int Skipped)> EnqueueAsync(
        IReadOnlyCollection<Guid> userIds,
        bool resetAttempts = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
            return (0, 0);

        var ids = userIds.Distinct().ToList();
        var existing = await _db.UserContentProvisioning
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var queued = 0;
        var skipped = 0;

        foreach (var userId in ids)
        {
            if (!existing.TryGetValue(userId, out var row))
            {
                _db.UserContentProvisioning.Add(new UserContentProvisioning
                {
                    UserId = userId,
                    Status = UserContentProvisioningStatus.Pending,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
                queued++;
                continue;
            }

            var leaseValid = row.Status == UserContentProvisioningStatus.InProgress
                             && row.LeaseExpiresAtUtc > now;

            if (leaseValid)
            {
                skipped++;
                continue;
            }

            row.Status = UserContentProvisioningStatus.Pending;
            row.LeaseExpiresAtUtc = null;
            row.LastError = null;
            row.UpdatedAtUtc = now;

            if (resetAttempts)
                row.Attempts = 0;

            queued++;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (queued, skipped);
    }
}

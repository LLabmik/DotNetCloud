using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Manages user storage quotas.
/// </summary>
public interface IQuotaService
{
    /// <summary>Gets the quota for a user. Throws <see cref="DotNetCloud.Core.Errors.NotFoundException"/> if no record exists.</summary>
    Task<QuotaDto> GetQuotaAsync(Guid userId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the quota for a user, creating it with the configured default if it does not yet exist.</summary>
    Task<QuotaDto> GetOrCreateQuotaAsync(Guid userId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Returns quota records for every user. Requires admin caller.</summary>
    Task<IReadOnlyList<QuotaDto>> GetAllQuotasAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Sets the maximum storage quota for a user.</summary>
    Task<QuotaDto> SetQuotaAsync(Guid userId, long maxBytes, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a user has sufficient quota for the given size.</summary>
    Task<bool> HasSufficientQuotaAsync(Guid userId, long requiredBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adjusts the used bytes for a user by <paramref name="delta"/> bytes.
    /// A positive delta increases usage; a negative delta decreases it.
    /// Publishes quota notification events when thresholds are crossed.
    /// </summary>
    Task AdjustUsedBytesAsync(Guid userId, long delta, CancellationToken cancellationToken = default);

    /// <summary>Recalculates the used bytes for a user by summing file sizes.</summary>
    Task RecalculateAsync(Guid userId, CancellationToken cancellationToken = default);
}

using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Service for managing organization-wide announcements with acknowledgement tracking.
/// </summary>
public interface IAnnouncementService
{
    /// <summary>Creates a new announcement.</summary>
    Task<AnnouncementDto> CreateAsync(CreateAnnouncementDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists announcements visible to the caller.</summary>
    Task<IReadOnlyList<AnnouncementDto>> ListAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a single announcement by ID.</summary>
    Task<AnnouncementDto?> GetAsync(Guid id, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates an announcement.</summary>
    Task UpdateAsync(Guid id, UpdateAnnouncementDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes an announcement.</summary>
    Task DeleteAsync(Guid id, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Acknowledges an announcement for the caller.</summary>
    Task AcknowledgeAsync(Guid id, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the list of users who acknowledged an announcement.</summary>
    Task<IReadOnlyList<AnnouncementAcknowledgementDto>> GetAcknowledgementsAsync(Guid id, CallerContext caller, CancellationToken cancellationToken = default);
}

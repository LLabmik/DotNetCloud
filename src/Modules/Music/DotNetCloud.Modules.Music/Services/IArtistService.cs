using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Manages music artist records.
/// </summary>
public interface IArtistService
{
    /// <summary>Gets an artist by ID.</summary>
    Task<ArtistDto?> GetArtistAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists artists with paging.</summary>
    Task<IReadOnlyList<ArtistDto>> ListArtistsAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Searches artists by query.</summary>
    Task<IReadOnlyList<ArtistDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default);

    /// <summary>Deletes an artist.</summary>
    Task DeleteArtistAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the total artist count for an owner.</summary>
    Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken = default);
}

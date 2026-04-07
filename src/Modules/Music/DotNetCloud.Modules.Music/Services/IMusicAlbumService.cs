using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Manages music albums.
/// </summary>
public interface IMusicAlbumService
{
    /// <summary>Gets an album by ID.</summary>
    Task<MusicAlbumDto?> GetAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists albums with paging.</summary>
    Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Lists albums for a specific artist.</summary>
    Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsByArtistAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Searches albums by query.</summary>
    Task<IReadOnlyList<MusicAlbumDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default);

    /// <summary>Gets recently added albums.</summary>
    Task<IReadOnlyList<MusicAlbumDto>> GetRecentAlbumsAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default);

    /// <summary>Gets starred (favorited) albums for the current user.</summary>
    Task<IReadOnlyList<MusicAlbumDto>> GetStarredAlbumsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes an album.</summary>
    Task DeleteAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the cover art file path for an album.</summary>
    Task<string?> GetCoverArtPathAsync(Guid albumId, CancellationToken cancellationToken = default);
}

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Client for the Cover Art Archive API (https://coverartarchive.org/).
/// </summary>
public interface ICoverArtArchiveClient
{
    /// <summary>
    /// Gets the front cover image for a specific release.
    /// </summary>
    /// <param name="releaseMbid">MusicBrainz release ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Image data and MIME type, or null if no front cover is available.</returns>
    Task<CoverArtResult?> GetFrontCoverAsync(string releaseMbid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of available cover art images for a release.
    /// </summary>
    /// <param name="releaseMbid">MusicBrainz release ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available images, or null if the request failed.</returns>
    Task<IReadOnlyList<CoverArtImage>?> GetCoverListAsync(string releaseMbid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to find front cover art by trying multiple releases in a release group.
    /// Falls back through releases until cover art is found.
    /// </summary>
    /// <param name="releases">Releases to try, in order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Image data and MIME type from the first release with art, or null if none found.</returns>
    Task<CoverArtResult?> GetFrontCoverFromReleasesAsync(IReadOnlyList<MusicBrainzRelease> releases, CancellationToken cancellationToken = default);
}

// ── Cover Art Archive response DTOs ─────────────────────────────────

/// <summary>
/// Result of a successful cover art fetch.
/// </summary>
public sealed record CoverArtResult
{
    /// <summary>Raw image data.</summary>
    public required byte[] Data { get; init; }

    /// <summary>MIME type of the image (e.g. "image/jpeg", "image/png").</summary>
    public required string MimeType { get; init; }

    /// <summary>MusicBrainz release ID this art came from.</summary>
    public required string ReleaseMbid { get; init; }
}

/// <summary>
/// Metadata about a single cover art image from the Cover Art Archive.
/// </summary>
public sealed record CoverArtImage
{
    /// <summary>Image ID.</summary>
    public long Id { get; init; }

    /// <summary>Image types (e.g. "Front", "Back", "Booklet").</summary>
    public IReadOnlyList<string> Types { get; init; } = [];

    /// <summary>Whether this is the front image.</summary>
    public bool Front { get; init; }

    /// <summary>Whether this is the back image.</summary>
    public bool Back { get; init; }

    /// <summary>Full-size image URL.</summary>
    public string? Image { get; init; }
}

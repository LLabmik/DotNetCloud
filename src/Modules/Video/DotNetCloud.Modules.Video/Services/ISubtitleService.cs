using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Manages video subtitles/captions.
/// </summary>
public interface ISubtitleService
{
    /// <summary>Uploads a subtitle for a video.</summary>
    Task<SubtitleDto> UploadSubtitleAsync(Guid videoId, UploadSubtitleDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets all subtitles for a video.</summary>
    Task<IReadOnlyList<SubtitleDto>> GetSubtitlesAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a single subtitle by ID.</summary>
    Task<SubtitleDto?> GetSubtitleAsync(Guid subtitleId, CancellationToken cancellationToken = default);

    /// <summary>Gets the raw content of a subtitle file.</summary>
    Task<(string Content, string Format)?> GetSubtitleContentAsync(Guid subtitleId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a subtitle.</summary>
    Task DeleteSubtitleAsync(Guid subtitleId, CallerContext caller, CancellationToken cancellationToken = default);
}

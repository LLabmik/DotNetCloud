using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerMediaType = DotNetCloud.Core.Server.Services.MediaType;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Per-user media library configuration and folder scanning endpoints.
/// Each user can configure their own media library paths and trigger scans.
/// </summary>
[ApiController]
[Route("api/v1/media-library")]
[Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
public sealed class MediaLibraryController : ControllerBase
{
    private readonly IUserSettingsService _userSettingsService;
    private readonly MediaFolderImportService _importService;
    private readonly ILogger<MediaLibraryController> _logger;

    private const string Module = "media-library";

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaLibraryController"/> class.
    /// </summary>
    public MediaLibraryController(
        IUserSettingsService userSettingsService,
        MediaFolderImportService importService,
        ILogger<MediaLibraryController> logger)
    {
        _userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all media library paths configured for the current user.
    /// </summary>
    [HttpGet("paths")]
    public async Task<IActionResult> GetLibraryPathsAsync()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var photosSources = await MediaLibrarySourceSettings.LoadSourcesAsync(_userSettingsService, userId, "photos");
        var musicSources = await MediaLibrarySourceSettings.LoadSourcesAsync(_userSettingsService, userId, "music");
        var videoSources = await MediaLibrarySourceSettings.LoadSourcesAsync(_userSettingsService, userId, "video");

        return Ok(new
        {
            success = true,
            data = new MediaLibraryPathsDto
            {
                PhotosPath = photosSources.FirstOrDefault()?.DisplayPath ?? string.Empty,
                MusicPath = musicSources.FirstOrDefault()?.DisplayPath ?? string.Empty,
                VideoPath = videoSources.FirstOrDefault()?.DisplayPath ?? string.Empty,
                PhotosSources = photosSources,
                MusicSources = musicSources,
                VideoSources = videoSources,
            }
        });
    }

    /// <summary>
    /// Updates media library paths for the current user.
    /// </summary>
    [HttpPut("paths")]
    public async Task<IActionResult> UpdateLibraryPathsAsync([FromBody] MediaLibraryPathsDto dto)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        // Validate paths — virtual Files module paths (or empty to clear)
        // No filesystem validation needed; paths are virtual folder names

        await _userSettingsService.UpsertSettingAsync(userId, Module, "photos-path",
            new UpsertUserSettingDto { Value = dto.PhotosPath?.Trim() ?? string.Empty, Description = "Photos library folder path" });
        await _userSettingsService.UpsertSettingAsync(userId, Module, "music-path",
            new UpsertUserSettingDto { Value = dto.MusicPath?.Trim() ?? string.Empty, Description = "Music library folder path" });
        await _userSettingsService.UpsertSettingAsync(userId, Module, "video-path",
            new UpsertUserSettingDto { Value = dto.VideoPath?.Trim() ?? string.Empty, Description = "Video library folder path" });

        _logger.LogInformation("User {UserId} updated media library paths", userId);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Triggers a scan/import of a specific media library for the current user.
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> ScanLibraryAsync([FromBody] MediaLibraryScanRequestDto request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        if (!Enum.TryParse<ServerMediaType>(request.MediaType, ignoreCase: true, out var mediaType) ||
            mediaType == ServerMediaType.All)
        {
            return BadRequest(new { success = false, error = new { code = "INVALID_TYPE", message = "Specify Photos, Music, or Video media type" } });
        }

        var sources = await MediaLibrarySourceSettings.LoadSourcesAsync(_userSettingsService, userId, request.MediaType);
        if (sources.Count == 0)
        {
            return BadRequest(new { success = false, error = new { code = "NO_SOURCES", message = $"No {request.MediaType} library sources configured. Add one first." } });
        }

        _logger.LogInformation("User {UserId} triggered {MediaType} library scan across {SourceCount} configured sources",
            userId, mediaType, sources.Count);

        var result = await _importService.ScanSourcesAsync(sources, userId, request.MediaType, progress: null);

        return Ok(new { success = true, data = result });
    }

    private static void ValidatePath(string label, string? path, List<string> errors)
    {
        // Virtual folder paths — no filesystem validation needed
        // Paths like "/Photos" or "/Music/Library" are valid
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var claimValue = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return claimValue is not null && Guid.TryParse(claimValue, out userId);
    }
}

/// <summary>
/// Media library directory paths per media type.
/// </summary>
public sealed class MediaLibraryPathsDto
{
    /// <summary>Path to photos directory.</summary>
    public string PhotosPath { get; set; } = string.Empty;

    /// <summary>Path to music directory.</summary>
    public string MusicPath { get; set; } = string.Empty;

    /// <summary>Path to video directory.</summary>
    public string VideoPath { get; set; } = string.Empty;

    /// <summary>Configured photo-library sources.</summary>
    public IReadOnlyList<MediaLibrarySource> PhotosSources { get; set; } = [];

    /// <summary>Configured music-library sources.</summary>
    public IReadOnlyList<MediaLibrarySource> MusicSources { get; set; } = [];

    /// <summary>Configured video-library sources.</summary>
    public IReadOnlyList<MediaLibrarySource> VideoSources { get; set; } = [];
}

/// <summary>
/// Request to trigger a media library scan.
/// </summary>
public sealed class MediaLibraryScanRequestDto
{
    /// <summary>Type of media to scan for: "Photos", "Music", or "Video".</summary>
    public string MediaType { get; set; } = string.Empty;
}

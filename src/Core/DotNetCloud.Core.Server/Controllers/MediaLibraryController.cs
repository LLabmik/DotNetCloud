using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        var photosPath = await _userSettingsService.GetSettingAsync(userId, Module, "photos-path");
        var musicPath = await _userSettingsService.GetSettingAsync(userId, Module, "music-path");
        var videoPath = await _userSettingsService.GetSettingAsync(userId, Module, "video-path");

        return Ok(new
        {
            success = true,
            data = new MediaLibraryPathsDto
            {
                PhotosPath = photosPath?.Value ?? string.Empty,
                MusicPath = musicPath?.Value ?? string.Empty,
                VideoPath = videoPath?.Value ?? string.Empty,
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

        // Validate paths — must be absolute and exist on disk (or be empty to clear)
        var errors = new List<string>();
        ValidatePath("Photos", dto.PhotosPath, errors);
        ValidatePath("Music", dto.MusicPath, errors);
        ValidatePath("Video", dto.VideoPath, errors);

        if (errors.Count > 0)
        {
            return BadRequest(new { success = false, error = new { code = "INVALID_PATHS", message = string.Join("; ", errors) } });
        }

        await _userSettingsService.UpsertSettingAsync(userId, Module, "photos-path",
            new UpsertUserSettingDto { Value = dto.PhotosPath?.Trim() ?? string.Empty, Description = "Photos library directory path" });
        await _userSettingsService.UpsertSettingAsync(userId, Module, "music-path",
            new UpsertUserSettingDto { Value = dto.MusicPath?.Trim() ?? string.Empty, Description = "Music library directory path" });
        await _userSettingsService.UpsertSettingAsync(userId, Module, "video-path",
            new UpsertUserSettingDto { Value = dto.VideoPath?.Trim() ?? string.Empty, Description = "Video library directory path" });

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

        if (!Enum.TryParse<MediaType>(request.MediaType, ignoreCase: true, out var mediaType) ||
            mediaType == MediaType.All)
        {
            return BadRequest(new { success = false, error = new { code = "INVALID_TYPE", message = "Specify Photos, Music, or Video media type" } });
        }

        var settingKey = mediaType switch
        {
            MediaType.Photos => "photos-path",
            MediaType.Music => "music-path",
            MediaType.Video => "video-path",
            _ => throw new InvalidOperationException("Unreachable")
        };

        var pathSetting = await _userSettingsService.GetSettingAsync(userId, Module, settingKey);
        var directoryPath = pathSetting?.Value;

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return BadRequest(new { success = false, error = new { code = "NO_PATH", message = $"No {request.MediaType} library path configured. Set one first." } });
        }

        if (!Directory.Exists(directoryPath))
        {
            return BadRequest(new { success = false, error = new { code = "PATH_NOT_FOUND", message = $"Directory does not exist: {directoryPath}" } });
        }

        _logger.LogInformation("User {UserId} triggered {MediaType} library scan of {Path}",
            userId, mediaType, directoryPath);

        var result = await _importService.ScanAndImportAsync(directoryPath, userId, mediaType);

        return Ok(new { success = true, data = result });
    }

    private static void ValidatePath(string label, string? path, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(path)) return; // Empty means "not configured" — that's fine

        var trimmed = path.Trim();
        if (!Path.IsPathRooted(trimmed))
        {
            errors.Add($"{label} path must be an absolute path");
        }
        else if (!Directory.Exists(trimmed))
        {
            errors.Add($"{label} path does not exist: {trimmed}");
        }
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
}

/// <summary>
/// Request to trigger a media library scan.
/// </summary>
public sealed class MediaLibraryScanRequestDto
{
    /// <summary>Type of media to scan for: "Photos", "Music", or "Video".</summary>
    public string MediaType { get; set; } = string.Empty;
}

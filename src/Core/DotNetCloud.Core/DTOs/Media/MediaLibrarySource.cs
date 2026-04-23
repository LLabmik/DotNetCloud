using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.DTOs.Media
{
    /// <summary>
    /// Identifies the backing kind for a persisted media-library scan source.
    /// </summary>
    public enum MediaLibrarySourceKind
    {
        /// <summary>
        /// A regular user-owned Files folder.
        /// </summary>
        OwnedFileNode,

        /// <summary>
        /// An admin shared-folder mount exposed under <c>_DotNetCloud</c>.
        /// </summary>
        SharedMount,
    }

    /// <summary>
    /// Persists one media-library scan source for a user and module.
    /// </summary>
    public sealed class MediaLibrarySource
    {
        /// <summary>
        /// Gets the persisted source kind.
        /// </summary>
        public required MediaLibrarySourceKind SourceKind { get; init; }

        /// <summary>
        /// Gets the concrete Files folder identifier for owned folders.
        /// Null means the caller's owned root should be scanned.
        /// </summary>
        public Guid? FolderId { get; init; }

        /// <summary>
        /// Gets the admin shared-folder definition identifier for shared mounts.
        /// </summary>
        public Guid? SharedFolderId { get; init; }

        /// <summary>
        /// Gets the relative path inside the admin shared-folder source.
        /// Empty or null means the shared-folder root.
        /// </summary>
        public string? RelativePath { get; init; }

        /// <summary>
        /// Gets the user-facing display path captured when the source was selected.
        /// </summary>
        public string DisplayPath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the display name shown in source pickers.
        /// </summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the source should participate in scans.
        /// </summary>
        public bool Enabled { get; init; } = true;

        /// <summary>
        /// Gets the timestamp of the last successful scan for this source.
        /// </summary>
        public DateTime? LastScannedAtUtc { get; init; }
    }
}

namespace DotNetCloud.Core.Services
{
using DotNetCloud.Core.DTOs.Media;

/// <summary>
/// Serializes and deserializes per-module media-library source settings.
/// </summary>
public static class MediaLibrarySourceSettings
{
    /// <summary>
    /// The settings module used to persist media-library preferences.
    /// </summary>
    public const string SettingsModule = "media-library";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Gets the JSON-backed settings key for the requested media type.
    /// </summary>
    public static string GetSourcesKey(string mediaType)
        => $"{NormalizeMediaType(mediaType)}-sources";

    /// <summary>
    /// Gets the legacy single-path settings key for the requested media type.
    /// </summary>
    public static string GetLegacyPathKey(string mediaType)
        => $"{NormalizeMediaType(mediaType)}-path";

    /// <summary>
    /// Gets the legacy single-folder identifier settings key for the requested media type.
    /// </summary>
    public static string GetLegacyFolderIdKey(string mediaType)
        => $"{NormalizeMediaType(mediaType)}-folder-id";

    /// <summary>
    /// Loads persisted scan sources for a user and media module.
    /// Falls back to the legacy single-folder settings when needed.
    /// </summary>
    public static async Task<IReadOnlyList<MediaLibrarySource>> LoadSourcesAsync(
        IUserSettingsService settingsService,
        Guid userId,
        string mediaType)
    {
        ArgumentNullException.ThrowIfNull(settingsService);

        var sourcesSetting = await settingsService.GetSettingAsync(userId, SettingsModule, GetSourcesKey(mediaType));
        var sources = Deserialize(sourcesSetting?.Value);
        if (sources.Count > 0)
        {
            return sources;
        }

        var pathSetting = await settingsService.GetSettingAsync(userId, SettingsModule, GetLegacyPathKey(mediaType));
        var folderIdSetting = await settingsService.GetSettingAsync(userId, SettingsModule, GetLegacyFolderIdKey(mediaType));
        var legacySource = TryCreateLegacySource(pathSetting?.Value, folderIdSetting?.Value);

        return legacySource is null ? [] : [legacySource];
    }

    /// <summary>
    /// Persists the provided scan sources for a user and media module.
    /// </summary>
    public static async Task SaveSourcesAsync(
        IUserSettingsService settingsService,
        Guid userId,
        string mediaType,
        IReadOnlyList<MediaLibrarySource> sources,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(sources);

        var normalized = Normalize(sources);
        await settingsService.UpsertSettingAsync(
            userId,
            SettingsModule,
            GetSourcesKey(mediaType),
            new UpsertUserSettingDto
            {
                Value = Serialize(normalized),
                Description = description ?? $"{mediaType} library scan sources"
            });
    }

    /// <summary>
    /// Deserializes a persisted media-library source list.
    /// </summary>
    public static IReadOnlyList<MediaLibrarySource> Deserialize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            var sources = JsonSerializer.Deserialize<List<MediaLibrarySource>>(value, SerializerOptions);
            return sources is null ? [] : Normalize(sources);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Serializes a media-library source list.
    /// </summary>
    public static string Serialize(IReadOnlyList<MediaLibrarySource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return JsonSerializer.Serialize(Normalize(sources), SerializerOptions);
    }

    /// <summary>
    /// Produces a stable identity key for a persisted source.
    /// </summary>
    public static string GetSourceKey(MediaLibrarySource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.SourceKind switch
        {
            MediaLibrarySourceKind.OwnedFileNode => $"owned:{source.FolderId?.ToString() ?? "root"}",
            MediaLibrarySourceKind.SharedMount => $"shared:{source.SharedFolderId}:{NormalizeRelativePath(source.RelativePath)}",
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
    }

    /// <summary>
    /// Normalizes a media-library source list by deduplicating and trimming persisted values.
    /// </summary>
    public static IReadOnlyList<MediaLibrarySource> Normalize(IReadOnlyList<MediaLibrarySource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var byKey = new Dictionary<string, MediaLibrarySource>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!IsValid(source))
            {
                continue;
            }

            var normalized = NormalizeSource(source);
            byKey[GetSourceKey(normalized)] = normalized;
        }

        return byKey.Values
            .OrderBy(source => source.DisplayPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static MediaLibrarySource? TryCreateLegacySource(string? path, string? folderId)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmedPath = path.Trim();
        if (Guid.TryParse(folderId, out var parsedFolderId))
        {
            return new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                FolderId = parsedFolderId,
                DisplayPath = trimmedPath,
                DisplayName = GetDisplayName(trimmedPath),
                Enabled = true,
            };
        }

        if (string.Equals(trimmedPath, "/", StringComparison.Ordinal))
        {
            return new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                FolderId = null,
                DisplayPath = trimmedPath,
                DisplayName = "Home",
                Enabled = true,
            };
        }

        return null;
    }

    private static MediaLibrarySource NormalizeSource(MediaLibrarySource source)
    {
        var displayPath = string.IsNullOrWhiteSpace(source.DisplayPath)
            ? "/"
            : source.DisplayPath.Trim();
        var displayName = string.IsNullOrWhiteSpace(source.DisplayName)
            ? GetDisplayName(displayPath)
            : source.DisplayName.Trim();

        return new MediaLibrarySource
        {
            SourceKind = source.SourceKind,
            FolderId = source.FolderId,
            SharedFolderId = source.SharedFolderId,
            RelativePath = NormalizeRelativePath(source.RelativePath),
            DisplayPath = displayPath,
            DisplayName = displayName,
            Enabled = source.Enabled,
            LastScannedAtUtc = source.LastScannedAtUtc,
        };
    }

    private static bool IsValid(MediaLibrarySource source)
    {
        return source.SourceKind switch
        {
            MediaLibrarySourceKind.OwnedFileNode => source.FolderId.HasValue || string.Equals(source.DisplayPath, "/", StringComparison.Ordinal),
            MediaLibrarySourceKind.SharedMount => source.SharedFolderId.HasValue,
            _ => false
        };
    }

    private static string NormalizeMediaType(string mediaType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        return mediaType.Trim().ToLowerInvariant();
    }

    private static string NormalizeRelativePath(string? relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : relativePath.Replace('\\', '/').Trim('/');
    }

    private static string GetDisplayName(string displayPath)
    {
        if (string.IsNullOrWhiteSpace(displayPath) || string.Equals(displayPath, "/", StringComparison.Ordinal))
        {
            return "Home";
        }

        var trimmed = displayPath.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        return lastSlash >= 0 ? trimmed[(lastSlash + 1)..] : trimmed;
    }
}
}
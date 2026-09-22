using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Local file system implementation of <see cref="IChatImageStore"/>.
/// Saves images under {StoragePath}/chat-uploads/.
/// </summary>
public sealed class LocalChatImageStore : IChatImageStore
{
    /// <summary>Limits used when no <see cref="IChatSettingsProvider"/> is available.</summary>
    private static readonly ChatSettings FallbackSettings = new ChatSettings().Normalized();

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml",
        "image/bmp", "image/tiff", "image/x-icon", "image/heic"
    };

    private static readonly Dictionary<string, string> MimeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
        ["image/svg+xml"] = ".svg",
        ["image/bmp"] = ".bmp",
        ["image/tiff"] = ".tiff",
        ["image/x-icon"] = ".ico",
        ["image/heic"] = ".heic"
    };

    private readonly string _uploadDir;
    private readonly ILogger<LocalChatImageStore> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalChatImageStore"/> class.
    /// </summary>
    /// <param name="configuration">Configuration source for the storage root path.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="scopeFactory">
    /// Optional scope factory used to resolve <see cref="IChatSettingsProvider"/> so the
    /// administrator-configured attachment size limit applies without a restart. A scope is used
    /// because this store is a singleton while the settings provider is scoped, so resolving it
    /// from the root provider would trip DI scope validation. When absent the built-in default
    /// limit is used.
    /// </param>
    public LocalChatImageStore(
        IConfiguration configuration,
        ILogger<LocalChatImageStore> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;

        var storagePath = configuration.GetValue<string>("Files:Storage:RootPath");
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            storagePath = Path.Combine(Directory.GetCurrentDirectory(), "storage");
        }

        _uploadDir = Path.Combine(storagePath, "chat-uploads");
        Directory.CreateDirectory(_uploadDir);
    }

    /// <inheritdoc />
    public async Task<ChatImageUploadResult> SaveAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken = default)
    {
        if (data.Length == 0)
            throw new ArgumentException("Image data is empty.", nameof(data));

        var settings = await ResolveSettingsAsync(cancellationToken);
        if (data.Length > settings.MaxAttachmentSizeBytes)
        {
            throw new ArgumentException(
                $"Image exceeds maximum size of {settings.MaxAttachmentSizeMb} MB.", nameof(data));
        }

        var normalizedMime = NormalizeMimeType(contentType, fileName);
        if (!AllowedMimeTypes.Contains(normalizedMime))
            throw new ArgumentException($"Unsupported image type: {contentType}", nameof(contentType));

        var ext = MimeToExtension.GetValueOrDefault(normalizedMime, ".png");
        var storedName = $"{Guid.CreateVersion7():N}{ext}";
        var fullPath = Path.Combine(_uploadDir, storedName);

        await File.WriteAllBytesAsync(fullPath, data, cancellationToken);

        var url = $"/api/v1/chat/uploads/{storedName}";

        _logger.LogInformation("Chat image saved: {FileName} -> {StoredName} ({Size} bytes)", fileName, storedName, data.Length);

        return new ChatImageUploadResult
        {
            StoredFileName = storedName,
            Url = url,
            ContentType = normalizedMime,
            FileSize = data.Length
        };
    }

    /// <summary>
    /// Resolves the administrator-configured attachment limits, falling back to the built-in
    /// defaults when no settings provider is available.
    /// </summary>
    private async Task<ChatSettings> ResolveSettingsAsync(CancellationToken cancellationToken)
    {
        if (_scopeFactory is null)
            return FallbackSettings;

        using var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetService(typeof(IChatSettingsProvider)) is IChatSettingsProvider provider
            ? await provider.GetSettingsAsync(cancellationToken)
            : FallbackSettings;
    }

    /// <inheritdoc />
    public Task<ChatImageFile?> GetAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        // Prevent path traversal: only allow simple filenames
        if (string.IsNullOrWhiteSpace(storedFileName)
            || storedFileName.Contains('/')
            || storedFileName.Contains('\\')
            || storedFileName.Contains("..")
            || storedFileName != Path.GetFileName(storedFileName))
        {
            return Task.FromResult<ChatImageFile?>(null);
        }

        var fullPath = Path.Combine(_uploadDir, storedFileName);
        if (!File.Exists(fullPath))
            return Task.FromResult<ChatImageFile?>(null);

        var ext = Path.GetExtension(storedFileName).ToLowerInvariant();
        var mime = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".tiff" => "image/tiff",
            ".ico" => "image/x-icon",
            ".heic" => "image/heic",
            _ => "application/octet-stream"
        };

        var data = File.ReadAllBytes(fullPath);
        return Task.FromResult<ChatImageFile?>(new ChatImageFile
        {
            Data = data,
            ContentType = mime
        });
    }

    private static string NormalizeMimeType(string contentType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && AllowedMimeTypes.Contains(contentType))
            return contentType;

        // Fall back to extension-based detection
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".tiff" => "image/tiff",
            ".ico" => "image/x-icon",
            ".heic" => "image/heic",
            _ => contentType
        };
    }
}

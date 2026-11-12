using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Photos.Models;
using DotNetCloud.Modules.Photos.Services;
using DotNetCloud.Modules.Sdk.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Generates and stores photo thumbnails (grid 300px, detail 1200px) in the database
/// using ImageSharp. Full-size requests return null (caller uses the original file).
/// </summary>
/// <remarks>
/// A sandboxed module never touches the Files module or its storage: the original bytes are fetched
/// through the core's module API (<see cref="IDncModuleApi.DownloadFileAsync"/>, capability
/// <c>IFileDirectory</c>, per-user consent <c>files:read</c>), which resolves the acting user from the
/// core-signed assertion attached by the reverse proxy. Thumbnails are produced on first request
/// instead of by a background worker, because a background thread has no acting user to assert.
/// </remarks>
public sealed class PhotoThumbnailService : IPhotoThumbnailService
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif",
        "image/webp", "image/bmp", "image/tiff"
    };

    private readonly PhotosDbContext _db;
    private readonly IDncModuleApi? _coreApi;
    private readonly ILogger<PhotoThumbnailService> _logger;
    private readonly int _jpegQuality;
    private readonly int _detailMaxDimension;
    private readonly int _gridMaxDimension;

    /// <summary>
    /// Initializes the photo thumbnail service.
    /// </summary>
    /// <param name="db">Photos database context.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="configuration">
    /// Optional configuration. Reads <c>Photos:Thumbnails:*</c>; when absent the defaults below apply.
    /// </param>
    /// <param name="coreApi">
    /// The core module API used to read the original image, or <see langword="null"/> when the host
    /// was not launched by the supervisor (a local <c>dotnet run</c>): cached thumbnails still serve,
    /// generation answers "unavailable" instead of failing.
    /// </param>
    public PhotoThumbnailService(
        PhotosDbContext db,
        ILogger<PhotoThumbnailService> logger,
        IConfiguration? configuration = null,
        IDncModuleApi? coreApi = null)
    {
        _db = db;
        _coreApi = coreApi;
        _logger = logger;

        // Thumbnail payload and cost are dominated by the full-resolution resize and the JPEG
        // encoder. Both are configurable so a deployment can trade quality for storage/CPU.
        _jpegQuality = ReadInt(configuration, "Photos:Thumbnails:JpegQuality", 80, 1, 100);
        _detailMaxDimension = ReadInt(
            configuration, "Photos:Thumbnails:DetailMaxDimension", (int)PhotoThumbnailSize.Detail, 256, 4096);
        _gridMaxDimension = ReadInt(
            configuration, "Photos:Thumbnails:GridMaxDimension", (int)PhotoThumbnailSize.Grid, 64, 1024);
    }

    private static int ReadInt(IConfiguration? configuration, string key, int defaultValue, int min, int max)
    {
        if (configuration is null || !int.TryParse(configuration[key], out var value))
        {
            return defaultValue;
        }

        return Math.Clamp(value, min, max);
    }

    /// <inheritdoc />
    public async Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
        Guid photoId,
        Guid actingUserId,
        PhotoThumbnailSize size,
        CancellationToken cancellationToken = default)
    {
        if (size == PhotoThumbnailSize.Full)
        {
            return (null, null);
        }

        var data = await ReadThumbnailAsync(photoId, size, cancellationToken);

        // First view of a newly indexed photo: produce the cache from the original while an acting
        // user is asserting (a background worker has no user to assert, which is why generation
        // happens here rather than when the row is indexed).
        if (data is null || data.Length == 0)
        {
            if (!await GenerateThumbnailsAsync(photoId, actingUserId, cancellationToken))
            {
                return (null, null);
            }

            data = await ReadThumbnailAsync(photoId, size, cancellationToken);
        }

        if (data is null || data.Length == 0)
        {
            return (null, null);
        }

        Stream stream = new MemoryStream(data, writable: false);
        return (stream, "image/jpeg");
    }

    /// <summary>
    /// Reads one cached thumbnail column without loading the other blob.
    /// </summary>
    private Task<byte[]?> ReadThumbnailAsync(
        Guid photoId,
        PhotoThumbnailSize size,
        CancellationToken cancellationToken) =>
        size == PhotoThumbnailSize.Grid
            ? _db.Photos.IgnoreQueryFilters()
                .Where(p => p.Id == photoId)
                .Select(p => p.ThumbnailGrid)
                .FirstOrDefaultAsync(cancellationToken)
            : _db.Photos.IgnoreQueryFilters()
                .Where(p => p.Id == photoId)
                .Select(p => p.ThumbnailDetail)
                .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> GenerateThumbnailsAsync(
        Guid photoId,
        Guid actingUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var photo = await _db.Photos.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);

            if (photo is null)
            {
                _logger.LogWarning("Photo {PhotoId} not found for thumbnail generation", photoId);
                return false;
            }

            // Only the owner may read the original: the download runs under the caller's own
            // assertion, so a user the photo is merely shared with cannot reach the owner's file.
            if (photo.OwnerId != actingUserId)
            {
                _logger.LogDebug("Thumbnail generation refused for photo {PhotoId}: caller is not the owner", photoId);
                return false;
            }

            if (!SupportedMimeTypes.Contains(photo.MimeType))
            {
                _logger.LogDebug("Unsupported MIME type for photo thumbnail: {MimeType}", photo.MimeType);
                return false;
            }

            // Rescans and duplicate index events call this for photos that are already done.
            // Generating again costs a full-resolution decode + resize for no benefit.
            if (photo.ThumbnailGrid is { Length: > 0 } && photo.ThumbnailDetail is { Length: > 0 })
            {
                _logger.LogDebug("Photo {PhotoId} already has thumbnails — skipping generation", photoId);
                return true;
            }

            using var sourceStream = await OpenSourceStreamAsync(photo, cancellationToken);
            if (sourceStream is null)
            {
                return false;
            }

            using var image = await Image.LoadAsync(sourceStream, cancellationToken);

            // One full-resolution resize produces the detail thumbnail; the grid is derived from
            // that result instead of re-resizing the original a second time.
            using var detailImage = ResizeImage(image, _detailMaxDimension);
            var detailData = await EncodeJpegAsync(detailImage, cancellationToken);

            using var gridImage = ResizeImage(detailImage, _gridMaxDimension);
            var gridData = await EncodeJpegAsync(gridImage, cancellationToken);

            photo.ThumbnailGrid = gridData;
            photo.ThumbnailDetail = detailData;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Photo thumbnails generated and stored in DB for {PhotoId} (grid={GridSize}b, detail={DetailSize}b)",
                photoId, gridData.Length, detailData.Length);
            return true;
        }
        catch (FilesConsentRequiredException)
        {
            // A missing consent is the user's to grant, not a generation failure (see OpenSourceStreamAsync).
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnails for photo {PhotoId}", photoId);
            return false;
        }
    }

    /// <summary>
    /// Opens the photo's original bytes through the core.
    /// </summary>
    /// <remarks>
    /// The module never reaches the Files module or its storage: the core brokers the read for the
    /// acting user (capability <c>IFileDirectory</c>, consent <c>files:read</c>). A host that was not
    /// launched by the supervisor has no core API, so the result is "unavailable" rather than an
    /// exception.
    /// </remarks>
    private async Task<Stream?> OpenSourceStreamAsync(Photo photo, CancellationToken cancellationToken)
    {
        if (_coreApi is null)
        {
            _logger.LogWarning(
                "Cannot read the original of photo {PhotoId}: the core module API is not available " +
                "(the host was not launched by the supervisor).",
                photo.Id);
            return null;
        }

        try
        {
            return await _coreApi.DownloadFileAsync(photo.FileNodeId.ToString(), cancellationToken);
        }
        catch (Grpc.Core.RpcException ex) when (IsConsentRequired(ex))
        {
            // A consent refusal is not a module failure: the user simply has not approved `files:read`.
            // Surface it as a 403 the UI can act on (it asks the shell for consent) instead of letting
            // the caller's catch-all turn it into "could not read the original".
            _logger.LogInformation(
                "Reading the original of photo {PhotoId} needs the files:read consent.",
                photo.Id);

            throw new FilesConsentRequiredException(
                "Reading this photo's original needs permission to use your files.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read the original of photo {PhotoId} (FileNode {FileNodeId}) through the core",
                photo.Id, photo.FileNodeId);
            return null;
        }
    }

    /// <summary>True when the core refused the call because the user has not consented.</summary>
    private static bool IsConsentRequired(Grpc.Core.RpcException exception) =>
        exception.StatusCode == Grpc.Core.StatusCode.PermissionDenied
        && (string.Equals(exception.Trailers?.GetValue("error-code"), ErrorCodes.ModuleConsentRequired, StringComparison.Ordinal)
            || (exception.Status.Detail?.Contains(ErrorCodes.ModuleConsentRequired, StringComparison.Ordinal) ?? false));

    /// <inheritdoc />
    public async Task DeleteThumbnailsAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        var photo = await _db.Photos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);

        if (photo is not null)
        {
            photo.ThumbnailGrid = null;
            photo.ThumbnailDetail = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogDebug("Photo thumbnails deleted for {PhotoId}", photoId);
    }

    private static Image ResizeImage(Image source, int maxDimension)
    {
        if (source.Width <= maxDimension && source.Height <= maxDimension)
        {
            return source.Clone(_ => { });
        }

        return source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(maxDimension, maxDimension),
            Mode = ResizeMode.Max
        }));
    }

    private async Task<byte[]> EncodeJpegAsync(Image image, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = _jpegQuality }, cancellationToken);
        return ms.ToArray();
    }

    /// <inheritdoc />
    public async Task<bool> SaveEditsAsync(Guid photoId, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Look up the photo to get FileNodeId + MimeType
            var photo = await _db.Photos.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);

            if (photo is null)
            {
                _logger.LogWarning("SaveEdits: Photo {PhotoId} not found", photoId);
                return false;
            }

            // Only the owner may rewrite the thumbnail cache from the original.
            if (photo.OwnerId != actingUserId)
            {
                _logger.LogDebug("SaveEdits: caller is not the owner of photo {PhotoId}", photoId);
                return false;
            }

            if (!SupportedMimeTypes.Contains(photo.MimeType))
            {
                _logger.LogWarning("SaveEdits: Unsupported MIME type {MimeType} for photo {PhotoId}", photo.MimeType, photoId);
                return false;
            }

            // 2. Load the edit stack
            var editRecords = await _db.PhotoEditRecords
                .Where(e => e.PhotoId == photoId)
                .OrderBy(e => e.StackOrder)
                .ToListAsync(cancellationToken);

            if (editRecords.Count == 0)
            {
                // Revert-all and undo arrive here with an empty stack: the previews still have to be
                // rebuilt, because earlier "save" baked them and the point of the revert is to undo
                // that. An empty stack therefore renders the untouched original.
                _logger.LogDebug("SaveEdits: no queued edits for photo {PhotoId}; re-rendering the original", photoId);
            }

            // 3. Read the original through the core (the module has no filesystem access to it).
            using var sourceStream = await OpenSourceStreamAsync(photo, cancellationToken);
            if (sourceStream is null)
            {
                _logger.LogWarning("SaveEdits: the original of photo {PhotoId} could not be read", photoId);
                return false;
            }

            using var image = await Image.LoadAsync(sourceStream, cancellationToken);

            // 4. Apply each edit operation to the image
            foreach (var record in editRecords)
            {
                if (!Enum.TryParse<PhotoEditType>(record.OperationType, out var editType))
                    continue;

                var parameters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(record.ParametersJson)
                    ?? new Dictionary<string, string>();

                ApplyEditToImage(image, editType, parameters);
            }

            // 5. Regenerate thumbnails from the edited image. The grid is derived from the
            //    detail result so only one full-resolution resize is ever performed.
            using var detailImage = ResizeImage(image, _detailMaxDimension);
            var detailData = await EncodeJpegAsync(detailImage, cancellationToken);

            byte[] gridData;
            using (var gridImage = ResizeImage(detailImage, _gridMaxDimension))
            {
                gridData = await EncodeJpegAsync(gridImage, cancellationToken);
            }

            photo.ThumbnailGrid = gridData;
            photo.ThumbnailDetail = detailData;
            photo.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("SaveEdits: Thumbnails regenerated with {EditCount} edits for photo {PhotoId}",
                editRecords.Count, photoId);
            return true;
        }
        catch (FilesConsentRequiredException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveEdits: Failed to save edits for photo {PhotoId}", photoId);
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The same operation as <see cref="SaveEditsAsync"/> - render the current edit stack onto the
    /// previews, with an empty stack rendering the unedited original. This name exists because "save"
    /// reads wrong at the undo/revert call sites.
    /// </remarks>
    public Task<bool> RegeneratePreviewsAsync(Guid photoId, Guid actingUserId, CancellationToken cancellationToken = default)
        => SaveEditsAsync(photoId, actingUserId, cancellationToken);

    private static void ApplyEditToImage(Image image, PhotoEditType editType, Dictionary<string, string> parameters)
    {
        switch (editType)
        {
            case PhotoEditType.Rotate:
                if (TryReadInt(parameters, "value", out var degrees))
                {
                    image.Mutate(ctx => ctx.Rotate(degrees));
                }

                break;

            case PhotoEditType.Flip:
                if (TryReadInt(parameters, "value", out var flip))
                {
                    image.Mutate(ctx => ctx.Flip(flip == 0 ? FlipMode.Horizontal : FlipMode.Vertical));
                }

                break;

            case PhotoEditType.Brightness:
            case PhotoEditType.Contrast:
            case PhotoEditType.Saturation:
                // The API takes -100..100; ImageSharp takes a multiplier where 1.0 is no change.
                if (TryReadFloat(parameters, "value", out var amount))
                {
                    var factor = 1.0f + (amount / 100f);
                    image.Mutate(ctx =>
                    {
                        switch (editType)
                        {
                            case PhotoEditType.Brightness:
                                ctx.Brightness(factor);
                                break;
                            case PhotoEditType.Contrast:
                                ctx.Contrast(factor);
                                break;
                            default:
                                ctx.Saturate(factor);
                                break;
                        }
                    });
                }

                break;

            case PhotoEditType.Blur:
            case PhotoEditType.Sharpen:
                // `radius` is what the API validator accepts; `value` is what the first UI sent.
                if (TryReadRadius(parameters, out var radius) && radius > 0)
                {
                    image.Mutate(ctx =>
                    {
                        if (editType == PhotoEditType.Blur)
                        {
                            ctx.GaussianBlur(radius);
                        }
                        else
                        {
                            ctx.GaussianSharpen(radius);
                        }
                    });
                }

                break;

            case PhotoEditType.Crop:
                ApplyCrop(image, parameters);
                break;
        }
    }

    /// <summary>
    /// Crops to the recorded rectangle, clamped to the image it is applied to.
    /// </summary>
    /// <remarks>
    /// The rectangle is recorded in original pixels, but earlier operations in the stack (a rotate, or
    /// another crop) change the frame it refers to. Clamping turns a stale rectangle into the largest
    /// legal crop instead of throwing and losing the whole stack - which is why the old pipeline, whose
    /// single int gate skipped Crop entirely, could never apply one.
    /// </remarks>
    private static void ApplyCrop(Image image, Dictionary<string, string> parameters)
    {
        if (!TryReadInt(parameters, "x", out var x)
            || !TryReadInt(parameters, "y", out var y)
            || !TryReadInt(parameters, "width", out var width)
            || !TryReadInt(parameters, "height", out var height))
        {
            return;
        }

        x = Math.Clamp(x, 0, image.Width - 1);
        y = Math.Clamp(y, 0, image.Height - 1);
        width = Math.Clamp(width, 1, image.Width - x);
        height = Math.Clamp(height, 1, image.Height - y);

        image.Mutate(ctx => ctx.Crop(new Rectangle(x, y, width, height)));
    }

    private static bool TryReadInt(Dictionary<string, string> parameters, string key, out int value)
    {
        value = 0;
        return parameters.TryGetValue(key, out var raw)
            && int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadFloat(Dictionary<string, string> parameters, string key, out float value)
    {
        value = 0;
        return parameters.TryGetValue(key, out var raw)
            && float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadRadius(Dictionary<string, string> parameters, out float radius)
        => TryReadFloat(parameters, "radius", out radius) || TryReadFloat(parameters, "value", out radius);
}

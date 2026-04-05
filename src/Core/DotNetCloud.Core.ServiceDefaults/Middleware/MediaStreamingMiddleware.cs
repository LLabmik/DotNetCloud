using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.ServiceDefaults.Middleware;

/// <summary>
/// Middleware that handles HTTP Range-request streaming for media files.
/// Parses <c>Range</c> headers and produces <c>206 Partial Content</c> responses
/// with correct <c>Content-Range</c>, <c>Accept-Ranges</c>, and <c>Content-Length</c> headers.
/// </summary>
/// <remarks>
/// This middleware is mapped to a configurable route prefix (default <c>/api/media/stream</c>)
/// and delegates actual file access to a registered <see cref="IMediaStreamProvider"/>.
/// Modules register their own <see cref="IMediaStreamProvider"/> implementations to serve
/// photos, audio, and video files through the same streaming infrastructure.
/// </remarks>
public class MediaStreamingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MediaStreamingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaStreamingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger instance.</param>
    public MediaStreamingMiddleware(RequestDelegate next, ILogger<MediaStreamingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes the HTTP request, handling range-request streaming if applicable.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="streamProvider">The media stream provider for resolving file streams.</param>
    public async Task InvokeAsync(HttpContext context, IMediaStreamProvider streamProvider)
    {
        ArgumentNullException.ThrowIfNull(streamProvider);

        // Only handle GET/HEAD requests
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Extract file node ID from route
        if (!context.Request.RouteValues.TryGetValue("fileNodeId", out var rawId) ||
            !Guid.TryParse(rawId?.ToString(), out var fileNodeId))
        {
            await _next(context);
            return;
        }

        var fileInfo = await streamProvider.GetFileInfoAsync(fileNodeId, context.RequestAborted);
        if (fileInfo is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return;
        }

        // Always advertise range support
        context.Response.Headers["Accept-Ranges"] = "bytes";

        var rangeHeader = context.Request.Headers.Range.FirstOrDefault();
        var (rangeStart, rangeEnd) = ParseRangeHeader(rangeHeader, fileInfo.TotalLength);

        // Validate range
        if (rangeStart.HasValue && rangeStart.Value >= fileInfo.TotalLength)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
            context.Response.Headers["Content-Range"] = $"bytes */{fileInfo.TotalLength}";
            return;
        }

        var effectiveStart = rangeStart ?? 0;
        var effectiveEnd = rangeEnd ?? (fileInfo.TotalLength - 1);

        // Clamp end to file boundaries
        if (effectiveEnd >= fileInfo.TotalLength)
        {
            effectiveEnd = fileInfo.TotalLength - 1;
        }

        var contentLength = effectiveEnd - effectiveStart + 1;
        var isPartial = rangeStart.HasValue || rangeEnd.HasValue;

        // Set response headers
        context.Response.ContentType = fileInfo.ContentType;
        context.Response.ContentLength = contentLength;

        if (isPartial)
        {
            context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
            context.Response.Headers["Content-Range"] = $"bytes {effectiveStart}-{effectiveEnd}/{fileInfo.TotalLength}";
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
        }

        // HEAD requests don't send a body
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        // Stream the content
        await using var stream = await streamProvider.OpenReadAsync(
            fileNodeId, effectiveStart, effectiveEnd, context.RequestAborted);

        if (stream is null)
        {
            _logger.LogWarning("Stream provider returned null for file {FileNodeId}.", fileNodeId);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return;
        }

        await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    /// <summary>
    /// Parses an HTTP <c>Range</c> header value into start/end byte offsets.
    /// Supports a single byte range only (e.g. <c>bytes=0-1023</c>, <c>bytes=1024-</c>, <c>bytes=-500</c>).
    /// </summary>
    /// <param name="rangeHeader">The raw Range header value, or <c>null</c>.</param>
    /// <param name="totalLength">Total file length in bytes.</param>
    /// <returns>
    /// A tuple of (start, end) byte offsets. Both are <c>null</c> if no Range header is present
    /// or the header could not be parsed.
    /// </returns>
    internal static (long? Start, long? End) ParseRangeHeader(string? rangeHeader, long totalLength)
    {
        if (string.IsNullOrWhiteSpace(rangeHeader))
        {
            return (null, null);
        }

        // Must start with "bytes="
        const string prefix = "bytes=";
        if (!rangeHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        var rangeSpec = rangeHeader[prefix.Length..].Trim();

        // Only support single range (no multi-range)
        if (rangeSpec.Contains(',', StringComparison.Ordinal))
        {
            return (null, null);
        }

        var dashIndex = rangeSpec.IndexOf('-');
        if (dashIndex < 0)
        {
            return (null, null);
        }

        var startPart = rangeSpec[..dashIndex].Trim();
        var endPart = rangeSpec[(dashIndex + 1)..].Trim();

        // "bytes=-500" → last 500 bytes
        if (string.IsNullOrEmpty(startPart))
        {
            if (long.TryParse(endPart, NumberStyles.None, CultureInfo.InvariantCulture, out var suffixLength) && suffixLength > 0)
            {
                var start = Math.Max(0, totalLength - suffixLength);
                return (start, totalLength - 1);
            }

            return (null, null);
        }

        // "bytes=1024-" or "bytes=1024-2047"
        if (!long.TryParse(startPart, NumberStyles.None, CultureInfo.InvariantCulture, out var rangeStart))
        {
            return (null, null);
        }

        if (string.IsNullOrEmpty(endPart))
        {
            return (rangeStart, null);
        }

        if (long.TryParse(endPart, NumberStyles.None, CultureInfo.InvariantCulture, out var rangeEnd) && rangeEnd >= rangeStart)
        {
            return (rangeStart, rangeEnd);
        }

        return (null, null);
    }
}

/// <summary>
/// Provides file information and read streams for the <see cref="MediaStreamingMiddleware"/>.
/// Modules implement this interface to plug into the shared streaming infrastructure.
/// </summary>
public interface IMediaStreamProvider
{
    /// <summary>
    /// Gets basic file information for content-type and length headers.
    /// </summary>
    /// <param name="fileNodeId">The file node ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File info, or <c>null</c> if the file is not found or access is denied.</returns>
    Task<MediaFileInfo?> GetFileInfoAsync(Guid fileNodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read-only stream positioned at <paramref name="rangeStart"/>,
    /// limited to read up to <paramref name="rangeEnd"/> (inclusive).
    /// </summary>
    /// <param name="fileNodeId">The file node ID.</param>
    /// <param name="rangeStart">Start byte offset (inclusive).</param>
    /// <param name="rangeEnd">End byte offset (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only stream, or <c>null</c> if the file is not found.</returns>
    Task<Stream?> OpenReadAsync(Guid fileNodeId, long rangeStart, long rangeEnd, CancellationToken cancellationToken = default);
}

/// <summary>
/// Basic file information returned by <see cref="IMediaStreamProvider.GetFileInfoAsync"/>.
/// </summary>
public sealed record MediaFileInfo
{
    /// <summary>
    /// MIME content type of the file.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Total file size in bytes.
    /// </summary>
    public required long TotalLength { get; init; }

    /// <summary>
    /// Original file name (used for <c>Content-Disposition</c> when downloading).
    /// </summary>
    public string? FileName { get; init; }
}

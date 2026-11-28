using DotNetCloud.Modules.Bookmarks.Models;
using DotNetCloud.Modules.Bookmarks.Services;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Bookmarks.Data.Services;

/// <summary>
/// Fetches and manages rich previews for bookmarks using AngleSharp HTML parsing.
/// </summary>
public sealed class BookmarkPreviewFetchService : IBookmarkPreviewService
{
    private readonly BookmarksDbContext _db;
    private readonly SafeUrlFetcher _urlFetcher;
    private readonly ILogger<BookmarkPreviewFetchService> _logger;

    public BookmarkPreviewFetchService(BookmarksDbContext db, SafeUrlFetcher urlFetcher, ILogger<BookmarkPreviewFetchService> logger)
    {
        _db = db;
        _urlFetcher = urlFetcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BookmarkPreview> FetchPreviewAsync(Guid bookmarkId, CancellationToken ct = default)
    {
        var bookmark = await _db.Bookmarks.FindAsync(new object[] { bookmarkId }, ct)
            ?? throw new InvalidOperationException($"Bookmark {bookmarkId} not found.");

        // Upsert preview record with Fetching status
        var preview = await _db.BookmarkPreviews
            .FirstOrDefaultAsync(p => p.BookmarkId == bookmarkId, ct);

        if (preview is null)
        {
            preview = new BookmarkPreview
            {
                BookmarkId = bookmarkId,
                Status = BookmarkPreviewStatus.Fetching,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.BookmarkPreviews.Add(preview);
        }
        else
        {
            preview.Status = BookmarkPreviewStatus.Fetching;
            preview.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        // Fetch
        if (!Uri.TryCreate(bookmark.Url, UriKind.Absolute, out var uri))
        {
            preview.Status = BookmarkPreviewStatus.Failed;
            preview.ErrorMessage = "Invalid URL.";
            preview.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return preview;
        }

        var fetchResult = await _urlFetcher.FetchAsync(uri, ct);

        if (!fetchResult.Success || fetchResult.Content is null)
        {
            preview.Status = BookmarkPreviewStatus.Failed;
            preview.ErrorMessage = fetchResult.ErrorReason ?? "Fetch failed.";
            preview.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return preview;
        }

        // Parse HTML
        try
        {
            var parser = new HtmlParser();
            using var document = await parser.ParseDocumentAsync(fetchResult.Content, ct);

            ExtractMetadata(document, preview, uri);

            preview.FetchedAt = DateTime.UtcNow;
            preview.ContentType = fetchResult.ContentType;
            preview.ContentLength = fetchResult.ContentLength;
            preview.CanonicalUrl ??= fetchResult.FinalUri;
            preview.ETag = fetchResult.ETag;
            preview.LastModified = fetchResult.LastModified;
            preview.Status = BookmarkPreviewStatus.Ok;
            preview.ErrorMessage = null;
            preview.UpdatedAt = DateTime.UtcNow;

            // Auto-update bookmark title if the user hasn't set a custom one
            if (!string.IsNullOrWhiteSpace(preview.ResolvedTitle)
                && string.IsNullOrWhiteSpace(bookmark.Title))
            {
                bookmark.Title = preview.ResolvedTitle;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse HTML for bookmark {BookmarkId}", bookmarkId);
            preview.Status = BookmarkPreviewStatus.Failed;
            preview.ErrorMessage = $"Parse error: {ex.Message}";
            preview.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return preview;
    }

    /// <inheritdoc />
    public Task<BookmarkPreview> RefreshPreviewAsync(Guid bookmarkId, CancellationToken ct = default)
        => FetchPreviewAsync(bookmarkId, ct);

    /// <inheritdoc />
    public async Task<BookmarkPreview?> GetPreviewAsync(Guid bookmarkId, CancellationToken ct = default)
    {
        return await _db.BookmarkPreviews
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.BookmarkId == bookmarkId, ct);
    }

    /// <inheritdoc />
    public async Task<int> RefreshStalePreviewsAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        var staleThreshold = DateTime.UtcNow - maxAge;
        var stalePreviews = await _db.BookmarkPreviews
            .AsNoTracking()
            .Where(p => p.Status == BookmarkPreviewStatus.Ok && p.FetchedAt < staleThreshold)
            .OrderBy(p => p.FetchedAt)
            .Take(50) // batch limit
            .ToListAsync(ct);

        var count = 0;
        foreach (var p in stalePreviews)
        {
            try
            {
                await FetchPreviewAsync(p.BookmarkId, ct);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh stale preview for bookmark {BookmarkId}", p.BookmarkId);
            }
        }

        _logger.LogInformation("Refreshed {Count} stale bookmark previews", count);
        return count;
    }

    private static void ExtractMetadata(IDocument document, BookmarkPreview preview, Uri baseUri)
    {
        // OG tags (highest priority)
        preview.SiteName = GetMetaContent(document, "og:site_name")
            ?? GetMetaContent(document, "twitter:site");
        preview.ResolvedTitle = GetMetaContent(document, "og:title")
            ?? GetMetaContent(document, "twitter:title")
            ?? document.Head?.QuerySelector("title")?.TextContent?.Trim();
        preview.ResolvedDescription = GetMetaContent(document, "og:description")
            ?? GetMetaContent(document, "twitter:description")
            ?? document.Head?.QuerySelector("meta[name='description']")?.GetAttribute("content")?.Trim();
        preview.PreviewImageUrl = GetMetaContent(document, "og:image")
            ?? GetMetaContent(document, "twitter:image")
            ?? GetMetaContent(document, "twitter:image:src");

        // Canonical URL
        preview.CanonicalUrl = document.Head?.QuerySelector("link[rel='canonical']")?.GetAttribute("href");

        // Favicon
        preview.FaviconUrl = document.Head?.QuerySelector("link[rel='icon']")?.GetAttribute("href")
            ?? document.Head?.QuerySelector("link[rel='shortcut icon']")?.GetAttribute("href")
            ?? document.Head?.QuerySelector("link[rel='apple-touch-icon']")?.GetAttribute("href")
            ?? new Uri(baseUri, "/favicon.ico").ToString();

        // Resolve relative URLs
        if (preview.FaviconUrl is not null && !Uri.IsWellFormedUriString(preview.FaviconUrl, UriKind.Absolute))
            preview.FaviconUrl = new Uri(baseUri, preview.FaviconUrl).ToString();

        if (preview.PreviewImageUrl is not null && !Uri.IsWellFormedUriString(preview.PreviewImageUrl, UriKind.Absolute))
            preview.PreviewImageUrl = new Uri(baseUri, preview.PreviewImageUrl).ToString();
    }

    private static string? GetMetaContent(IDocument document, string property)
    {
        var element = document.Head?.QuerySelector($"meta[property='{property}']")
            ?? document.Head?.QuerySelector($"meta[name='{property}']");
        return element?.GetAttribute("content")?.Trim();
    }
}

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// SSRF-safe link preview unfurling for chat messages. Detects the first http(s) URL in
/// message content, fetches the page through <see cref="SafeUrlFetcher"/>, and extracts
/// OpenGraph/Twitter/HTML metadata using AngleSharp.
/// </summary>
public sealed partial class LinkPreviewService : ILinkPreviewService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    private const int MaxCacheEntries = 512;

    private static readonly string[] MediaExtensions =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".ico", ".bmp", ".avif",
        ".mp3", ".mp4", ".webm", ".ogg", ".wav", ".mov", ".m4a", ".aac", ".flac"
    ];

    // Matches a bare http(s) URL, excluding surrounding whitespace and common trailing punctuation.
    [GeneratedRegex(@"(?:https?://)(?:www\.)?[^\s<>""')\]]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BareUrlRegex();

    private readonly SafeUrlFetcher _fetcher;
    private readonly ILogger<LinkPreviewService> _logger;

    // Small bounded in-memory cache keyed by normalized URL to avoid re-fetching links that
    // are shared repeatedly in a chat. TTL-bounded; entries evicted by age on read.
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkPreviewService"/> class.
    /// </summary>
    public LinkPreviewService(SafeUrlFetcher fetcher, ILogger<LinkPreviewService> logger)
    {
        _fetcher = fetcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public Uri? FindFirstUrl(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        // Track fenced code blocks across lines so URLs inside them are never previewed.
        var inFence = false;

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence)
            {
                continue;
            }

            // Skip indented code blocks (4+ spaces or a tab).
            if (line.StartsWith("    ", StringComparison.Ordinal) || line.StartsWith('\t'))
            {
                continue;
            }

            foreach (Match match in BareUrlRegex().Matches(line))
            {
                var candidate = match.Value.TrimEnd('.', ',', ';', '!', '?');
                if (candidate.Length == 0)
                {
                    continue;
                }

                // Skip media file URLs — they are images/audio/video, not linkable pages.
                if (IsMediaUrl(candidate))
                {
                    continue;
                }

                if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                    && (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                        || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
                {
                    return uri;
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<LinkPreviewResult?> FetchPreviewAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var cacheKey = NormalizeKey(url);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            if (cached.ExpiresAtUtc > DateTime.UtcNow)
            {
                return cached.Result;
            }

            _cache.TryRemove(cacheKey, out _);
        }

        LinkPreviewResult? result;
        try
        {
            result = await FetchAndParseAsync(url, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Link preview: unexpected failure fetching {Url}", url);
            return null;
        }

        if (_cache.Count < MaxCacheEntries)
        {
            _cache[cacheKey] = new CacheEntry(result, DateTime.UtcNow + CacheTtl);
        }

        return result;
    }

    private async Task<LinkPreviewResult?> FetchAndParseAsync(Uri url, CancellationToken cancellationToken)
    {
        var fetch = await _fetcher.FetchAsync(url, cancellationToken);
        if (!fetch.Success || fetch.Content is null)
        {
            _logger.LogDebug("Link preview: fetch failed for {Url}: {Reason}", url, fetch.ErrorReason);
            return null;
        }

        try
        {
            return ParsePreviewHtml(fetch.Content, url, fetch.FinalUri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Link preview: failed to parse HTML for {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Parses a fetched HTML page into a <see cref="LinkPreviewResult"/>, or returns
    /// <see langword="null"/> when the page contains no usable metadata.
    /// Internal so unit tests can exercise metadata extraction without network access.
    /// </summary>
    internal static LinkPreviewResult? ParsePreviewHtml(Stream html, Uri url, string? finalUri)
    {
        var parser = new HtmlParser();
        using var document = parser.ParseDocument(html);

        var siteName = GetMetaContent(document, "og:site_name") ?? GetMetaContent(document, "twitter:site");
        var title = GetMetaContent(document, "og:title")
            ?? GetMetaContent(document, "twitter:title")
            ?? document.Head?.QuerySelector("title")?.TextContent?.Trim();
        var description = GetMetaContent(document, "og:description")
            ?? GetMetaContent(document, "twitter:description")
            ?? document.Head?.QuerySelector("meta[name='description']")?.GetAttribute("content")?.Trim();
        var imageUrl = GetMetaContent(document, "og:image")
            ?? GetMetaContent(document, "twitter:image")
            ?? GetMetaContent(document, "twitter:image:src");
        var faviconUrl = document.Head?.QuerySelector("link[rel='icon']")?.GetAttribute("href")
            ?? document.Head?.QuerySelector("link[rel='shortcut icon']")?.GetAttribute("href")
            ?? document.Head?.QuerySelector("link[rel='apple-touch-icon']")?.GetAttribute("href")
            ?? new Uri(url, "/favicon.ico").ToString();

        // Base for resolving relative URLs — prefer the post-redirect URL.
        var baseUri = Uri.TryCreate(finalUri, UriKind.Absolute, out var final) ? final : url;

        if (imageUrl is not null && !Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
        {
            imageUrl = new Uri(baseUri, imageUrl).ToString();
        }

        if (!Uri.IsWellFormedUriString(faviconUrl, UriKind.Absolute))
        {
            faviconUrl = new Uri(baseUri, faviconUrl).ToString();
        }

        // Nothing useful to show without at least a title, description, or image.
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        return new LinkPreviewResult
        {
            Url = url.ToString(),
            Title = Truncate(title, 500),
            Description = Truncate(description, 1000),
            ImageUrl = Truncate(imageUrl, 2000),
            SiteName = Truncate(siteName, 200),
            FaviconUrl = Truncate(faviconUrl, 2000)
        };
    }

    private static bool IsMediaUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && MediaExtensions.Any(ext => uri.AbsolutePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeKey(Uri url)
    {
        // Strip fragment; keep scheme+host+path+query (cache is case-insensitive on host).
        var builder = new UriBuilder(url) { Fragment = string.Empty };
        return builder.Uri.ToString();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? GetMetaContent(IDocument document, string property)
    {
        var element = document.Head?.QuerySelector($"meta[property='{property}']")
            ?? document.Head?.QuerySelector($"meta[name='{property}']");
        return element?.GetAttribute("content")?.Trim();
    }

    /// <summary>Cache entry with an absolute expiry.</summary>
    private sealed record CacheEntry(LinkPreviewResult? Result, DateTime ExpiresAtUtc);
}

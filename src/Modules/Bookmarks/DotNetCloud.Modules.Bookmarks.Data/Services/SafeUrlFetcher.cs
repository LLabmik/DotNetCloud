using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Bookmarks.Data.Services;

/// <summary>
/// SSRF-safe HTTP fetcher for bookmark preview scraping.
/// Validates URLs, blocks private/internal IPs, enforces size and timeout limits.
/// </summary>
public sealed class SafeUrlFetcher
{
    private readonly ILogger<SafeUrlFetcher> _logger;
    private readonly HttpClient _httpClient;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(15);
    private const int MaxRedirects = 5;
    private const int MaxResponseSizeBytes = 1_048_576; // 1 MB
    private static readonly string[] AllowedContentTypes = ["text/html", "application/xhtml+xml"];

    public SafeUrlFetcher(ILogger<SafeUrlFetcher> logger)
    {
        _logger = logger;

        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            ConnectTimeout = ConnectTimeout,
            ConnectCallback = ValidateAndConnectAsync
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = OverallTimeout,
            MaxResponseContentBufferSize = MaxResponseSizeBytes
        };
    }

    /// <summary>
    /// Fetches a URL safely, returning the HTML content and metadata.
    /// </summary>
    public async Task<SafeFetchResult> FetchAsync(Uri uri, CancellationToken ct = default)
    {
        if (!IsAllowedScheme(uri))
        {
            _logger.LogWarning("Blocked URL with disallowed scheme: {Uri}", uri);
            return new SafeFetchResult { Success = false, ErrorReason = $"Scheme '{uri.Scheme}' is not allowed." };
        }

        if (IsBlockedIp(uri.Host))
        {
            _logger.LogWarning("Blocked private/internal IP for host: {Host}", uri.Host);
            return new SafeFetchResult { Success = false, ErrorReason = "Private/internal IP addresses are not allowed.", BlockedIp = uri.Host };
        }

        var currentUri = uri;
        var redirectCount = 0;

        while (redirectCount <= MaxRedirects)
        {
            ct.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            request.Headers.UserAgent.ParseAdd("DotNetCloud-BookmarkPreview/1.0");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (TaskCanceledException)
            {
                return new SafeFetchResult { Success = false, ErrorReason = "Request timed out." };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP request failed for {Uri}", currentUri);
                return new SafeFetchResult { Success = false, ErrorReason = $"Request failed: {ex.Message}" };
            }

            // Handle redirects
            var statusCode = (int)response.StatusCode;
            if (statusCode is >= 300 and < 400)
            {
                response.Dispose();

                var location = response.Headers.Location;
                if (location is null)
                    return new SafeFetchResult { Success = false, ErrorReason = "Redirect location missing." };

                if (!location.IsAbsoluteUri)
                {
                    location = new Uri(currentUri, location);
                }

                if (!IsAllowedScheme(location))
                {
                    _logger.LogWarning("Blocked redirect to disallowed scheme: {Uri}", location);
                    return new SafeFetchResult { Success = false, ErrorReason = $"Redirect scheme '{location.Scheme}' not allowed." };
                }

                if (IsBlockedIp(location.Host))
                {
                    _logger.LogWarning("Blocked redirect to private IP: {Host}", location.Host);
                    return new SafeFetchResult { Success = false, ErrorReason = "Redirect to private IP blocked.", BlockedIp = location.Host };
                }

                currentUri = location;
                redirectCount++;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                return new SafeFetchResult { Success = false, ErrorReason = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}." };
            }

            // Validate Content-Type
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!AllowedContentTypes.Any(ct => contentType.StartsWith(ct, StringComparison.OrdinalIgnoreCase)))
            {
                response.Dispose();
                return new SafeFetchResult { Success = false, ErrorReason = $"Content-Type '{contentType}' not allowed." };
            }

            // Read content with size limit
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(OverallTimeout);

                var contentBytes = await response.Content.ReadAsByteArrayAsync(cts.Token);

                if (contentBytes.Length > MaxResponseSizeBytes)
                {
                    return new SafeFetchResult { Success = false, ErrorReason = $"Response too large ({contentBytes.Length} bytes)." };
                }

                return new SafeFetchResult
                {
                    Success = true,
                    Content = new MemoryStream(contentBytes),
                    ContentType = contentType,
                    ContentLength = response.Content.Headers.ContentLength,
                    FinalUri = currentUri.ToString(),
                    ETag = response.Headers.ETag?.Tag,
                    LastModified = response.Content.Headers.LastModified?.ToString("R")
                };
            }
            finally
            {
                response.Dispose();
            }
        }

        return new SafeFetchResult { Success = false, ErrorReason = $"Too many redirects (max {MaxRedirects})." };
    }

    private static bool IsAllowedScheme(Uri uri) =>
        uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
        || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlockedIp(string host)
    {
        if (!IPAddress.TryParse(host, out var ip))
        {
            // Hostname: allow — DNS resolution will happen at connect time
            return false;
        }

        return IsPrivateOrSpecialIp(ip);
    }

    private static bool IsPrivateOrSpecialIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 100.64.0.0/10 (CGNAT)
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(ip)) return true;
            if (ip.IsIPv6LinkLocal) return true;
            if (ip.IsIPv6SiteLocal) return true;
        }

        return false;
    }

    private static async ValueTask<Stream> ValidateAndConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.InitialRequestMessage?.RequestUri?.Host ?? context.DnsEndPoint.Host;

        // Re-validate DNS resolution
        var addresses = await Dns.GetHostAddressesAsync(host, ct);
        foreach (var addr in addresses)
        {
            if (IsPrivateOrSpecialIp(addr))
            {
                throw new HttpRequestException($"Blocked connection to private IP: {addr}");
            }
        }

        var socket = new Socket(context.DnsEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(context.DnsEndPoint, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

/// <summary>
/// Result of a safe URL fetch operation.
/// </summary>
public sealed record SafeFetchResult
{
    public bool Success { get; init; }
    public Stream? Content { get; init; }
    public string? ContentType { get; init; }
    public long? ContentLength { get; init; }
    public string? FinalUri { get; init; }
    public string? ErrorReason { get; init; }
    public string? BlockedIp { get; init; }
    public string? ETag { get; init; }
    public string? LastModified { get; init; }
}

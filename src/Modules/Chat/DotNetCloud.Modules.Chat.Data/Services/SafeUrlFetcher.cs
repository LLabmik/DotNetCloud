using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// SSRF-safe HTTP fetcher used for chat link previews.
/// Validates URLs, blocks private/internal IPs, enforces size and timeout limits,
/// and re-validates DNS resolution at connect time to mitigate DNS-rebinding attacks.
/// </summary>
/// <remarks>
/// Timeouts are deliberately tight because unfurling runs inline during message send;
/// a slow or unreachable page must never delay sending a chat message.
/// </remarks>
public sealed class SafeUrlFetcher
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(6);
    private const int MaxRedirects = 5;
    private const int MaxResponseSizeBytes = 768 * 1024; // 768 KB of HTML is plenty for a preview

    private static readonly string[] AllowedContentTypes = ["text/html", "application/xhtml+xml"];

    private readonly ILogger<SafeUrlFetcher> _logger;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeUrlFetcher"/> class.
    /// </summary>
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
            _logger.LogWarning("Link preview: blocked URL with disallowed scheme: {Uri}", uri);
            return SafeFetchResult.Failure($"Scheme '{uri.Scheme}' is not allowed.");
        }

        if (IsBlockedIp(uri.Host))
        {
            _logger.LogWarning("Link preview: blocked private/internal IP for host: {Host}", uri.Host);
            return SafeFetchResult.Failure("Private/internal IP addresses are not allowed.", uri.Host);
        }

        var currentUri = uri;
        var redirectCount = 0;

        while (redirectCount <= MaxRedirects)
        {
            ct.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            request.Headers.UserAgent.ParseAdd("DotNetCloud-ChatLinkPreview/1.0");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (TaskCanceledException)
            {
                return SafeFetchResult.Failure("Request timed out.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Link preview: HTTP request failed for {Uri}", currentUri);
                return SafeFetchResult.Failure($"Request failed: {ex.Message}");
            }

            // Handle redirects — each hop is re-validated against SSRF rules.
            var statusCode = (int)response.StatusCode;
            if (statusCode is >= 300 and < 400)
            {
                response.Dispose();

                var location = response.Headers.Location;
                if (location is null)
                {
                    return SafeFetchResult.Failure("Redirect location missing.");
                }

                if (!location.IsAbsoluteUri)
                {
                    location = new Uri(currentUri, location);
                }

                if (!IsAllowedScheme(location))
                {
                    _logger.LogWarning("Link preview: blocked redirect to disallowed scheme: {Uri}", location);
                    return SafeFetchResult.Failure($"Redirect scheme '{location.Scheme}' not allowed.");
                }

                if (IsBlockedIp(location.Host))
                {
                    _logger.LogWarning("Link preview: blocked redirect to private IP: {Host}", location.Host);
                    return SafeFetchResult.Failure("Redirect to private IP blocked.", location.Host);
                }

                currentUri = location;
                redirectCount++;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                return SafeFetchResult.Failure($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            // Validate Content-Type
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!AllowedContentTypes.Any(allowed => contentType.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)))
            {
                response.Dispose();
                return SafeFetchResult.Failure($"Content-Type '{contentType}' not allowed.");
            }

            // Read content with size limit
            try
            {
                var contentBytes = await response.Content.ReadAsByteArrayAsync(ct);

                if (contentBytes.Length > MaxResponseSizeBytes)
                {
                    return SafeFetchResult.Failure($"Response too large ({contentBytes.Length} bytes).");
                }

                return new SafeFetchResult
                {
                    Success = true,
                    Content = new MemoryStream(contentBytes),
                    ContentType = contentType,
                    FinalUri = currentUri.ToString()
                };
            }
            finally
            {
                response.Dispose();
            }
        }

        return SafeFetchResult.Failure($"Too many redirects (max {MaxRedirects}).");
    }

    private static bool IsAllowedScheme(Uri uri) =>
        uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
        || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the host is an IP literal on a private/internal range.</summary>
    internal static bool IsBlockedIp(string host)
    {
        if (!IPAddress.TryParse(host, out var ip))
        {
            // Hostname: allow — DNS is resolved and validated again at connect time.
            return false;
        }

        return IsPrivateOrSpecialIp(ip);
    }

    /// <summary>Returns true when the IP is loopback, private, link-local, CGNAT, or otherwise internal.</summary>
    internal static bool IsPrivateOrSpecialIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }

            // 100.64.0.0/10 (CGNAT)
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
            {
                return true;
            }

            // 0.0.0.0/8 (special use) and 127.0.0.0/8 are covered by loopback above for 127.x.
            if (bytes[0] == 0)
            {
                return true;
            }
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(ip))
            {
                return true;
            }

            if (ip.IsIPv6LinkLocal)
            {
                return true;
            }

            if (ip.IsIPv6SiteLocal)
            {
                return true;
            }

            // IPv4-mapped IPv6 (::ffff:10.0.0.1)
            if (ip.IsIPv4MappedToIPv6)
            {
                return IsPrivateOrSpecialIp(ip.MapToIPv4());
            }
        }

        return false;
    }

    private static async ValueTask<Stream> ValidateAndConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.InitialRequestMessage?.RequestUri?.Host ?? context.DnsEndPoint.Host;

        // Re-validate DNS resolution at connect time (mitigates DNS rebinding).
        var addresses = await Dns.GetHostAddressesAsync(host, ct);
        foreach (var addr in addresses)
        {
            if (IsPrivateOrSpecialIp(addr))
            {
                throw new HttpRequestException($"Blocked connection to private IP: {addr}");
            }
        }

        var addressFamily = context.DnsEndPoint.AddressFamily;
        if (addressFamily == AddressFamily.Unspecified)
        {
            addressFamily = addresses.FirstOrDefault()?.AddressFamily ?? AddressFamily.InterNetwork;
        }

        var socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);
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
    /// <summary>Whether the fetch succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>HTML content stream when successful.</summary>
    public Stream? Content { get; init; }

    /// <summary>Response Content-Type when successful.</summary>
    public string? ContentType { get; init; }

    /// <summary>Final URI after redirects when successful.</summary>
    public string? FinalUri { get; init; }

    /// <summary>Human-readable failure reason.</summary>
    public string? ErrorReason { get; init; }

    /// <summary>Blocked IP address, when the failure was an SSRF block.</summary>
    public string? BlockedIp { get; init; }

    /// <summary>Creates a failure result.</summary>
    public static SafeFetchResult Failure(string reason, string? blockedIp = null) =>
        new() { Success = false, ErrorReason = reason, BlockedIp = blockedIp };
}

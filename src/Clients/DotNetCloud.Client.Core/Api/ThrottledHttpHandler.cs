using DotNetCloud.Client.Core.Transfer;

namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// <see cref="DelegatingHandler"/> that wraps request/response content in
/// <see cref="ThrottledStream"/> to enforce per-direction bandwidth limits.
/// </summary>
public sealed class ThrottledHttpHandler : DelegatingHandler
{
    private readonly long _uploadBytesPerSecond;
    private readonly long _downloadBytesPerSecond;

    /// <summary>Initializes a new <see cref="ThrottledHttpHandler"/>.</summary>
    /// <param name="uploadBytesPerSecond">Upload limit in bytes/s. 0 = unlimited.</param>
    /// <param name="downloadBytesPerSecond">Download limit in bytes/s. 0 = unlimited.</param>
    public ThrottledHttpHandler(long uploadBytesPerSecond, long downloadBytesPerSecond)
    {
        _uploadBytesPerSecond = uploadBytesPerSecond;
        _downloadBytesPerSecond = downloadBytesPerSecond;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Wrap request content for upload throttling
        if (_uploadBytesPerSecond > 0 && request.Content is not null)
        {
            var originalStream = await request.Content.ReadAsStreamAsync(cancellationToken);
            var throttled = new ThrottledStream(originalStream, _uploadBytesPerSecond, leaveOpen: true);
            var newContent = new StreamContent(throttled);

            // Copy headers from original content
            foreach (var header in request.Content.Headers)
                newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);

            request.Content = newContent;
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Wrap response content for download throttling
        if (_downloadBytesPerSecond > 0 && response.Content is not null)
        {
            var originalStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var throttled = new ThrottledStream(originalStream, _downloadBytesPerSecond, leaveOpen: true);
            var newContent = new StreamContent(throttled);

            // Copy headers from original content
            foreach (var header in response.Content.Headers)
                newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);

            response.Content = newContent;
        }

        return response;
    }
}

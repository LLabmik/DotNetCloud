using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// Delegating handler that attaches a unique <c>X-Request-ID</c> header to every outgoing
/// HTTP request so that client and server log entries can be correlated.
/// </summary>
public sealed class CorrelationIdHandler : DelegatingHandler
{
    private readonly ILogger<CorrelationIdHandler> _logger;

    /// <summary>Initializes a new <see cref="CorrelationIdHandler"/>.</summary>
    public CorrelationIdHandler(ILogger<CorrelationIdHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        request.Headers.TryAddWithoutValidation("X-Request-ID", requestId);
        request.Headers.TryAddWithoutValidation("X-Sync-Capabilities", "cdc");

        _logger.LogInformation(
            "API call {Method} {Url} RequestId={RequestId}",
            request.Method,
            request.RequestUri,
            requestId);

        var response = await base.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotModified)
        {
            _logger.LogError(
                "API call failed. RequestId={RequestId}, Status={StatusCode}",
                requestId,
                (int)response.StatusCode);
        }

        return response;
    }
}

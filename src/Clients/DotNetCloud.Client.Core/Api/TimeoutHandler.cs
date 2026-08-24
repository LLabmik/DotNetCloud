namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// Enforces a "time-to-first-byte" timeout around the inner handler chain.
/// Because callers use <see cref="HttpCompletionOption.ResponseHeadersRead"/>,
/// the timeout covers connection + headers only; streaming upload/download
/// bodies are NOT cancelled, so large transfers are unaffected.
/// </summary>
public sealed class TimeoutHandler : DelegatingHandler
{
    private readonly TimeSpan _timeout;

    /// <summary>Creates a handler with the given timeout (default 30 s).</summary>
    public TimeoutHandler(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            return await base.SendAsync(request, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException(
                $"The request timed out after {_timeout.TotalSeconds:0}s waiting for the server to respond.");
        }
    }
}

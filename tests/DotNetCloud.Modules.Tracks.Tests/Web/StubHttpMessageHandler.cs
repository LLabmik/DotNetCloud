using System.Net;
using System.Text;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.Tests.Web;

/// <summary>
/// Test double for the module host: records every request (method, URI and body) and answers with a
/// canned response, so the browser-side clients can be exercised without a server.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _responder;

    /// <summary>Initializes a new instance of the <see cref="StubHttpMessageHandler"/> class.</summary>
    /// <param name="responder">Builds the response from the request and its (already read) body.</param>
    public StubHttpMessageHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    /// <summary>
    /// Gets every request the client made, in order. <c>Uri</c> is the module-relative path and query
    /// (e.g. <c>/api/v1/products/x</c>), which is what the clients are expected to send.
    /// </summary>
    public List<(HttpMethod Method, string Uri, string? Body)> Requests { get; } = [];

    /// <summary>Creates a handler that always answers with the given JSON body.</summary>
    /// <param name="json">The response body.</param>
    /// <param name="statusCode">The HTTP status.</param>
    /// <returns>The handler.</returns>
    public static StubHttpMessageHandler Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new((_, _) => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri?.PathAndQuery ?? string.Empty, body));
        return _responder(request, body);
    }
}

/// <summary>An <see cref="IJSRuntime"/> whose storage calls fail, as they do in an opaque-origin frame.</summary>
internal sealed class UnavailableStorageJsRuntime : IJSRuntime
{
    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => throw new JSException("localStorage is not available in an opaque origin");

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => throw new JSException("localStorage is not available in an opaque origin");
}

/// <summary>An <see cref="IJSRuntime"/> that records what was invoked and returns an empty answer.</summary>
internal sealed class RecordingJsRuntime : IJSRuntime
{
    private readonly Func<string, object?[], object?>? _resultFactory;

    /// <summary>Initializes a new instance of the <see cref="RecordingJsRuntime"/> class.</summary>
    /// <param name="resultFactory">Optional factory for the value a call returns.</param>
    public RecordingJsRuntime(Func<string, object?[], object?>? resultFactory = null)
    {
        _resultFactory = resultFactory;
    }

    /// <summary>Gets the calls, as (identifier, arguments).</summary>
    public List<(string Identifier, object?[] Args)> Calls { get; } = [];

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Calls.Add((identifier, args ?? []));
        var result = _resultFactory?.Invoke(identifier, args ?? []);
        return ValueTask.FromResult(result is TValue typed ? typed : default!);
    }
}

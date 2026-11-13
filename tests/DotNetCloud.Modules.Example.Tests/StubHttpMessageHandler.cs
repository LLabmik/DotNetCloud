using System.Net;
using System.Text;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Records the requests a browser-side client makes and answers them from a scripted responder.
/// </summary>
/// <remarks>
/// Assertions use <see cref="HttpRequestMessage.RequestUri"/>'s path and query, so they never depend on
/// the client's base address — which a framed module derives from its own document URL.
/// </remarks>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    /// <summary>Gets the path-and-query of every request, in order.</summary>
    public List<string> Requests { get; } = [];

    /// <summary>Creates a handler that always answers with the same JSON body.</summary>
    /// <param name="json">The response body.</param>
    /// <param name="status">The status code.</param>
    /// <returns>The handler.</returns>
    public static StubHttpMessageHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!.PathAndQuery);
        return Task.FromResult(_responder(request));
    }
}

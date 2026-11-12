using System.Net;
using System.Text;

namespace DotNetCloud.Modules.Sdk.Tests.Web;

/// <summary>
/// Records the requests a browser-side client makes and answers them from a scripted responder.
/// </summary>
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
    /// <param name="configure">Optional header configuration.</param>
    /// <returns>The handler.</returns>
    public static StubHttpMessageHandler Json(
        string json,
        HttpStatusCode status = HttpStatusCode.OK,
        Action<HttpResponseMessage>? configure = null) =>
        new(_ =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            configure?.Invoke(response);
            return response;
        });

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!.PathAndQuery);
        return Task.FromResult(_responder(request));
    }
}

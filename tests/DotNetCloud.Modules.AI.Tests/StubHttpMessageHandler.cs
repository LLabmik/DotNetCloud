using System.Net;
using System.Text;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>Records the requests a browser-side client makes and answers them from a script.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    public List<string> Requests { get; } = [];

    public List<HttpMethod> Methods { get; } = [];

    public static StubHttpMessageHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!.PathAndQuery);
        Methods.Add(request.Method);
        return Task.FromResult(_responder(request));
    }
}

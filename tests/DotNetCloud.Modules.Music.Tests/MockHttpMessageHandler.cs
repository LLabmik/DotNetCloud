using System.Net;
using System.Text;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Reusable mock <see cref="HttpMessageHandler"/> for testing HTTP clients.
/// Tracks all received requests and responds with preconfigured responses.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    /// <summary>All request messages received by this handler.</summary>
    public List<HttpRequestMessage> ReceivedRequests { get; } = [];

    /// <summary>
    /// Creates a new mock handler with a custom response factory.
    /// </summary>
    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ReceivedRequests.Add(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_handler(request));
    }

    /// <summary>Creates a handler that always returns 200 OK with the given JSON body.</summary>
    public static MockHttpMessageHandler ForJson(string json)
    {
        return new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    /// <summary>Creates a handler that always returns 200 OK with binary data.</summary>
    public static MockHttpMessageHandler ForBytes(byte[] data, string contentType)
    {
        return new MockHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(data)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            return response;
        });
    }

    /// <summary>Creates a handler that always returns the given HTTP status code.</summary>
    public static MockHttpMessageHandler ForStatus(HttpStatusCode code)
    {
        return new MockHttpMessageHandler(_ => new HttpResponseMessage(code));
    }

    /// <summary>Creates a handler that returns a different response for each successive call.</summary>
    public static MockHttpMessageHandler ForSequence(params HttpResponseMessage[] responses)
    {
        var callIndex = 0;
        return new MockHttpMessageHandler(_ =>
        {
            var index = callIndex;
            if (index < responses.Length)
            {
                callIndex++;
                return responses[index];
            }
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });
    }

    /// <summary>Creates a handler that throws the specified exception.</summary>
    public static MockHttpMessageHandler ForException<TException>() where TException : Exception, new()
    {
        return new MockHttpMessageHandler(_ => throw new TException());
    }

    /// <summary>Creates a handler that throws an <see cref="HttpRequestException"/>.</summary>
    public static MockHttpMessageHandler ForNetworkError()
    {
        return new MockHttpMessageHandler(_ => throw new HttpRequestException("Simulated network error"));
    }

    /// <summary>Creates a handler that throws a <see cref="TaskCanceledException"/> to simulate a timeout.</summary>
    public static MockHttpMessageHandler ForTimeout()
    {
        return new MockHttpMessageHandler(_ => throw new TaskCanceledException("Simulated timeout"));
    }

    /// <summary>Creates a handler that returns URL-dependent responses.</summary>
    public static MockHttpMessageHandler ForRoutes(Dictionary<string, HttpResponseMessage> routes)
    {
        return new MockHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.PathAndQuery ?? "";
            foreach (var route in routes)
            {
                if (path.Contains(route.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return route.Value;
                }
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
    }
}

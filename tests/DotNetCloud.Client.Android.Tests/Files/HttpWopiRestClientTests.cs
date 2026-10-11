using System.Net;
using System.Net.Http;
using System.Text;
using DotNetCloud.Client.Android.Files;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Client.Android.Tests.Files;

/// <summary>
/// Wire-level tests for the Collabora/WOPI REST client: URL shape, bearer auth and
/// enveloped/raw response handling.
/// </summary>
[TestClass]
public sealed class HttpWopiRestClientTests
{
    private const string ServerUrl = "https://example.com:15443";
    private const string AccessToken = "test-access-token-123";
    private const string NodeIdText = "0e984725-c51c-4bf4-9960-e1c80e27aba0";
    private static readonly Guid NodeId = Guid.Parse(NodeIdText);

    // ── Helpers ──────────────────────────────────────────────────────

    private static (HttpWopiRestClient Client, RecordingHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RecordingHandler(respond);
        var http = new HttpClient(handler);
        var client = new HttpWopiRestClient(http, NullLogger<HttpWopiRestClient>.Instance);
        return (client, handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static void AssertBearer(RecordingHandler handler)
    {
        Assert.AreEqual("Bearer", handler.LastRequest!.Headers.Authorization?.Scheme);
        Assert.AreEqual(AccessToken, handler.LastRequest.Headers.Authorization?.Parameter);
    }

    // ── Discovery ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetDiscoveryAsync_Available_ReturnsNormalizedExtensions()
    {
        const string body = """
            {
              "success": true,
              "data": {
                "available": true,
                "supportedExtensions": ["DOCX", "docx", ".odt", "pdf", ""],
                "actionCount": 24,
                "fetchedAt": "2026-10-10T12:00:00Z"
              }
            }
            """;
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        var discovery = await client.GetDiscoveryAsync(ServerUrl, AccessToken);

        Assert.IsTrue(discovery.Available);
        CollectionAssert.AreEqual(
            new[] { "docx", "odt", "pdf" },
            discovery.SupportedExtensions.ToArray());
        Assert.AreEqual(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.AreEqual($"{ServerUrl}/api/v1/wopi/discovery", handler.LastRequest.RequestUri!.ToString());
        AssertBearer(handler);
    }

    [TestMethod]
    public async Task GetDiscoveryAsync_NotAvailable_ReturnsUnavailable()
    {
        const string body = """{ "success": true, "data": { "available": false, "supportedExtensions": [] } }""";
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        var discovery = await client.GetDiscoveryAsync(ServerUrl, AccessToken);

        Assert.IsFalse(discovery.Available);
        Assert.AreEqual(0, discovery.SupportedExtensions.Count);
    }

    [TestMethod]
    public async Task GetDiscoveryAsync_ServerError_ReturnsUnavailableInsteadOfThrowing()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var discovery = await client.GetDiscoveryAsync(ServerUrl, AccessToken);

        Assert.IsFalse(discovery.Available);
        Assert.AreEqual(0, discovery.SupportedExtensions.Count);
    }

    [TestMethod]
    public async Task GetDiscoveryAsync_TransportFailure_ReturnsUnavailableInsteadOfThrowing()
    {
        var (client, _) = CreateClient(_ => throw new HttpRequestException("no route to host"));

        var discovery = await client.GetDiscoveryAsync(ServerUrl, AccessToken);

        Assert.IsFalse(discovery.Available);
    }

    // ── Open editor ──────────────────────────────────────────────────

    [TestMethod]
    public async Task OpenEditorAsync_EnvelopedResponse_ReturnsSession()
    {
        const string body = """
            {
              "success": true,
              "data": {
                "accessToken": "wopi-token",
                "accessTokenTtl": 1760000000000,
                "wopiSrc": "https://example.com:15443/api/v1/wopi/files/0e984725",
                "editorUrl": "https://example.com:15443/browser/abc/cool.html?WOPISrc=x&access_token=wopi-token"
              }
            }
            """;
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        var session = await client.OpenEditorAsync(ServerUrl, AccessToken, NodeId);

        Assert.AreEqual("wopi-token", session.AccessToken);
        Assert.AreEqual(1760000000000L, session.AccessTokenTtl);
        Assert.AreEqual("https://example.com:15443/api/v1/wopi/files/0e984725", session.WopiSrc);
        Assert.AreEqual(
            "https://example.com:15443/browser/abc/cool.html?WOPISrc=x&access_token=wopi-token",
            session.EditorUrl);
        Assert.AreEqual(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.AreEqual($"{ServerUrl}/api/v1/wopi/token/{NodeIdText}", handler.LastRequest.RequestUri!.ToString());
        AssertBearer(handler);
    }

    [TestMethod]
    public async Task OpenEditorAsync_RawResponse_IsAlsoAccepted()
    {
        const string body = """
            {
              "accessToken": "wopi-token",
              "accessTokenTtl": 1760000000000,
              "wopiSrc": "https://example.com:15443/api/v1/wopi/files/0e984725",
              "editorUrl": "https://example.com:15443/browser/abc/cool.html"
            }
            """;
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        var session = await client.OpenEditorAsync(ServerUrl, AccessToken, NodeId);

        Assert.AreEqual("wopi-token", session.AccessToken);
        Assert.AreEqual("https://example.com:15443/browser/abc/cool.html", session.EditorUrl);
    }

    [TestMethod]
    public async Task OpenEditorAsync_MissingEditorUrl_Throws()
    {
        const string body = """{ "success": true, "data": { "accessToken": "wopi-token", "accessTokenTtl": 1 } }""";
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.OpenEditorAsync(ServerUrl, AccessToken, NodeId));
    }

    [TestMethod]
    public async Task OpenEditorAsync_Forbidden_Throws()
    {
        var (client, _) = CreateClient(_ => Json(
            HttpStatusCode.Forbidden, """{ "success": false, "error": "no access" }"""));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.OpenEditorAsync(ServerUrl, AccessToken, NodeId));
    }

    // ── Close editor ─────────────────────────────────────────────────

    [TestMethod]
    public async Task CloseEditorAsync_SendsDeleteAndAcceptsNoContent()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.CloseEditorAsync(ServerUrl, AccessToken, NodeId);

        Assert.AreEqual(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.AreEqual($"{ServerUrl}/api/v1/wopi/token/{NodeIdText}", handler.LastRequest.RequestUri!.ToString());
        AssertBearer(handler);
    }

    [TestMethod]
    public async Task CloseEditorAsync_ServerError_Throws()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.CloseEditorAsync(ServerUrl, AccessToken, NodeId));
    }

    /// <summary>Stub transport that records the last request and returns a scripted response.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_respond(request));
        }
    }
}

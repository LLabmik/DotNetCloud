using System.Net;
using System.Net.Http;
using System.Text;
using DotNetCloud.Client.Android.Files;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Client.Android.Tests.Files;

/// <summary>
/// Wire-level tests for the shared-with-me listing. The endpoint returns a <b>raw</b> JSON array
/// (not the server's standard envelope), which is the case these tests exist to pin down.
/// </summary>
[TestClass]
public sealed class HttpFileRestClientSharedWithMeTests
{
    private const string ServerUrl = "https://example.com:15443";
    private const string AccessToken = "test-access-token-123";

    [TestMethod]
    public async Task GetSharedWithMeAsync_RawArray_MapsShares()
    {
        var nodeId = Guid.NewGuid();
        var body = $$"""
            [
              {
                "id": "{{Guid.NewGuid()}}",
                "fileNodeId": "{{nodeId}}",
                "nodeName": "ShareTest.docx",
                "nodeType": "File",
                "size": 5400,
                "mimeType": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "shareType": "User",
                "permission": "Read",
                "createdAt": "2026-10-10T18:00:00Z"
              }
            ]
            """;
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        var shares = await client.GetSharedWithMeAsync(ServerUrl, AccessToken);

        Assert.AreEqual(1, shares.Count);
        Assert.AreEqual(nodeId, shares[0].NodeId);
        Assert.AreEqual("ShareTest.docx", shares[0].Name);
        Assert.AreEqual("File", shares[0].NodeType);
        Assert.AreEqual(5400, shares[0].Size);
        Assert.IsTrue(shares[0].IsReadOnly);
        Assert.AreEqual(new DateTime(2026, 10, 10, 18, 0, 0, DateTimeKind.Utc), shares[0].SharedAt);
        Assert.AreEqual(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.AreEqual($"{ServerUrl}/api/v1/files/shared-with-me", handler.LastRequest.RequestUri!.ToString());
        Assert.AreEqual("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.AreEqual(AccessToken, handler.LastRequest.Headers.Authorization?.Parameter);
    }

    [TestMethod]
    public async Task GetSharedWithMeAsync_ReadWritePermission_IsNotReadOnly()
    {
        var body = $$"""
            [
              {
                "fileNodeId": "{{Guid.NewGuid()}}",
                "nodeName": "Editable.docx",
                "nodeType": "File",
                "permission": "ReadWrite",
                "createdAt": "2026-10-10T18:00:00Z"
              }
            ]
            """;
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        var shares = await client.GetSharedWithMeAsync(ServerUrl, AccessToken);

        Assert.AreEqual(1, shares.Count);
        Assert.IsFalse(shares[0].IsReadOnly);
    }

    [TestMethod]
    public async Task GetSharedWithMeAsync_EnvelopedResponse_IsAlsoAccepted()
    {
        var nodeId = Guid.NewGuid();
        var body = $$"""
            {
              "success": true,
              "data": [
                {
                  "fileNodeId": "{{nodeId}}",
                  "nodeName": "Enveloped.docx",
                  "nodeType": "File",
                  "permission": "Read",
                  "createdAt": "2026-10-10T18:00:00Z"
                }
              ]
            }
            """;
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        var shares = await client.GetSharedWithMeAsync(ServerUrl, AccessToken);

        Assert.AreEqual(1, shares.Count);
        Assert.AreEqual("Enveloped.docx", shares[0].Name);
    }

    [TestMethod]
    public async Task GetSharedWithMeAsync_SkipsEntriesWithoutANodeOrName()
    {
        var body = $$"""
            [
              { "fileNodeId": "00000000-0000-0000-0000-000000000000", "nodeName": "Ghost", "nodeType": "File" },
              { "fileNodeId": "{{Guid.NewGuid()}}", "nodeName": "   ", "nodeType": "File" },
              { "fileNodeId": "{{Guid.NewGuid()}}", "nodeName": "Real", "nodeType": "Folder" }
            ]
            """;
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        var shares = await client.GetSharedWithMeAsync(ServerUrl, AccessToken);

        Assert.AreEqual(1, shares.Count);
        Assert.AreEqual("Real", shares[0].Name);
        Assert.AreEqual("Folder", shares[0].NodeType);
    }

    [TestMethod]
    public async Task GetSharedWithMeAsync_EmptyArray_ReturnsEmpty()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, "[]"));

        var shares = await client.GetSharedWithMeAsync(ServerUrl, AccessToken);

        Assert.AreEqual(0, shares.Count);
    }

    [TestMethod]
    public async Task GetSharedWithMeAsync_ServerError_Throws()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetSharedWithMeAsync(ServerUrl, AccessToken));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static (HttpFileRestClient Client, RecordingHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RecordingHandler(respond);
        var http = new HttpClient(handler);
        var client = new HttpFileRestClient(http, NullLogger<HttpFileRestClient>.Instance);
        return (client, handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

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

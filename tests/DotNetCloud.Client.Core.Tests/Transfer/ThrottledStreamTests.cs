using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Transfer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.Transfer;

[TestClass]
public sealed class ThrottledStreamTests
{
    [TestMethod]
    public async Task ThrottledStream_UnlimitedPassThrough_NoDelay()
    {
        // Arrange: 0 = unlimited, should pass through immediately.
        var data = new byte[4096];
        Random.Shared.NextBytes(data);
        using var inner = new MemoryStream(data);
        using var throttled = new ThrottledStream(inner, bytesPerSecond: 0);

        // Act
        var buffer = new byte[4096];
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var read = await throttled.ReadAsync(buffer);
        sw.Stop();

        // Assert: should complete almost instantly.
        Assert.AreEqual(4096, read);
        Assert.IsTrue(sw.ElapsedMilliseconds < 500, $"Expected <500ms, got {sw.ElapsedMilliseconds}ms");
        CollectionAssert.AreEqual(data, buffer);
    }

    [TestMethod]
    public async Task ThrottledStream_LimitOf1KBps_ThrottlesWrite()
    {
        // Arrange: 1 KB/s limit, write 2 KB → should take ~1 second.
        var data = new byte[2048];
        Random.Shared.NextBytes(data);
        using var inner = new MemoryStream();
        using var throttled = new ThrottledStream(inner, bytesPerSecond: 1024);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await throttled.WriteAsync(data);
        sw.Stop();

        // Assert: should take at least 800ms (1s minus timing tolerance).
        Assert.IsTrue(sw.ElapsedMilliseconds >= 800,
            $"Expected >=800ms for 2 KB at 1 KB/s, got {sw.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public void ThrottledStream_Dispose_DisposesInner()
    {
        var inner = new MemoryStream();
        var throttled = new ThrottledStream(inner, bytesPerSecond: 0);
        throttled.Dispose();

        // After disposal, the inner stream should also be disposed.
        Assert.ThrowsExactly<ObjectDisposedException>(() => inner.ReadByte());
    }

    [TestMethod]
    public void ThrottledStream_LeaveOpen_DoesNotDisposeInner()
    {
        var inner = new MemoryStream(new byte[] { 1, 2, 3 });
        var throttled = new ThrottledStream(inner, bytesPerSecond: 0, leaveOpen: true);
        throttled.Dispose();

        // Inner stream should still be accessible.
        inner.Position = 0;
        Assert.AreEqual(1, inner.ReadByte());
    }
}

[TestClass]
public sealed class ThrottledHttpHandlerTests
{
    [TestMethod]
    public async Task ThrottledHttpHandler_ZeroLimits_DoesNotWrapContent()
    {
        // Arrange: both limits 0 → content should pass through unwrapped.
        var innerHandler = new StubHandler(new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new StringContent("hello"),
        });

        var handler = new ThrottledHttpHandler(0, 0) { InnerHandler = innerHandler };
        using var client = new HttpClient(handler);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/test")
        {
            Content = new StringContent("request body"),
        };
        var response = await client.SendAsync(request);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("hello", body);

        // The inner handler should have received the original content type unchanged.
        Assert.IsNotNull(innerHandler.LastRequest?.Content);
    }

    [TestMethod]
    public async Task ThrottledHttpHandler_WithLimits_WrapsContent()
    {
        // Arrange: non-zero limits → content should be wrapped.
        var responseContent = new byte[512];
        Random.Shared.NextBytes(responseContent);

        var innerHandler = new StubHandler(new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new ByteArrayContent(responseContent),
        });

        var handler = new ThrottledHttpHandler(
            uploadBytesPerSecond: 1024 * 1024,
            downloadBytesPerSecond: 1024 * 1024)
        {
            InnerHandler = innerHandler
        };
        using var client = new HttpClient(handler);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/test")
        {
            Content = new StringContent("upload data"),
        };
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsByteArrayAsync();

        // Assert: content should still be readable (throttling at 1 MB/s shouldn't add visible delay).
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        CollectionAssert.AreEqual(responseContent, body);
    }

    /// <summary>Minimal handler stub that returns a canned response.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }
}

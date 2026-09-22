using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Core.Server.Tests.Proxy;

/// <summary>
/// Unit tests for <see cref="Program.ModuleApiProxyTransformer"/>, which forwards module REST API
/// requests to the process-isolated module hosts.
/// </summary>
/// <remarks>
/// The transformer derives from YARP's <c>HttpTransformer</c>, whose base implementation already copies
/// the standard request headers. Because <c>TryAddWithoutValidation</c> <b>appends</b> rather than
/// replaces, any header that the base already copied must not be added a second time — a duplicated
/// <c>If-None-Match</c> for example arrives at the module host as two values, so a conditional request
/// can never match and every <c>304 Not Modified</c> silently degrades to a full <c>200</c> response.
/// </remarks>
[TestClass]
public class ModuleApiProxyTransformerTests
{
    [TestMethod]
    public async Task TransformRequestAsync_ForwardedHeaders_AreNotDuplicated()
    {
        var context = CreateContext();
        var proxyRequest = new HttpRequestMessage();

        await Program.ModuleApiProxyTransformer.Instance.TransformRequestAsync(
            context, proxyRequest, "http://localhost:50105/", CancellationToken.None);

        Assert.AreEqual(
            1,
            proxyRequest.Headers.IfNoneMatch.Count,
            "If-None-Match must be forwarded exactly once so conditional requests can answer 304.");
        Assert.AreEqual(
            1,
            proxyRequest.Headers.Authorization is null ? 0 : 1,
            "Authorization must be attached exactly once.");
        Assert.AreEqual(
            1,
            proxyRequest.Headers.Count(h => h.Key == "Authorization"),
            "Authorization must be attached exactly once (no duplicate header entries).");
    }

    [TestMethod]
    public async Task TransformRequestAsync_IfNoneMatch_PreservesTheEntityTagValue()
    {
        var context = CreateContext();
        var proxyRequest = new HttpRequestMessage();

        await Program.ModuleApiProxyTransformer.Instance.TransformRequestAsync(
            context, proxyRequest, "http://localhost:50105/", CancellationToken.None);

        var forwarded = proxyRequest.Headers.IfNoneMatch.SingleOrDefault();
        Assert.IsNotNull(forwarded, "The entity tag must survive the proxy hop.");
        Assert.AreEqual("\"abc123\"", forwarded.Tag);
    }

    [TestMethod]
    public async Task TransformRequestAsync_CustomHeader_IsForwarded()
    {
        var context = CreateContext();
        context.Request.Headers["X-Dnc-Custom"] = "custom-value";
        var proxyRequest = new HttpRequestMessage();

        await Program.ModuleApiProxyTransformer.Instance.TransformRequestAsync(
            context, proxyRequest, "http://localhost:50105/", CancellationToken.None);

        Assert.AreEqual(
            "custom-value",
            proxyRequest.Headers.GetValues("X-Dnc-Custom").SingleOrDefault(),
            "Headers the base transformer does not copy must still reach the module host.");
    }

    [TestMethod]
    public async Task TransformRequestAsync_EveryForwardedHeader_AppearsExactlyOnce()
    {
        var context = CreateContext();
        context.Request.Headers["X-Dnc-Custom"] = "custom-value";
        context.Request.Headers.Accept = "application/json";
        var proxyRequest = new HttpRequestMessage();

        await Program.ModuleApiProxyTransformer.Instance.TransformRequestAsync(
            context, proxyRequest, "http://localhost:50105/", CancellationToken.None);

        foreach (var name in new[] { "If-None-Match", "Authorization", "X-Device-Id", "X-Dnc-Custom", "Accept" })
        {
            Assert.AreEqual(
                1,
                proxyRequest.Headers.Count(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)),
                $"{name} must be forwarded exactly once.");
        }
    }

    [TestMethod]
    public async Task TransformRequestAsync_RequestCarryingContentType_DoesNotThrow()
    {
        // Regression (2026-09-21): probing presence with Headers.Contains("Content-Type") throws
        // "Misused header name, 'Content-Type'" because a content header is not valid on
        // HttpRequestMessage.Headers. YARP reports that as RequestCreation, so every proxied write —
        // Notes save, WOPI token (Collabora), Files upload/initiate — answered 502 Bad Gateway.
        var context = CreateContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/notes";
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = 42;
        var proxyRequest = new HttpRequestMessage();

        await Program.ModuleApiProxyTransformer.Instance.TransformRequestAsync(
            context, proxyRequest, "http://localhost:50200/", CancellationToken.None);
    }

    [TestMethod]
    public async Task TransformRequestAsync_RequestCarryingContentType_KeepsItOffTheRequestHeaders()
    {
        var context = CreateContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/notes";
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = 42;
        var proxyRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost:50200/api/v1/notes")
        {
            Content = new StringContent("{\"title\":\"x\"}", Encoding.UTF8, "application/json"),
        };

        await Program.ModuleApiProxyTransformer.Instance.TransformRequestAsync(
            context, proxyRequest, "http://localhost:50200/", CancellationToken.None);

        Assert.AreEqual(
            0,
            proxyRequest.Headers.Count(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)),
            "Content-Type is a content header and must never be added to HttpRequestMessage.Headers.");
        Assert.AreEqual(
            1,
            proxyRequest.Content.Headers.Count(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)),
            "Content-Type must reach the module host exactly once, on the request content.");
    }

    [TestMethod]
    public async Task TransformRequestAsync_RequestCarryingContentLength_DoesNotThrow()
    {
        var context = CreateContext();
        context.Request.Method = HttpMethods.Put;
        context.Request.Path = "/api/v1/files/chunk";
        context.Request.ContentLength = 1048576;
        context.Request.Headers["X-Dnc-Custom"] = "custom-value";
        var proxyRequest = new HttpRequestMessage();

        await Program.ModuleApiProxyTransformer.Instance.TransformRequestAsync(
            context, proxyRequest, "http://localhost:50194/", CancellationToken.None);

        Assert.AreEqual(
            "custom-value",
            proxyRequest.Headers.GetValues("X-Dnc-Custom").SingleOrDefault(),
            "Ordinary request headers must still be forwarded alongside content headers.");
    }

    /// <summary>
    /// Builds a request context carrying the headers the module hosts depend on.
    /// </summary>
    /// <returns>A context wired with an authenticated conditional GET.</returns>
    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("cloud.example.com");
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/v1/chat/alerts";
        context.Request.Headers.Authorization = "Bearer test-token";
        context.Request.Headers.IfNoneMatch = "\"abc123\"";
        context.Request.Headers["X-Device-Id"] = "device-1";
        return context;
    }
}

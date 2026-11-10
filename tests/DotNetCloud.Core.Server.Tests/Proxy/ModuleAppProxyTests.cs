using System.Security.Claims;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Proxy;
using DotNetCloud.Core.Server.Supervisor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DotNetCloud.Core.Server.Tests.Proxy;

/// <summary>
/// Security tests for the module UI plane (<c>/apps/{moduleId}/**</c>).
/// </summary>
/// <remarks>
/// The proxy is a trust boundary: a module UI must never see the browser's core session, must not be
/// able to set cookies on the core origin, and must not be able to relax the framing headers that
/// keep it inside the core's sandbox.
/// </remarks>
[TestClass]
public class ModuleAppProxyTests
{
    private const string ModuleId = "vendor.receipts";

    [TestMethod]
    public async Task TransformRequestAsync_BrowserCredentials_NeverReachTheModule()
    {
        using var keyRing = new ModuleAssertionKeyRing();
        var transformer = new ModuleAppProxyTransformer(ModuleId, keyRing);
        var userId = Guid.CreateVersion7();
        var context = CreateContext(userId);

        context.Request.Headers.Cookie = ".AspNetCore.Identity.Application=session";
        context.Request.Headers.Authorization = "Bearer user-token";
        context.Request.Headers[ModuleIdentityMetadata.UserAssertionHeader] = "forged-assertion";
        context.Request.Headers[ModuleIdentityMetadata.ModuleTokenHeader] = "forged-module-token";

        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        await transformer.TransformRequestAsync(context, proxyRequest, "http://localhost", CancellationToken.None);

        Assert.IsFalse(proxyRequest.Headers.Contains("Cookie"), "The core session cookie must never be forwarded.");
        Assert.IsNull(proxyRequest.Headers.Authorization, "A bearer token must never be forwarded.");
        Assert.IsFalse(
            proxyRequest.Headers.Contains(ModuleIdentityMetadata.ModuleTokenHeader),
            "A browser-supplied module credential must be dropped.");

        var assertions = proxyRequest.Headers.GetValues(ModuleIdentityMetadata.UserAssertionHeader).ToList();
        Assert.AreEqual(1, assertions.Count, "Exactly one core-minted assertion must be forwarded.");
        Assert.AreNotEqual("forged-assertion", assertions[0]);
    }

    [TestMethod]
    public async Task TransformRequestAsync_MintsAnAssertionBoundToTheModule()
    {
        using var keyRing = new ModuleAssertionKeyRing();
        var transformer = new ModuleAppProxyTransformer(ModuleId, keyRing);
        var userId = Guid.CreateVersion7();
        var context = CreateContext(userId);

        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        await transformer.TransformRequestAsync(context, proxyRequest, "http://localhost", CancellationToken.None);

        var token = proxyRequest.Headers.GetValues(ModuleIdentityMetadata.UserAssertionHeader).Single();

        Assert.IsTrue(keyRing.TryValidate(token, ModuleId, out var assertion));
        Assert.IsNotNull(assertion);
        Assert.AreEqual(userId, assertion.UserId);
        Assert.AreEqual(ModuleAppProxy.UiOperation, assertion.Operation);
    }

    [TestMethod]
    public async Task TransformRequestAsync_AssertionForOneModule_DoesNotValidateForAnother()
    {
        using var keyRing = new ModuleAssertionKeyRing();
        var transformer = new ModuleAppProxyTransformer(ModuleId, keyRing);
        var context = CreateContext(Guid.CreateVersion7());

        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        await transformer.TransformRequestAsync(context, proxyRequest, "http://localhost", CancellationToken.None);
        var token = proxyRequest.Headers.GetValues(ModuleIdentityMetadata.UserAssertionHeader).Single();

        Assert.IsFalse(keyRing.TryValidate(token, "dotnetcloud.files", out _));
    }

    [TestMethod]
    public async Task TransformRequestAsync_AnonymousRequest_MintsNoAssertion()
    {
        using var keyRing = new ModuleAssertionKeyRing();
        var transformer = new ModuleAppProxyTransformer(ModuleId, keyRing);
        var context = new DefaultHttpContext();

        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        await transformer.TransformRequestAsync(context, proxyRequest, "http://localhost", CancellationToken.None);

        Assert.IsFalse(proxyRequest.Headers.Contains(ModuleIdentityMetadata.UserAssertionHeader));
    }

    [TestMethod]
    public async Task TransformRequestAsync_AddsForwardedHeaders()
    {
        using var keyRing = new ModuleAssertionKeyRing();
        var transformer = new ModuleAppProxyTransformer(ModuleId, keyRing);
        var context = CreateContext(Guid.CreateVersion7());
        context.Request.Host = new HostString("cloud.example.com");

        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        await transformer.TransformRequestAsync(context, proxyRequest, "http://localhost", CancellationToken.None);

        Assert.AreEqual("https", proxyRequest.Headers.GetValues("X-Forwarded-Proto").Single());
        Assert.AreEqual("cloud.example.com", proxyRequest.Headers.GetValues("X-Forwarded-Host").Single());
    }

    [TestMethod]
    public async Task TransformResponseAsync_StripsModuleCookiesAndFramingHeaders()
    {
        using var keyRing = new ModuleAssertionKeyRing();
        var transformer = new ModuleAppProxyTransformer(ModuleId, keyRing);
        var proxyResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        proxyResponse.Headers.TryAddWithoutValidation("Set-Cookie", "session=module-secret; Path=/");
        proxyResponse.Headers.TryAddWithoutValidation("X-Frame-Options", "ALLOWALL");
        proxyResponse.Headers.TryAddWithoutValidation("Content-Security-Policy", "frame-ancestors *");
        proxyResponse.Headers.TryAddWithoutValidation("Server", "node");
        proxyResponse.Headers.TryAddWithoutValidation("X-Powered-By", "Express");

        await transformer.TransformResponseAsync(new DefaultHttpContext(), proxyResponse, CancellationToken.None);

        Assert.IsFalse(proxyResponse.Headers.Contains("Set-Cookie"), "A module must not be able to set cookies on the core origin.");
        Assert.IsFalse(proxyResponse.Headers.Contains("X-Frame-Options"));
        Assert.IsFalse(proxyResponse.Headers.Contains("Server"));
        Assert.IsFalse(proxyResponse.Headers.Contains("X-Powered-By"));

        var csp = proxyResponse.Headers.GetValues("Content-Security-Policy").Single();
        Assert.AreEqual(ModuleAppResponsePolicy.ContentSecurityPolicy, csp);
        Assert.IsTrue(csp.Contains("frame-ancestors 'self'", StringComparison.Ordinal));
        Assert.IsFalse(csp.Contains("frame-ancestors *", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ApplyForHtml_SetsNoStore()
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        ModuleAppResponsePolicy.Apply(response.Headers, "text/html; charset=utf-8");

        var cacheControl = response.Headers.CacheControl;
        Assert.IsNotNull(cacheControl);
        Assert.IsTrue(cacheControl!.NoStore);
        Assert.IsTrue(cacheControl.NoCache);
    }

    [TestMethod]
    public void ApplyForNonHtml_LeavesCachingAlone()
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        ModuleAppResponsePolicy.Apply(response.Headers, "text/css");

        Assert.IsNull(response.Headers.CacheControl);
        Assert.IsFalse(response.Headers.Contains("Set-Cookie"));
    }

    [TestMethod]
    public void Apply_AlwaysAddsNosniffAndReferrerPolicy()
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        ModuleAppResponsePolicy.Apply(response.Headers);

        Assert.AreEqual("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.AreEqual(ModuleAppResponsePolicy.ReferrerPolicy, response.Headers.GetValues("Referrer-Policy").Single());
    }

    [TestMethod]
    public void Apply_AllowsTheOpaqueFrameOriginSoTheFrameCanReadItsOwnApi()
    {
        // The frame is sandboxed without allow-same-origin, so its document's origin is opaque and
        // every fetch it makes is cross-origin. Without these two headers the module's own API
        // responses are unreadable and the UI is dead on arrival.
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        ModuleAppResponsePolicy.Apply(response.Headers);

        Assert.AreEqual(
            ModuleAppResponsePolicy.CrossOriginFrameOrigin,
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.AreEqual("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [TestMethod]
    public void Apply_ReplacesAModulesOwnCorsHeaders()
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Access-Control-Allow-Origin", "https://evil.example");

        ModuleAppResponsePolicy.Apply(response.Headers);

        // The proxy is the CORS authority for the plane; a module cannot widen it to a real origin.
        Assert.AreEqual(
            ModuleAppResponsePolicy.CrossOriginFrameOrigin,
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [TestMethod]
    public void ApplyPreflight_AllowsTheMethodsAndHeadersAModuleUiNeeds()
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        ModuleAppResponsePolicy.ApplyPreflight(response.Headers);

        Assert.AreEqual(ModuleAppResponsePolicy.PreflightAllowedMethods, response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.AreEqual(ModuleAppResponsePolicy.PreflightAllowedHeaders, response.Headers.GetValues("Access-Control-Allow-Headers").Single());
        Assert.IsTrue(
            response.Headers.GetValues("Access-Control-Allow-Methods").Single().Contains("PATCH", StringComparison.Ordinal),
            "the Email module patches accounts, and the plane must not block it");

        // A state-changing call from the frame carries the ticket header; if the preflight does not
        // allow it the browser never sends the real request and the module reports "unreachable".
        Assert.IsTrue(
            response.Headers.GetValues("Access-Control-Allow-Headers").Single()
                .Contains(ModuleAppProxy.FrameTicketHeader, StringComparison.OrdinalIgnoreCase),
            "the frame ticket header must survive the CORS preflight");
    }

    [TestMethod]
    public void TryStripAppBasePath_Root_BecomesSlash()
    {
        var context = CreateContext(Guid.CreateVersion7());
        context.Request.Path = $"/apps/{ModuleId}/";

        Assert.IsTrue(ModuleAppProxy.TryStripAppBasePath(context.Request, ModuleId));
        Assert.AreEqual($"/apps/{ModuleId}", context.Request.PathBase.Value);
        Assert.AreEqual("/", context.Request.Path.Value);
    }

    [TestMethod]
    public void TryStripAppBasePath_Nested_KeepsTheRemainder()
    {
        var context = CreateContext(Guid.CreateVersion7());
        context.Request.Path = $"/apps/{ModuleId}/assets/app.js";

        Assert.IsTrue(ModuleAppProxy.TryStripAppBasePath(context.Request, ModuleId));
        Assert.AreEqual("/assets/app.js", context.Request.Path.Value);
    }

    [TestMethod]
    public void TryStripAppBasePath_NoTrailingSlash_BecomesSlash()
    {
        var context = CreateContext(Guid.CreateVersion7());
        context.Request.Path = $"/apps/{ModuleId}";

        Assert.IsTrue(ModuleAppProxy.TryStripAppBasePath(context.Request, ModuleId));
        Assert.AreEqual("/", context.Request.Path.Value);
    }

    [TestMethod]
    public void TryStripAppBasePath_SiblingPrefix_IsRejected()
    {
        var context = CreateContext(Guid.CreateVersion7());
        context.Request.Path = $"/apps/{ModuleId}x/page";

        Assert.IsFalse(ModuleAppProxy.TryStripAppBasePath(context.Request, ModuleId));
    }

    [TestMethod]
    [DataRow("dotnetcloud.files", true)]
    [DataRow("vendor.receipts", true)]
    [DataRow("org.example.custom-app", true)]
    [DataRow("files", false)]
    [DataRow("dotnetcloud.", false)]
    [DataRow(".dotnetcloud.files", false)]
    [DataRow("dotnetcloud..files", false)]
    [DataRow("DotNetCloud.Files", false)]
    [DataRow("-bad.module", false)]
    [DataRow("", false)]
    public void IsValidModuleId_OnlyAcceptsReverseDns(string moduleId, bool expected)
    {
        Assert.AreEqual(expected, ModuleIdRouteConstraint.IsValidModuleId(moduleId));
    }

    [TestMethod]
    public void Match_FirstPartyShortName_IsNotCaptured()
    {
        var constraint = new ModuleIdRouteConstraint();

        var matched = constraint.Match(
            null,
            null,
            "moduleId",
            new RouteValueDictionary { ["moduleId"] = "files" },
            RouteDirection.IncomingRequest);

        Assert.IsFalse(matched, "/apps/files is a first-party page and must not reach the module app proxy.");
    }

    [TestMethod]
    public void Match_ReverseDnsModuleId_IsCaptured()
    {
        var constraint = new ModuleIdRouteConstraint();

        var matched = constraint.Match(
            null,
            null,
            "moduleId",
            new RouteValueDictionary { ["moduleId"] = ModuleId },
            RouteDirection.IncomingRequest);

        Assert.IsTrue(matched);
    }

    [TestMethod]
    public async Task CappedResponseStream_WithinLimit_WritesThrough()
    {
        var inner = new MemoryStream();
        await using var capped = new CappedResponseStream(inner, 8);

        await capped.WriteAsync(new byte[8]);

        Assert.AreEqual(8, capped.BytesWritten);
        Assert.AreEqual(8, inner.Length);
    }

    [TestMethod]
    public async Task CappedResponseStream_OverLimit_Faults()
    {
        await using var capped = new CappedResponseStream(new MemoryStream(), 4);

        await Assert.ThrowsExactlyAsync<IOException>(async () => await capped.WriteAsync(new byte[5]));
    }

    /// <summary>
    /// The cap is resolved on the first write, from a value the caller reads out of the response.
    /// </summary>
    [TestMethod]
    public async Task CappedResponseStream_ResolvedLimit_IsAppliedOnFirstWrite()
    {
        var inner = new MemoryStream();
        await using var capped = new CappedResponseStream(inner, () => 8);

        await capped.WriteAsync(new byte[8]);

        Assert.AreEqual(8, inner.Length);
        await Assert.ThrowsExactlyAsync<IOException>(async () => await capped.WriteAsync(new byte[1]));
    }

    /// <summary>
    /// A media or archive response is allowed to be large; everything else keeps the 32 MiB guard.
    /// </summary>
    /// <remarks>
    /// The proxy used one 32 MiB ceiling for every module response, so a 39 MiB FLAC track aborted
    /// mid-stream and the player reported "Failed to fetch" while the module had already answered 200.
    /// </remarks>
    [TestMethod]
    [DataRow("audio/flac", true)]
    [DataRow("audio/mpeg", true)]
    [DataRow("audio/ogg; codecs=opus", true)]
    [DataRow("video/mp4", true)]
    [DataRow("image/jpeg", true)]
    [DataRow("application/zip", true)]
    [DataRow("application/octet-stream", true)]
    [DataRow("application/json", false)]
    [DataRow("text/html; charset=utf-8", false)]
    [DataRow("application/xml", false)]
    [DataRow(null, false)]
    [DataRow("", false)]
    public void ResolveResponseCap_ContentType_SelectsTheCeiling(string? contentType, bool isMedia)
    {
        var expected = isMedia ? ModuleAppProxy.MaxMediaResponseBytes : ModuleAppProxy.MaxResponseBytes;

        Assert.AreEqual(expected, ModuleAppProxy.ResolveResponseCap(contentType));
        Assert.AreEqual(isMedia, ModuleAppProxy.IsMediaContentType(contentType));
    }

    /// <summary>A media body far above the UI cap must stream through untouched.</summary>
    [TestMethod]
    public async Task CappedResponseStream_MediaSizedBody_WithResolvedMediaCap_WritesThrough()
    {
        var inner = new MemoryStream();
        var payload = new byte[ModuleAppProxy.MaxResponseBytes + 1024];

        await using (var capped = new CappedResponseStream(
            inner,
            () => ModuleAppProxy.ResolveResponseCap("audio/flac")))
        {
            await capped.WriteAsync(payload);
        }

        Assert.AreEqual(payload.Length, inner.Length);
    }

    [TestMethod]
    public void ClampHeight_OutOfRange_IsClamped()
    {
        Assert.AreEqual(
            DotNetCloud.Core.Modules.Ui.ModuleAppProtocol.MinFrameHeight,
            DotNetCloud.Core.Modules.Ui.ModuleAppProtocol.ClampHeight(10));
        Assert.AreEqual(
            DotNetCloud.Core.Modules.Ui.ModuleAppProtocol.MaxFrameHeight,
            DotNetCloud.Core.Modules.Ui.ModuleAppProtocol.ClampHeight(1_000_000));
        Assert.AreEqual(
            DotNetCloud.Core.Modules.Ui.ModuleAppProtocol.MinFrameHeight,
            DotNetCloud.Core.Modules.Ui.ModuleAppProtocol.ClampHeight(double.NaN));
    }

    [TestMethod]
    [DataRow("https://example.org/path", true)]
    [DataRow("http://example.org/path", false)]
    [DataRow("javascript:alert(1)", false)]
    [DataRow("data:text/html,<script>alert(1)</script>", false)]
    [DataRow("//evil.example.org", false)]
    [DataRow("/relative", false)]
    [DataRow("", false)]
    public void IsAllowedExternalUrl_OnlyHttpsAbsolute(string url, bool expected)
    {
        Assert.AreEqual(expected, DotNetCloud.Core.Modules.Ui.ModuleAppUrls.IsAllowedExternalUrl(url));
    }

    // ── Frame ticket / anonymous static assets ────────────────────────────────
    // A sandboxed frame has an opaque origin, so the browser never attaches the SameSite session
    // cookie: without these rules every module UI 401s on its own CSS/JS and never handshakes.

    private static ModuleFrameTicketService CreateTicketService() =>
        new(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(), TimeProvider.System);

    [TestMethod]
    public void TryAdoptFrameTicket_HeaderForThisModule_AdoptsTheUser()
    {
        var service = CreateTicketService();
        var userId = Guid.CreateVersion7();
        var context = new DefaultHttpContext();
        context.Request.Path = $"/apps/{ModuleId}/";
        context.Request.Headers[ModuleAppProxy.FrameTicketHeader] = service.Issue(userId, ModuleId);

        Assert.IsTrue(ModuleAppProxy.TryAdoptFrameTicket(context, ModuleId, service));
        Assert.AreEqual(userId.ToString(), context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.AreEqual(userId.ToString(), context.User.FindFirst("sub")?.Value);
    }

    [TestMethod]
    public void TryAdoptFrameTicket_QueryForAnotherModule_IsRejected()
    {
        var service = CreateTicketService();
        var context = new DefaultHttpContext();
        context.Request.Path = $"/apps/{ModuleId}/";
        context.Request.QueryString = new QueryString(
            $"?{ModuleAppProxy.FrameTicketQuery}={Uri.EscapeDataString(service.Issue(Guid.CreateVersion7(), "dotnetcloud.email"))}");

        Assert.IsFalse(ModuleAppProxy.TryAdoptFrameTicket(context, ModuleId, service));
        Assert.IsFalse(context.User.Identity?.IsAuthenticated ?? false);
    }

    [TestMethod]
    [DataRow("/apps/vendor.receipts/assets/app.css", true)]
    [DataRow("/apps/vendor.receipts/assets/app.js", true)]
    [DataRow("/apps/vendor.receipts/fonts/inter.woff2", true)]
    [DataRow("/apps/vendor.receipts/", false)]
    [DataRow("/apps/vendor.receipts", false)]
    [DataRow("/apps/vendor.receipts/api/items", false)]
    [DataRow("/apps/vendor.receipts/api/items.json", false)]
    [DataRow("/apps/vendor.receiptsx/assets/app.css", false)]
    public void IsAnonymousStaticAsset_OnlyModuleAssets(string path, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;

        Assert.AreEqual(expected, ModuleAppProxy.IsAnonymousStaticAsset(context.Request, ModuleId));
    }

    [TestMethod]
    public void IsAnonymousStaticAsset_PostToAnAssetPath_IsNotAnonymous()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/apps/vendor.receipts/assets/app.js";

        Assert.IsFalse(ModuleAppProxy.IsAnonymousStaticAsset(context.Request, ModuleId));
    }

    [TestMethod]
    public void RemoveFrameTicketFromQuery_KeepsOtherParameters()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?dncTicket=abc&threadId=t-1&x=2");

        ModuleAppProxy.RemoveFrameTicketFromQuery(context.Request);

        Assert.AreEqual("?threadId=t-1&x=2", context.Request.QueryString.Value);
    }

    [TestMethod]
    public void RemoveFrameTicketFromQuery_OnlyTicket_BecomesEmpty()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?dncTicket=abc");

        ModuleAppProxy.RemoveFrameTicketFromQuery(context.Request);

        Assert.AreEqual(string.Empty, context.Request.QueryString.Value);
    }

    private static DefaultHttpContext CreateContext(Guid userId)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}

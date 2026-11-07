using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Consent;
using DotNetCloud.Core.Server.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.Server.Tests.Consent;

/// <summary>
/// Tests for the consent decision endpoints (F9): POST-only, anti-forgery protected, non-frameable,
/// and unable to approve scopes the user was never shown.
/// </summary>
[TestClass]
public class ConsentEndpointsTests
{
    private const string ModuleId = "vendor.receipts";

    private static readonly Guid UserId = Guid.CreateVersion7();

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Consent = new ConsentTestDoubles.FakeConsentService([]);
            Audit = new RecordingAuditLogger();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions();
            services.AddAntiforgery();
            services.AddDataProtection();
            services.AddSingleton<TimeProvider>(TimeProvider.System);
            services.AddSingleton<ConsentRequestStore>();
            services.AddSingleton<IModuleConsentService>(Consent);
            services.AddSingleton<IAuditLogger>(Audit);

            Services = services.BuildServiceProvider();

            Store = Services.GetRequiredService<ConsentRequestStore>();
        }

        public ServiceProvider Services { get; }

        public ConsentRequestStore Store { get; }

        public ConsentTestDoubles.FakeConsentService Consent { get; }

        public RecordingAuditLogger Audit { get; }

        public string CreatePending() => Store.Create(
            UserId,
            "dnc-module-vendor-receipts",
            ModuleId,
            "Receipt Scanner",
            [ModuleScopes.FilesRead],
            "?client_id=dnc-module-vendor-receipts&scope=files:read");

        public DefaultHttpContext CreateContext(bool authenticated = true)
        {
            var http = new DefaultHttpContext
            {
                RequestServices = Services,
            };

            http.Response.Body = new MemoryStream();
            http.Request.Path = ConsentEndpoints.Path;
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("cloud.example.test");

            if (authenticated)
            {
                var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId.ToString())], "test");
                http.User = new ClaimsPrincipal(identity);
            }

            return http;
        }

        public void Dispose() => Services.Dispose();
    }

    private static async Task<(int Status, string Body)> ExecuteAsync(IResult result, DefaultHttpContext http)
    {
        await result.ExecuteAsync(http);

        http.Response.Body.Position = 0;
        using var reader = new StreamReader(http.Response.Body);
        return (http.Response.StatusCode, await reader.ReadToEndAsync());
    }

    [TestMethod]
    public async Task Get_Unauthenticated_Challenges()
    {
        using var fixture = new Fixture();
        var http = fixture.CreateContext(authenticated: false);
        http.Request.QueryString = new QueryString($"?rid={fixture.CreatePending()}");

        var result = await ConsentEndpoints.HandleGetAsync(http);

        Assert.IsInstanceOfType<ChallengeHttpResult>(result);
    }

    [TestMethod]
    public async Task Get_UnknownPromptId_Returns400()
    {
        using var fixture = new Fixture();
        var http = fixture.CreateContext();
        http.Request.QueryString = new QueryString("?rid=not-a-real-id");

        var (status, _) = await ExecuteAsync(await ConsentEndpoints.HandleGetAsync(http), http);

        Assert.AreEqual(StatusCodes.Status400BadRequest, status);
    }

    [TestMethod]
    public async Task Get_KnownPrompt_RendersThePromptWithNonFrameableHeaders()
    {
        using var fixture = new Fixture();
        var http = fixture.CreateContext();
        http.Request.QueryString = new QueryString($"?rid={fixture.CreatePending()}");

        var (status, body) = await ExecuteAsync(await ConsentEndpoints.HandleGetAsync(http), http);

        Assert.AreEqual(StatusCodes.Status200OK, status);
        StringAssert.Contains(body, "Receipt Scanner");
        StringAssert.Contains(body, "Read your files and folders.");

        Assert.AreEqual("DENY", http.Response.Headers["X-Frame-Options"].ToString());
        StringAssert.Contains(http.Response.Headers["Content-Security-Policy"].ToString(), "frame-ancestors 'none'");
        StringAssert.Contains(http.Response.Headers["Content-Security-Policy"].ToString(), "form-action 'self'");
        StringAssert.Contains(http.Response.Headers["Cache-Control"].ToString(), "no-store");
    }

    [TestMethod]
    public async Task Post_WithoutAntiforgeryToken_IsRejectedAndGrantsNothing()
    {
        using var fixture = new Fixture();
        var pendingId = fixture.CreatePending();

        var http = fixture.CreateContext();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["rid"] = pendingId,
            ["decision"] = "allow",
        });

        var (status, _) = await ExecuteAsync(await ConsentEndpoints.HandlePostAsync(http), http);

        Assert.AreEqual(StatusCodes.Status400BadRequest, status);
        Assert.AreEqual(
            0,
            fixture.Consent.Granted.Count,
            "A request without a valid anti-forgery token must not approve anything.");
        Assert.AreEqual(ConsentDecision.Pending, fixture.Store.Find(pendingId, UserId)!.Decision);
    }

    [TestMethod]
    public async Task Post_Unauthenticated_IsRejected()
    {
        using var fixture = new Fixture();
        var http = fixture.CreateContext(authenticated: false);
        http.Request.Method = "POST";

        var result = await ConsentEndpoints.HandlePostAsync(http);

        Assert.IsInstanceOfType<UnauthorizedHttpResult>(result);
    }

    [TestMethod]
    public async Task Post_UnknownDecision_IsRejected()
    {
        using var fixture = new Fixture();
        var pendingId = fixture.CreatePending();

        var (http, tokens) = CreatePostableRequest(fixture, pendingId, decision: "maybe");

        var (status, _) = await ExecuteAsync(await ConsentEndpoints.HandlePostAsync(http), http);

        Assert.AreEqual(StatusCodes.Status400BadRequest, status);
        Assert.AreEqual(0, fixture.Consent.Granted.Count);
        Assert.AreEqual(ConsentDecision.Pending, fixture.Store.Find(pendingId, UserId)!.Decision);
    }

    [TestMethod]
    public async Task Post_Allow_GrantsTheShownScopesAndRedirectsToAuthorize()
    {
        using var fixture = new Fixture();
        var pendingId = fixture.CreatePending();
        var (http, _) = CreatePostableRequest(fixture, pendingId, decision: "allow");

        var (status, _) = await ExecuteAsync(await ConsentEndpoints.HandlePostAsync(http), http);

        Assert.AreEqual(StatusCodes.Status302Found, status);
        Assert.AreEqual(
            "/connect/authorize?client_id=dnc-module-vendor-receipts&scope=files:read&dnc_consent="
            + Uri.EscapeDataString(pendingId),
            http.Response.Headers.Location.ToString());

        CollectionAssert.AreEqual(new[] { ModuleScopes.FilesRead }, fixture.Consent.Granted.ToArray());
        Assert.AreEqual(ConsentDecision.Approved, fixture.Store.Find(pendingId, UserId)!.Decision);
        Assert.AreEqual(AuditAction.ModuleConsentGranted, fixture.Audit.Entries.Single().Action);
    }

    [TestMethod]
    public async Task Post_Deny_RecordsNoGrantAndRedirectsToAuthorize()
    {
        using var fixture = new Fixture();
        var pendingId = fixture.CreatePending();
        var (http, _) = CreatePostableRequest(fixture, pendingId, decision: "deny");

        var (status, _) = await ExecuteAsync(await ConsentEndpoints.HandlePostAsync(http), http);

        Assert.AreEqual(StatusCodes.Status302Found, status);
        Assert.AreEqual(0, fixture.Consent.Granted.Count);
        Assert.AreEqual(ConsentDecision.Denied, fixture.Store.Find(pendingId, UserId)!.Decision);
        Assert.AreEqual(AuditAction.ModuleConsentDenied, fixture.Audit.Entries.Single().Action);
    }

    [TestMethod]
    public async Task Post_ReplaysOfAnAnsweredPrompt_AreRejected()
    {
        using var fixture = new Fixture();
        var pendingId = fixture.CreatePending();

        var (first, _) = CreatePostableRequest(fixture, pendingId, decision: "deny");
        await ConsentEndpoints.HandlePostAsync(first);

        var (second, _) = CreatePostableRequest(fixture, pendingId, decision: "allow");
        var (status, _) = await ExecuteAsync(await ConsentEndpoints.HandlePostAsync(second), second);

        Assert.AreEqual(StatusCodes.Status400BadRequest, status);
        Assert.AreEqual(0, fixture.Consent.Granted.Count, "A captured prompt must not be able to flip the answer.");
    }

    /// <summary>
    /// Builds a POST whose anti-forgery cookie and token come from the real token source, so the
    /// test exercises the same validation the browser flow does.
    /// </summary>
    private static (DefaultHttpContext Http, AntiforgeryTokenSet Tokens) CreatePostableRequest(
        Fixture fixture,
        string pendingId,
        string decision)
    {
        var get = fixture.CreateContext();
        var antiforgery = get.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(get);

        var cookie = string.Join(
            ';',
            get.Response.Headers.SetCookie
                .Select(value => value?.Split(';', 2)[0])
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        var http = fixture.CreateContext();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Headers.Cookie = cookie;
        http.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            [tokens.FormFieldName] = tokens.RequestToken,
            ["rid"] = pendingId,
            ["decision"] = decision,
        });

        return (http, tokens);
    }
}

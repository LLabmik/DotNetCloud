using System.Net;
using DotNetCloud.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for cross-site request forgery protection on the cookie-authenticated
/// session form endpoints.
/// </summary>
/// <remarks>
/// The login form used to post without any antiforgery token, so a third-party page could sign a
/// victim into an attacker-controlled account and have the victim's subsequent work created inside
/// it. These tests drive the real pipeline rather than the controller methods, so the
/// <c>ValidateAntiForgeryToken</c> filter is actually exercised.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
public class AuthSessionCsrfIntegrationTests
{
    private static DotNetCloudWebApplicationFactory _factory = null!;
    private static HttpClient _client = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _factory = new DotNetCloudWebApplicationFactory();

        // The antiforgery cookie is Secure-always, so the request must look like HTTPS.
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [TestMethod]
    public async Task Login_WithTokenFromRenderedForm_ReachesCredentialCheck()
    {
        // Renders the real login page and reuses the token it emits, which proves the token is
        // actually rendered — otherwise the control would lock every user out.
        using var page = await _client.GetAsync("/auth/login");
        page.EnsureSuccessStatusCode();

        var html = await page.Content.ReadAsStringAsync();
        var token = ExtractAntiforgeryToken(html);

        Assert.IsFalse(string.IsNullOrEmpty(token), "The login form must render an antiforgery token.");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token!,
            ["username"] = "csrf-probe",
            ["password"] = "wrong-password",
        });

        using var response = await _client.PostAsync("/auth/session/login", content);

        // The token passed validation, so the request reached the credential check and the
        // controller's normal "invalid credentials" redirect was produced.
        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther,
            $"Expected the normal login flow after a valid token, got {response.StatusCode}.");
    }

    [TestMethod]
    public async Task LoginWithoutAntiforgeryToken_ReturnsBadRequestAndSetsNoCookie()
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "csrf-probe",
            ["password"] = "irrelevant",
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/session/login")
        {
            Content = content,
        };

        // Simulate a cross-site post: no token field, and a foreign origin.
        request.Headers.Add("Origin", "https://attacker.example");

        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode,
            $"A token-less login post must be rejected before any credential is checked. Body: {body}");
        Assert.IsFalse(
            response.Headers.Contains("Set-Cookie"),
            "A rejected cross-site post must not issue an auth cookie.");
    }

    [TestMethod]
    public async Task LogoutWithoutAntiforgeryToken_ReturnsBadRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/session/logout");

        using var response = await _client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string? ExtractAntiforgeryToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\"";
        var markerIndex = html.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var valueMarker = "value=\"";
        var valueStart = html.IndexOf(valueMarker, markerIndex, StringComparison.Ordinal);
        if (valueStart < 0)
        {
            return null;
        }

        valueStart += valueMarker.Length;
        var valueEnd = html.IndexOf('"', valueStart);
        return valueEnd > valueStart ? html[valueStart..valueEnd] : null;
    }
}

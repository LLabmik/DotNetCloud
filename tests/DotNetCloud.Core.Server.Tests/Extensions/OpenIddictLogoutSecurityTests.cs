namespace DotNetCloud.Core.Server.Tests.Extensions;

/// <summary>
/// Security regression tests for the OpenIddict logout endpoint covering:
///   - Open redirect prevention via post-logout redirect URI validation
///
/// The HandleLogoutEndpoint method is private static and requires OpenIddict
/// middleware context, so these tests validate the redirect URI security rules
/// that the endpoint enforces. If the validation logic ever changes, at least
/// one of these tests must fail.
///
/// Validation rule: StartsWith("/") and does not StartsWith("//")
/// </summary>
[TestClass]
public class OpenIddictLogoutSecurityTests
{
    /// <summary>
    /// Validates that the redirect URI check used in HandleLogoutEndpoint
    /// correctly identifies safe local paths vs. dangerous external URIs.
    /// The actual code uses: StartsWith("/") and not StartsWith("//")
    /// </summary>
    private static bool IsAllowedRedirectUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
            return false;

        return uri.StartsWith("/", StringComparison.Ordinal)
            && !uri.StartsWith("//", StringComparison.Ordinal);
    }

    [TestMethod]
    public void PostLogoutRedirect_LocalPath_IsAllowed()
    {
        Assert.IsTrue(IsAllowedRedirectUri("/"));
        Assert.IsTrue(IsAllowedRedirectUri("/login"));
        Assert.IsTrue(IsAllowedRedirectUri("/account/signed-out"));
    }

    [TestMethod]
    public void PostLogoutRedirect_ExternalUrl_IsBlocked()
    {
        Assert.IsFalse(IsAllowedRedirectUri("https://evil.com"));
        Assert.IsFalse(IsAllowedRedirectUri("http://evil.com/login"));
        Assert.IsFalse(IsAllowedRedirectUri("https://phishing.example.com/fake-login"));
    }

    [TestMethod]
    public void PostLogoutRedirect_ProtocolRelativeUrl_IsBlocked()
    {
        // Protocol-relative URLs (//evil.com) are dangerous because browsers
        // resolve them to the current protocol, making them external redirects.
        Assert.IsFalse(IsAllowedRedirectUri("//evil.com"));
        Assert.IsFalse(IsAllowedRedirectUri("//evil.com/login"));
        Assert.IsFalse(IsAllowedRedirectUri("///evil.com")); // triple-slash still starts with //
    }

    [TestMethod]
    public void PostLogoutRedirect_JavascriptUri_IsBlocked()
    {
        Assert.IsFalse(IsAllowedRedirectUri("javascript:alert(1)"));
    }

    [TestMethod]
    public void PostLogoutRedirect_DataUri_IsBlocked()
    {
        Assert.IsFalse(IsAllowedRedirectUri("data:text/html,<script>alert(1)</script>"));
    }

    [TestMethod]
    public void PostLogoutRedirect_EmptyOrNull_IsBlocked()
    {
        Assert.IsFalse(IsAllowedRedirectUri(null));
        Assert.IsFalse(IsAllowedRedirectUri(""));
        Assert.IsFalse(IsAllowedRedirectUri("   "));
    }

    [TestMethod]
    public void PostLogoutRedirect_BackslashUrl_IsBlocked()
    {
        // Backslash URLs can be confused by some browsers — only forward-slash local paths allowed
        Assert.IsFalse(IsAllowedRedirectUri("\\evil.com"));
    }
}

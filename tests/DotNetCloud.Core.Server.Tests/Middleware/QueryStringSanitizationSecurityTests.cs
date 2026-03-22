using System.Reflection;
using DotNetCloud.Core.ServiceDefaults.Middleware;
using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Server.Tests.Middleware;

/// <summary>
/// Security regression tests for RequestResponseLoggingMiddleware query string sanitization.
/// Ensures sensitive tokens (access_token, password, API keys, etc.) are redacted from
/// log output to prevent credential leakage in log files.
///
/// Tests use reflection to access the private static SanitizeQueryString method.
/// </summary>
[TestClass]
public class QueryStringSanitizationSecurityTests
{
    private static readonly MethodInfo SanitizeMethod =
        typeof(RequestResponseLoggingMiddleware)
            .GetMethod("SanitizeQueryString", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string SanitizeQueryString(string queryString)
    {
        var qs = new QueryString(queryString);
        return (string)SanitizeMethod.Invoke(null, [qs])!;
    }

    [TestMethod]
    public void SanitizeQueryString_ReflectionSetup_MethodExists()
    {
        Assert.IsNotNull(SanitizeMethod,
            "SanitizeQueryString private static method must exist on RequestResponseLoggingMiddleware");
    }

    [TestMethod]
    public void SanitizeQueryString_AccessToken_IsRedacted()
    {
        var result = SanitizeQueryString("?access_token=eyJhbGciOiJSUz&scope=openid");

        Assert.IsTrue(result.Contains("access_token=***REDACTED***"),
            "access_token must be redacted from query strings");
        Assert.IsTrue(result.Contains("scope=openid"),
            "Non-sensitive params must be preserved");
        Assert.IsFalse(result.Contains("eyJhbGciOiJSUz"),
            "Token value must not appear in sanitized output");
    }

    [TestMethod]
    public void SanitizeQueryString_Password_IsRedacted()
    {
        var result = SanitizeQueryString("?user=admin&password=s3cret!");

        Assert.IsTrue(result.Contains("password=***REDACTED***"));
        Assert.IsTrue(result.Contains("user=admin"));
        Assert.IsFalse(result.Contains("s3cret!"));
    }

    [TestMethod]
    public void SanitizeQueryString_Token_IsRedacted()
    {
        var result = SanitizeQueryString("?token=abc123");

        Assert.IsTrue(result.Contains("token=***REDACTED***"));
        Assert.IsFalse(result.Contains("abc123"));
    }

    [TestMethod]
    public void SanitizeQueryString_ApiKey_IsRedacted()
    {
        var result = SanitizeQueryString("?api_key=my-secret-key");
        Assert.IsTrue(result.Contains("api_key=***REDACTED***"));
        Assert.IsFalse(result.Contains("my-secret-key"));
    }

    [TestMethod]
    public void SanitizeQueryString_ApiKeyAlternateSpelling_IsRedacted()
    {
        var result = SanitizeQueryString("?apikey=abc");
        Assert.IsTrue(result.Contains("apikey=***REDACTED***"));
    }

    [TestMethod]
    public void SanitizeQueryString_Secret_IsRedacted()
    {
        var result = SanitizeQueryString("?secret=vault-token");
        Assert.IsTrue(result.Contains("secret=***REDACTED***"));
    }

    [TestMethod]
    public void SanitizeQueryString_RefreshToken_IsRedacted()
    {
        var result = SanitizeQueryString("?refresh_token=rt-abc123");
        Assert.IsTrue(result.Contains("refresh_token=***REDACTED***"));
    }

    [TestMethod]
    public void SanitizeQueryString_ClientSecret_IsRedacted()
    {
        var result = SanitizeQueryString("?client_secret=cs-xyz");
        Assert.IsTrue(result.Contains("client_secret=***REDACTED***"));
    }

    [TestMethod]
    public void SanitizeQueryString_Key_IsRedacted()
    {
        var result = SanitizeQueryString("?key=important");
        Assert.IsTrue(result.Contains("key=***REDACTED***"));
    }

    [TestMethod]
    public void SanitizeQueryString_NonSensitiveParams_Preserved()
    {
        var result = SanitizeQueryString("?page=1&size=25&sort=name&filter=active");

        Assert.IsTrue(result.Contains("page=1"));
        Assert.IsTrue(result.Contains("size=25"));
        Assert.IsTrue(result.Contains("sort=name"));
        Assert.IsTrue(result.Contains("filter=active"));
    }

    [TestMethod]
    public void SanitizeQueryString_CaseInsensitive_RedactsUppercaseKeys()
    {
        var result = SanitizeQueryString("?ACCESS_TOKEN=abc&Password=xyz");

        Assert.IsTrue(result.Contains("***REDACTED***"),
            "Sensitive param matching should be case-insensitive");
        Assert.IsFalse(result.Contains("abc"));
        Assert.IsFalse(result.Contains("xyz"));
    }

    [TestMethod]
    public void SanitizeQueryString_EmptyQueryString_ReturnsEmpty()
    {
        var qs = new QueryString();
        var result = (string)SanitizeMethod.Invoke(null, [qs])!;
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void SanitizeQueryString_MultipleSensitiveParams_AllRedacted()
    {
        var result = SanitizeQueryString(
            "?access_token=tok1&refresh_token=tok2&client_secret=my-client-secret&page=1");

        Assert.IsTrue(result.Contains("access_token=***REDACTED***"));
        Assert.IsTrue(result.Contains("refresh_token=***REDACTED***"));
        Assert.IsTrue(result.Contains("client_secret=***REDACTED***"));
        Assert.IsTrue(result.Contains("page=1"));
        Assert.IsFalse(result.Contains("tok1"));
        Assert.IsFalse(result.Contains("tok2"));
        // Verify the actual secret value "my-client-secret" doesn't appear
        Assert.IsFalse(result.Contains("my-client-secret"));
    }
}

using DotNetCloud.Core.Server.Configuration;

namespace DotNetCloud.Core.Server.Tests.Configuration;

[TestClass]
public class CorsOptionsTests
{
    [TestMethod]
    public void DefaultOptions_HasCorrectDefaults()
    {
        var options = new CorsOptions();

        Assert.AreEqual(0, options.AllowedOrigins.Length);
        Assert.AreEqual(7, options.AllowedMethods.Length);
        Assert.IsTrue(options.AllowedMethods.Contains("GET"));
        Assert.IsTrue(options.AllowedMethods.Contains("POST"));
        Assert.IsTrue(options.AllowedMethods.Contains("PUT"));
        Assert.IsTrue(options.AllowedMethods.Contains("PATCH"));
        Assert.IsTrue(options.AllowedMethods.Contains("DELETE"));
        Assert.IsTrue(options.AllowedMethods.Contains("OPTIONS"));
        Assert.IsTrue(options.AllowedMethods.Contains("HEAD"));
        Assert.IsTrue(options.AllowedHeaders.Length > 0);
        Assert.IsTrue(options.AllowedHeaders.Contains("Authorization"));
        Assert.IsTrue(options.AllowedHeaders.Contains("Content-Type"));
        Assert.IsTrue(options.ExposedHeaders.Length > 0);
        Assert.IsTrue(options.ExposedHeaders.Contains("X-Api-Version"));
        Assert.IsTrue(options.AllowCredentials);
        Assert.AreEqual(600, options.PreflightMaxAgeSeconds);
    }

    [TestMethod]
    public void Options_CanBeConfigured()
    {
        var options = new CorsOptions
        {
            AllowedOrigins = ["https://mycloud.example.com"],
            AllowedMethods = ["GET", "POST"],
            AllowedHeaders = ["Authorization"],
            ExposedHeaders = ["X-Custom-Header"],
            AllowCredentials = false,
            PreflightMaxAgeSeconds = 1200
        };

        Assert.AreEqual(1, options.AllowedOrigins.Length);
        Assert.AreEqual("https://mycloud.example.com", options.AllowedOrigins[0]);
        Assert.AreEqual(2, options.AllowedMethods.Length);
        Assert.AreEqual(1, options.AllowedHeaders.Length);
        Assert.AreEqual(1, options.ExposedHeaders.Length);
        Assert.IsFalse(options.AllowCredentials);
        Assert.AreEqual(1200, options.PreflightMaxAgeSeconds);
    }

    [TestMethod]
    public void ExposedHeaders_ContainsRateLimitHeaders()
    {
        var options = new CorsOptions();

        Assert.IsTrue(options.ExposedHeaders.Contains("X-RateLimit-Limit"));
        Assert.IsTrue(options.ExposedHeaders.Contains("X-RateLimit-Remaining"));
        Assert.IsTrue(options.ExposedHeaders.Contains("X-RateLimit-Reset"));
        Assert.IsTrue(options.ExposedHeaders.Contains("Retry-After"));
    }

    [TestMethod]
    public void ExposedHeaders_ContainsVersioningHeaders()
    {
        var options = new CorsOptions();

        Assert.IsTrue(options.ExposedHeaders.Contains("X-Api-Version"));
        Assert.IsTrue(options.ExposedHeaders.Contains("X-Api-Deprecated"));
    }
}

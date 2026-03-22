using DotNetCloud.Core.Server.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.Server.Tests.Configuration;

/// <summary>
/// Security regression tests for CORS configuration covering:
///   - Empty AllowedOrigins results in deny-all (not AllowAnyOrigin)
///   - Explicit origins are used when provided
///   - AllowAnyOrigin is never called
/// </summary>
[TestClass]
public class CorsConfigurationSecurityTests
{
    [TestMethod]
    public void AddDotNetCloudCors_NoOriginsConfigured_PolicyDeniesAllOrigins()
    {
        // When no origins are configured, the policy must use SetIsOriginAllowed(_ => false)
        // and NEVER fall back to AllowAnyOrigin(), which would bypass CORS protection entirely.
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>());

        services.AddDotNetCloudCors(config);

        var provider = services.BuildServiceProvider();
        var corsOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>();
        var policy = corsOptions.Value.GetPolicy(CorsConfiguration.PolicyName);

        Assert.IsNotNull(policy);
        // AllowAnyOrigin sets a wildcard — our policy must NOT do that
        Assert.IsFalse(policy.AllowAnyOrigin, "CORS policy must NOT allow any origin when no origins are configured");

        // Verify that arbitrary origins are rejected
        Assert.IsFalse(policy.IsOriginAllowed("https://evil.com"),
            "CORS policy must reject arbitrary origins when no configured origins");
        Assert.IsFalse(policy.IsOriginAllowed("https://localhost"),
            "CORS policy must reject localhost when no configured origins");
    }

    [TestMethod]
    public void AddDotNetCloudCors_ExplicitOriginsConfigured_OnlyAllowsThoseOrigins()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://mycloud.example.com",
            ["Cors:AllowedOrigins:1"] = "https://admin.example.com",
        });

        services.AddDotNetCloudCors(config);

        var provider = services.BuildServiceProvider();
        var corsOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>();
        var policy = corsOptions.Value.GetPolicy(CorsConfiguration.PolicyName);

        Assert.IsNotNull(policy);
        Assert.IsFalse(policy.AllowAnyOrigin);

        Assert.IsTrue(policy.IsOriginAllowed("https://mycloud.example.com"),
            "Configured origin must be allowed");
        Assert.IsTrue(policy.IsOriginAllowed("https://admin.example.com"),
            "Second configured origin must be allowed");
        Assert.IsFalse(policy.IsOriginAllowed("https://evil.com"),
            "Unconfigured origin must be rejected");
    }

    [TestMethod]
    public void AddDotNetCloudCors_PolicyHasCorrectName()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>());

        services.AddDotNetCloudCors(config);

        var provider = services.BuildServiceProvider();
        var corsOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>();

        Assert.IsNotNull(corsOptions.Value.GetPolicy(CorsConfiguration.PolicyName));
    }

    [TestMethod]
    public void AddDotNetCloudCors_CredentialsAllowedByDefault()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://mycloud.example.com",
        });

        services.AddDotNetCloudCors(config);

        var provider = services.BuildServiceProvider();
        var corsOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>();
        var policy = corsOptions.Value.GetPolicy(CorsConfiguration.PolicyName);

        Assert.IsNotNull(policy);
        Assert.IsTrue(policy.SupportsCredentials,
            "CORS policy should support credentials by default for cookie/auth header support");
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}

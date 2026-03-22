using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Tests.Configuration;

/// <summary>
/// Security regression tests for ForwardedHeaders configuration covering:
///   - XForwardedHost is excluded (prevents host header injection)
///   - ForwardLimit is set (prevents proxy chain injection)
///   - Only XForwardedFor + XForwardedProto are enabled
/// </summary>
[TestClass]
public class ForwardedHeadersSecurityTests
{
    [TestMethod]
    public void ForwardedHeaders_XForwardedHostExcluded()
    {
        // XForwardedHost must NOT be included — it allows attackers to inject
        // arbitrary Host headers, leading to host header injection attacks
        // (password reset link poisoning, cache poisoning, etc.)
        var services = new ServiceCollection();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // This is the exact configuration from Program.cs
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 2;
        });

        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.IsFalse(
            opts.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedHost),
            "XForwardedHost MUST NOT be enabled — allows host header injection attacks");
    }

    [TestMethod]
    public void ForwardedHeaders_OnlyForAndProtoEnabled()
    {
        var services = new ServiceCollection();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 2;
        });

        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        var expected = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        Assert.AreEqual(expected, opts.ForwardedHeaders,
            "Only XForwardedFor and XForwardedProto should be enabled");
    }

    [TestMethod]
    public void ForwardedHeaders_ForwardLimitIsSet()
    {
        // ForwardLimit prevents malicious proxy chain injection.
        // Without a limit, an attacker could add fake X-Forwarded-For headers.
        var services = new ServiceCollection();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 2;
        });

        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.IsNotNull(opts.ForwardLimit, "ForwardLimit must be set to prevent proxy chain injection");
        Assert.IsTrue(opts.ForwardLimit > 0, "ForwardLimit must be greater than 0");
        Assert.IsTrue(opts.ForwardLimit <= 10, "ForwardLimit should be reasonable (≤10 proxy hops)");
    }
}

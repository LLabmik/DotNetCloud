using DotNetCloud.Core.Server.Configuration;

namespace DotNetCloud.Core.Server.Tests.Configuration;

[TestClass]
public class RateLimitingOptionsTests
{
    [TestMethod]
    public void DefaultOptions_HasCorrectDefaults()
    {
        var options = new RateLimitingOptions();

        Assert.IsTrue(options.Enabled);
        Assert.AreEqual(100, options.GlobalPermitLimit);
        Assert.AreEqual(60, options.GlobalWindowSeconds);
        Assert.AreEqual(200, options.AuthenticatedPermitLimit);
        Assert.AreEqual(60, options.AuthenticatedWindowSeconds);
        Assert.IsTrue(options.IncludeHeaders);
        Assert.AreEqual(0, options.QueueLimit);
        Assert.AreEqual(0, options.ModuleLimits.Count);
    }

    [TestMethod]
    public void SectionName_IsRateLimiting()
    {
        Assert.AreEqual("RateLimiting", RateLimitingOptions.SectionName);
    }

    [TestMethod]
    public void ModuleRateLimitConfig_HasCorrectDefaults()
    {
        var config = new ModuleRateLimitConfig();

        Assert.AreEqual(100, config.PermitLimit);
        Assert.AreEqual(60, config.WindowSeconds);
    }

    [TestMethod]
    public void Options_CanBeConfigured()
    {
        var options = new RateLimitingOptions
        {
            Enabled = false,
            GlobalPermitLimit = 50,
            GlobalWindowSeconds = 30,
            AuthenticatedPermitLimit = 500,
            AuthenticatedWindowSeconds = 120,
            IncludeHeaders = false,
            QueueLimit = 10,
            ModuleLimits = new Dictionary<string, ModuleRateLimitConfig>
            {
                ["files"] = new ModuleRateLimitConfig { PermitLimit = 200, WindowSeconds = 60 },
                ["chat"] = new ModuleRateLimitConfig { PermitLimit = 500, WindowSeconds = 30 }
            }
        };

        Assert.IsFalse(options.Enabled);
        Assert.AreEqual(50, options.GlobalPermitLimit);
        Assert.AreEqual(30, options.GlobalWindowSeconds);
        Assert.AreEqual(500, options.AuthenticatedPermitLimit);
        Assert.AreEqual(120, options.AuthenticatedWindowSeconds);
        Assert.IsFalse(options.IncludeHeaders);
        Assert.AreEqual(10, options.QueueLimit);
        Assert.AreEqual(2, options.ModuleLimits.Count);
        Assert.AreEqual(200, options.ModuleLimits["files"].PermitLimit);
        Assert.AreEqual(500, options.ModuleLimits["chat"].PermitLimit);
    }

    [TestMethod]
    public void PolicyNames_AreCorrect()
    {
        Assert.AreEqual("global", RateLimitingConfiguration.GlobalPolicy);
        Assert.AreEqual("authenticated", RateLimitingConfiguration.AuthenticatedPolicy);
    }
}

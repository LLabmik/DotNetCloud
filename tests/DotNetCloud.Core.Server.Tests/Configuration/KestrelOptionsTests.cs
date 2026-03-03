using DotNetCloud.Core.Server.Configuration;

namespace DotNetCloud.Core.Server.Tests.Configuration;

[TestClass]
public class KestrelOptionsTests
{
    [TestMethod]
    public void DefaultOptions_HasCorrectDefaults()
    {
        var options = new KestrelOptions();

        Assert.AreEqual(5080, options.HttpPort);
        Assert.AreEqual(5443, options.HttpsPort);
        Assert.IsTrue(options.EnableHttps);
        Assert.IsTrue(options.EnableHttp2);
        Assert.AreEqual(50 * 1024 * 1024, options.MaxRequestBodySize);
        Assert.AreEqual(30, options.RequestHeaderTimeoutSeconds);
        Assert.AreEqual(120, options.KeepAliveTimeoutSeconds);
        Assert.IsNull(options.MaxConcurrentConnections);
        Assert.AreEqual(0, options.ListenAddresses.Length);
        Assert.IsNull(options.CertificatePath);
        Assert.IsNull(options.CertificatePassword);
    }

    [TestMethod]
    public void Options_CanBeConfigured()
    {
        var options = new KestrelOptions
        {
            HttpPort = 8080,
            HttpsPort = 8443,
            EnableHttps = false,
            EnableHttp2 = false,
            MaxRequestBodySize = 100 * 1024 * 1024,
            RequestHeaderTimeoutSeconds = 60,
            KeepAliveTimeoutSeconds = 300,
            MaxConcurrentConnections = 1000,
            ListenAddresses = ["127.0.0.1", "0.0.0.0"],
            CertificatePath = "/path/to/cert.pfx",
            CertificatePassword = "password"
        };

        Assert.AreEqual(8080, options.HttpPort);
        Assert.AreEqual(8443, options.HttpsPort);
        Assert.IsFalse(options.EnableHttps);
        Assert.IsFalse(options.EnableHttp2);
        Assert.AreEqual(100 * 1024 * 1024, options.MaxRequestBodySize);
        Assert.AreEqual(60, options.RequestHeaderTimeoutSeconds);
        Assert.AreEqual(300, options.KeepAliveTimeoutSeconds);
        Assert.AreEqual(1000L, options.MaxConcurrentConnections);
        Assert.AreEqual(2, options.ListenAddresses.Length);
        Assert.AreEqual("/path/to/cert.pfx", options.CertificatePath);
        Assert.AreEqual("password", options.CertificatePassword);
    }

    [TestMethod]
    public void SectionName_IsKestrel()
    {
        Assert.AreEqual("Kestrel", KestrelOptions.SectionName);
    }
}

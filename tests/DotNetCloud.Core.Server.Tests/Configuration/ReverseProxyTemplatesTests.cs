using DotNetCloud.Core.Server.Configuration;

namespace DotNetCloud.Core.Server.Tests.Configuration;

[TestClass]
public class ReverseProxyTemplatesTests
{
    [TestMethod]
    public void GenerateNginxConfig_DefaultParams_ContainsProxyPass()
    {
        var config = ReverseProxyTemplates.GenerateNginxConfig();

        Assert.IsTrue(config.Contains("proxy_pass"));
        Assert.IsTrue(config.Contains("dotnetcloud.example.com"));
        Assert.IsTrue(config.Contains("5080"));
    }

    [TestMethod]
    public void GenerateNginxConfig_WithSsl_ContainsSslDirectives()
    {
        var config = ReverseProxyTemplates.GenerateNginxConfig(enableSsl: true);

        Assert.IsTrue(config.Contains("ssl_certificate"));
        Assert.IsTrue(config.Contains("ssl_protocols"));
        Assert.IsTrue(config.Contains("443"));
    }

    [TestMethod]
    public void GenerateNginxConfig_WithoutSsl_OmitsSslDirectives()
    {
        var config = ReverseProxyTemplates.GenerateNginxConfig(enableSsl: false);

        Assert.IsFalse(config.Contains("ssl_certificate"));
    }

    [TestMethod]
    public void GenerateNginxConfig_CustomServer_ContainsServerName()
    {
        var config = ReverseProxyTemplates.GenerateNginxConfig(serverName: "mycloud.local", upstreamPort: 8080);

        Assert.IsTrue(config.Contains("mycloud.local"));
        Assert.IsTrue(config.Contains("8080"));
    }

    [TestMethod]
    public void GenerateNginxConfig_IncludesWebSocketSupport()
    {
        var config = ReverseProxyTemplates.GenerateNginxConfig();

        Assert.IsTrue(config.Contains("/hubs/"));
        Assert.IsTrue(config.Contains("upgrade"));
    }

    [TestMethod]
    public void GenerateApacheConfig_DefaultParams_ContainsProxyPass()
    {
        var config = ReverseProxyTemplates.GenerateApacheConfig();

        Assert.IsTrue(config.Contains("ProxyPass"));
        Assert.IsTrue(config.Contains("ProxyPassReverse"));
        Assert.IsTrue(config.Contains("dotnetcloud.example.com"));
    }

    [TestMethod]
    public void GenerateApacheConfig_WithSsl_ContainsSslDirectives()
    {
        var config = ReverseProxyTemplates.GenerateApacheConfig(enableSsl: true);

        Assert.IsTrue(config.Contains("SSLEngine"));
        Assert.IsTrue(config.Contains("SSLCertificateFile"));
    }

    [TestMethod]
    public void GenerateApacheConfig_IncludesWebSocketSupport()
    {
        var config = ReverseProxyTemplates.GenerateApacheConfig();

        Assert.IsTrue(config.Contains("ws://"));
        Assert.IsTrue(config.Contains("websocket"));
    }

    [TestMethod]
    public void GenerateIisWebConfig_DefaultParams_ContainsAncm()
    {
        var config = ReverseProxyTemplates.GenerateIisWebConfig();

        Assert.IsTrue(config.Contains("AspNetCoreModuleV2"));
        Assert.IsTrue(config.Contains("aspNetCore"));
        Assert.IsTrue(config.Contains("InProcess"));
    }

    [TestMethod]
    public void GenerateIisWebConfig_OutOfProcess_ContainsOutOfProcess()
    {
        var config = ReverseProxyTemplates.GenerateIisWebConfig(hostingModel: "OutOfProcess");

        Assert.IsTrue(config.Contains("OutOfProcess"));
    }

    [TestMethod]
    public void GenerateIisWebConfig_CustomProcessPath_UsesCustomPath()
    {
        var config = ReverseProxyTemplates.GenerateIisWebConfig(processPath: @"C:\dotnetcloud\dotnet.exe");

        Assert.IsTrue(config.Contains(@"C:\dotnetcloud\dotnet.exe"));
    }

    [TestMethod]
    public void ValidateConfiguration_ValidNginxConfig_ReturnsIsValid()
    {
        var config = ReverseProxyTemplates.GenerateNginxConfig();

        var result = ReverseProxyTemplates.ValidateConfiguration("nginx", config);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public void ValidateConfiguration_EmptyConfig_ReturnsInvalid()
    {
        var result = ReverseProxyTemplates.ValidateConfiguration("nginx", "");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Count > 0);
    }

    [TestMethod]
    public void ValidateConfiguration_NginxMissingProxyPass_ReturnsError()
    {
        var result = ReverseProxyTemplates.ValidateConfiguration("nginx", "server { listen 80; }");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("proxy_pass")));
    }

    [TestMethod]
    public void ValidateConfiguration_ValidApacheConfig_ReturnsIsValid()
    {
        var config = ReverseProxyTemplates.GenerateApacheConfig();

        var result = ReverseProxyTemplates.ValidateConfiguration("apache", config);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void ValidateConfiguration_ValidIisConfig_ReturnsIsValid()
    {
        var config = ReverseProxyTemplates.GenerateIisWebConfig();

        var result = ReverseProxyTemplates.ValidateConfiguration("iis", config);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void ValidateConfiguration_UnknownProxyType_ReturnsError()
    {
        var result = ReverseProxyTemplates.ValidateConfiguration("caddy", "some config");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Unknown proxy type")));
    }

    [TestMethod]
    public void ValidateConfiguration_IisMissingAncm_ReturnsError()
    {
        var result = ReverseProxyTemplates.ValidateConfiguration("iis", "<configuration></configuration>");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("aspNetCore")));
    }
}

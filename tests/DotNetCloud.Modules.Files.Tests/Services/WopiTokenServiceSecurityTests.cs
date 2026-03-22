using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Security regression tests for WopiTokenService covering:
///   - Production environment MUST throw when no signing key is configured
///   - Production environment MUST throw when signing key is too short
///   - Non-production environment may use ephemeral key (backwards compat)
///   - Valid 32+ char key works in all environments
/// </summary>
[TestClass]
public class WopiTokenServiceSecurityTests
{
    [TestMethod]
    public void DeriveSigningKey_Production_NoKey_ThrowsInvalidOperationException()
    {
        // In production, using an ephemeral signing key is a security vulnerability:
        // tokens become invalid on restart, and can't be verified across instances.
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CreateTokenServiceWithEnvironment("Production", signingKey: ""));
    }

    [TestMethod]
    public void DeriveSigningKey_Production_ShortKey_ThrowsInvalidOperationException()
    {
        // Keys shorter than 32 chars provide insufficient entropy for HMAC-SHA256
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CreateTokenServiceWithEnvironment("Production", signingKey: "too-short"));
    }

    [TestMethod]
    public void DeriveSigningKey_Production_NullKey_ThrowsInvalidOperationException()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CreateTokenServiceWithEnvironment("Production", signingKey: null));
    }

    [TestMethod]
    public void DeriveSigningKey_Production_31CharKey_ThrowsInvalidOperationException()
    {
        // Exactly 31 chars — one less than the minimum requirement
        var key = new string('a', 31);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CreateTokenServiceWithEnvironment("Production", signingKey: key));
    }

    [TestMethod]
    public void DeriveSigningKey_Production_32CharKey_Succeeds()
    {
        var key = new string('a', 32);
        // Must NOT throw — 32 chars meets the minimum requirement
        var service = CreateTokenServiceWithEnvironment("Production", signingKey: key);
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void DeriveSigningKey_Production_LongKey_Succeeds()
    {
        var key = "this-is-a-sufficiently-long-signing-key-for-hmac-sha256-in-production";
        var service = CreateTokenServiceWithEnvironment("Production", signingKey: key);
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void DeriveSigningKey_Development_ShortKey_UsesEphemeralKey()
    {
        // In development, a short/missing key should fall back to ephemeral
        var service = CreateTokenServiceWithEnvironment("Development", signingKey: "short");
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void DeriveSigningKey_Development_NoKey_UsesEphemeralKey()
    {
        var service = CreateTokenServiceWithEnvironment("Development", signingKey: "");
        Assert.IsNotNull(service);
    }

    private static WopiTokenService CreateTokenServiceWithEnvironment(
        string environmentName, string? signingKey)
    {
        var db = new FilesDbContext(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var options = Microsoft.Extensions.Options.Options.Create(new Files.Options.CollaboraOptions
        {
            TokenLifetimeMinutes = 480,
            TokenSigningKey = signingKey ?? "",
            WopiBaseUrl = "https://cloud.example.com",
            Enabled = true,
            ServerUrl = "https://collabora.example.com"
        });

        var discovery = new Mock<ICollaboraDiscoveryService>();
        discovery.Setup(d => d.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        discovery.Setup(d => d.IsSupportedExtensionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        discovery.Setup(d => d.GetEditorUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://collabora.example.com/browser/dist/cool.html");

        var hostEnv = new TestHostEnvironment(environmentName);

        return new WopiTokenService(
            db,
            new PermissionService(db),
            discovery.Object,
            options,
            NullLogger<WopiTokenService>.Instance,
            hostEnv);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) => EnvironmentName = environmentName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

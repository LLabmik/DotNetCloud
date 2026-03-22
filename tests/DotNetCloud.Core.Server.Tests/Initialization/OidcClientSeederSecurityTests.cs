using DotNetCloud.Core.Server.Initialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DotNetCloud.Core.Server.Tests.Initialization;

/// <summary>
/// Security regression tests for OidcClientSeeder covering:
///   - Default redirect URIs are correct and safe
///   - Configuration overrides work for redirect URIs
///   - PKCE is required for all client registrations
///   - Clients are public (no client secrets)
/// </summary>
[TestClass]
public class OidcClientSeederSecurityTests
{
    [TestMethod]
    public async Task SeedAsync_DefaultConfig_RegistersDesktopClientWithCorrectRedirectUri()
    {
        var (manager, descriptors) = SetupMockManager();
        var config = BuildConfiguration(new Dictionary<string, string?>());
        var seeder = new OidcClientSeeder(manager.Object, config, NullLogger<OidcClientSeeder>.Instance);

        await seeder.SeedAsync();

        var desktop = descriptors.FirstOrDefault(d => d.ClientId == "dotnetcloud-desktop");
        Assert.IsNotNull(desktop, "Desktop client must be seeded");
        Assert.IsTrue(desktop.RedirectUris.Contains(new Uri("http://localhost:52701/oauth/callback")),
            "Default desktop redirect URI must be localhost callback");
    }

    [TestMethod]
    public async Task SeedAsync_DefaultConfig_RegistersMobileClientWithCorrectRedirectUri()
    {
        var (manager, descriptors) = SetupMockManager();
        var config = BuildConfiguration(new Dictionary<string, string?>());
        var seeder = new OidcClientSeeder(manager.Object, config, NullLogger<OidcClientSeeder>.Instance);

        await seeder.SeedAsync();

        var mobile = descriptors.FirstOrDefault(d => d.ClientId == "dotnetcloud-mobile");
        Assert.IsNotNull(mobile, "Mobile client must be seeded");
        Assert.IsTrue(mobile.RedirectUris.Contains(new Uri("net.dotnetcloud.client://oauth2redirect")),
            "Default mobile redirect URI must use custom scheme");
    }

    [TestMethod]
    public async Task SeedAsync_ConfigOverride_UsesCustomDesktopRedirectUri()
    {
        var (manager, descriptors) = SetupMockManager();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OidcClients:Desktop:RedirectUri"] = "http://localhost:9999/custom-callback",
        });
        var seeder = new OidcClientSeeder(manager.Object, config, NullLogger<OidcClientSeeder>.Instance);

        await seeder.SeedAsync();

        var desktop = descriptors.FirstOrDefault(d => d.ClientId == "dotnetcloud-desktop");
        Assert.IsNotNull(desktop);
        Assert.IsTrue(desktop.RedirectUris.Contains(new Uri("http://localhost:9999/custom-callback")),
            "Configuration override must change the desktop redirect URI");
        Assert.IsFalse(desktop.RedirectUris.Contains(new Uri("http://localhost:52701/oauth/callback")),
            "Default URI must not be present when override is configured");
    }

    [TestMethod]
    public async Task SeedAsync_ConfigOverride_UsesCustomMobileRedirectUri()
    {
        var (manager, descriptors) = SetupMockManager();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OidcClients:Mobile:RedirectUri"] = "net.custom.app://callback",
        });
        var seeder = new OidcClientSeeder(manager.Object, config, NullLogger<OidcClientSeeder>.Instance);

        await seeder.SeedAsync();

        var mobile = descriptors.FirstOrDefault(d => d.ClientId == "dotnetcloud-mobile");
        Assert.IsNotNull(mobile);
        Assert.IsTrue(mobile.RedirectUris.Contains(new Uri("net.custom.app://callback")));
    }

    [TestMethod]
    public async Task SeedAsync_AllClients_RequirePkce()
    {
        var (manager, descriptors) = SetupMockManager();
        var config = BuildConfiguration(new Dictionary<string, string?>());
        var seeder = new OidcClientSeeder(manager.Object, config, NullLogger<OidcClientSeeder>.Instance);

        await seeder.SeedAsync();

        foreach (var descriptor in descriptors)
        {
            Assert.IsTrue(
                descriptor.Requirements.Contains(Requirements.Features.ProofKeyForCodeExchange),
                $"Client '{descriptor.ClientId}' MUST require PKCE to prevent auth code interception attacks");
        }
    }

    [TestMethod]
    public async Task SeedAsync_AllClients_ArePublicType()
    {
        var (manager, descriptors) = SetupMockManager();
        var config = BuildConfiguration(new Dictionary<string, string?>());
        var seeder = new OidcClientSeeder(manager.Object, config, NullLogger<OidcClientSeeder>.Instance);

        await seeder.SeedAsync();

        foreach (var descriptor in descriptors)
        {
            Assert.AreEqual(ClientTypes.Public, descriptor.ClientType,
                $"Client '{descriptor.ClientId}' must be public (no client secret for native apps)");
        }
    }

    [TestMethod]
    public async Task SeedAsync_AllClients_UseAuthorizationCodeGrantOnly()
    {
        var (manager, descriptors) = SetupMockManager();
        var config = BuildConfiguration(new Dictionary<string, string?>());
        var seeder = new OidcClientSeeder(manager.Object, config, NullLogger<OidcClientSeeder>.Instance);

        await seeder.SeedAsync();

        foreach (var descriptor in descriptors)
        {
            Assert.IsTrue(
                descriptor.Permissions.Contains(Permissions.GrantTypes.AuthorizationCode),
                $"Client '{descriptor.ClientId}' must support authorization code grant");

            // Implicit grant is insecure — must NOT be present
            Assert.IsFalse(
                descriptor.Permissions.Contains(Permissions.GrantTypes.Implicit),
                $"Client '{descriptor.ClientId}' must NOT support implicit grant (insecure)");

            // Client credentials is for server-to-server, not public clients
            Assert.IsFalse(
                descriptor.Permissions.Contains(Permissions.GrantTypes.ClientCredentials),
                $"Client '{descriptor.ClientId}' must NOT support client credentials grant");
        }
    }

    [TestMethod]
    public async Task SeedAsync_ExistingClient_UpdatesInsteadOfDuplicate()
    {
        var manager = new Mock<IOpenIddictApplicationManager>();
        var descriptors = new List<OpenIddictApplicationDescriptor>();

        // Simulate existing client
        manager.Setup(m => m.FindByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object()); // non-null = client exists

        manager.Setup(m => m.UpdateAsync(It.IsAny<object>(), It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<object, OpenIddictApplicationDescriptor, CancellationToken>((_, d, _) => descriptors.Add(d))
            .Returns(ValueTask.CompletedTask);

        var config = BuildConfiguration(new Dictionary<string, string?>());
        var seeder = new OidcClientSeeder(manager.Object, config, NullLogger<OidcClientSeeder>.Instance);

        await seeder.SeedAsync();

        // CreateAsync should never be called for existing clients
        manager.Verify(m => m.CreateAsync(It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()), Times.Never);
        // UpdateAsync should be called instead
        manager.Verify(m => m.UpdateAsync(It.IsAny<object>(), It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ──── Helpers ─────────────────────────────────────────────────────────────

    private static (Mock<IOpenIddictApplicationManager> Manager, List<OpenIddictApplicationDescriptor> Descriptors) SetupMockManager()
    {
        var manager = new Mock<IOpenIddictApplicationManager>();
        var descriptors = new List<OpenIddictApplicationDescriptor>();

        // No existing clients
        manager.Setup(m => m.FindByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        manager.Setup(m => m.CreateAsync(It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<OpenIddictApplicationDescriptor, CancellationToken>((d, _) => descriptors.Add(d))
            .Returns(new ValueTask<object>(new object()));

        return (manager, descriptors);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}

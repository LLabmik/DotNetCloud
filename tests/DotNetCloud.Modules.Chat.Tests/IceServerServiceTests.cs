using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="IceServerService"/>.
/// </summary>
[TestClass]
public sealed class IceServerServiceTests
{
    private static IceServerOptions DefaultOptions() => new();

    private static IceServerService CreateService(IceServerOptions options, TimeProvider? timeProvider = null)
    {
        return new IceServerService(
            Options.Create(options),
            NullLogger<IceServerService>.Instance,
            timeProvider);
    }

    // ── Built-in STUN ────────────────────────────────────────────

    [TestMethod]
    public void GetIceServers_BuiltInStunEnabled_ReturnsStunServer()
    {
        var service = CreateService(DefaultOptions());
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual(1, servers.Count);
        Assert.AreEqual("stun:myhost.example.com:3478", servers[0].Urls[0]);
        Assert.IsNull(servers[0].Username);
        Assert.IsNull(servers[0].Credential);
    }

    [TestMethod]
    public void GetIceServers_BuiltInStunEnabled_CustomPort()
    {
        var options = new IceServerOptions { StunPort = 19302 };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual("stun:myhost.example.com:19302", servers[0].Urls[0]);
    }

    [TestMethod]
    public void GetIceServers_BuiltInStunEnabled_UsesStunPublicHostWhenPublicHostNull()
    {
        var options = new IceServerOptions { StunPublicHost = "stun.mydomain.com" };
        var service = CreateService(options);
        var servers = service.GetIceServers(null);

        Assert.AreEqual("stun:stun.mydomain.com:3478", servers[0].Urls[0]);
    }

    [TestMethod]
    public void GetIceServers_BuiltInStunEnabled_FallsBackToLocalhost()
    {
        var options = new IceServerOptions { StunPublicHost = "" };
        var service = CreateService(options);
        var servers = service.GetIceServers(null);

        Assert.AreEqual("stun:localhost:3478", servers[0].Urls[0]);
    }

    [TestMethod]
    public void GetIceServers_BuiltInStunEnabled_PublicHostOverridesStunPublicHost()
    {
        var options = new IceServerOptions { StunPublicHost = "config-host.com" };
        var service = CreateService(options);
        var servers = service.GetIceServers("request-host.com");

        Assert.AreEqual("stun:request-host.com:3478", servers[0].Urls[0]);
    }

    [TestMethod]
    public void GetIceServers_BuiltInStunDisabled_NoStunServer()
    {
        var options = new IceServerOptions { EnableBuiltInStun = false };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual(0, servers.Count);
    }

    // ── Additional STUN ──────────────────────────────────────────

    [TestMethod]
    public void GetIceServers_AdditionalStunUrls_Appended()
    {
        var options = new IceServerOptions
        {
            AdditionalStunUrls = ["stun:stun.l.google.com:19302", "stun:stun.cloudflare.com:3478"]
        };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual(3, servers.Count); // built-in + 2 additional
        Assert.AreEqual("stun:stun.l.google.com:19302", servers[1].Urls[0]);
        Assert.AreEqual("stun:stun.cloudflare.com:3478", servers[2].Urls[0]);
    }

    [TestMethod]
    public void GetIceServers_AdditionalStunUrls_SkipsEmptyEntries()
    {
        var options = new IceServerOptions
        {
            AdditionalStunUrls = ["stun:stun.l.google.com:19302", "", "  "]
        };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual(2, servers.Count); // built-in + 1 valid additional
    }

    [TestMethod]
    public void GetIceServers_BuiltInStunDisabled_OnlyAdditionalStun()
    {
        var options = new IceServerOptions
        {
            EnableBuiltInStun = false,
            AdditionalStunUrls = ["stun:stun.l.google.com:19302"]
        };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual(1, servers.Count);
        Assert.AreEqual("stun:stun.l.google.com:19302", servers[0].Urls[0]);
    }

    // ── TURN with static credentials ─────────────────────────────

    [TestMethod]
    public void GetIceServers_TurnEnabledStaticCredentials_ReturnsTurnServer()
    {
        var options = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478"],
            TurnUsername = "myuser",
            TurnCredential = "mypass"
        };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual(2, servers.Count); // built-in STUN + TURN
        var turnServer = servers[1];
        Assert.AreEqual("turn:turn.example.com:3478", turnServer.Urls[0]);
        Assert.AreEqual("myuser", turnServer.Username);
        Assert.AreEqual("mypass", turnServer.Credential);
    }

    [TestMethod]
    public void GetIceServers_TurnEnabledMultipleUrls_AllIncluded()
    {
        var options = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478", "turns:turn.example.com:5349"],
            TurnUsername = "user",
            TurnCredential = "pass"
        };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        var turnServer = servers[1];
        Assert.AreEqual(2, turnServer.Urls.Length);
        Assert.AreEqual("turn:turn.example.com:3478", turnServer.Urls[0]);
        Assert.AreEqual("turns:turn.example.com:5349", turnServer.Urls[1]);
    }

    [TestMethod]
    public void GetIceServers_TurnEnabledNoCredentials_NoTurnReturned()
    {
        var options = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478"],
            TurnUsername = "",
            TurnCredential = ""
        };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual(1, servers.Count); // only built-in STUN, no TURN
    }

    [TestMethod]
    public void GetIceServers_TurnEnabledEmptyUrls_NoTurnReturned()
    {
        var options = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = [],
            TurnUsername = "user",
            TurnCredential = "pass"
        };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual(1, servers.Count); // only built-in STUN
    }

    [TestMethod]
    public void GetIceServers_TurnDisabled_NoTurnReturned()
    {
        var options = new IceServerOptions
        {
            EnableTurn = false,
            TurnUrls = ["turn:turn.example.com:3478"],
            TurnUsername = "user",
            TurnCredential = "pass"
        };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual(1, servers.Count); // only built-in STUN
    }

    [TestMethod]
    public void GetIceServers_TurnUrlsWithBlanks_OnlyValidUrlsIncluded()
    {
        var options = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478", "", "  ", "turns:turn.example.com:5349"],
            TurnUsername = "user",
            TurnCredential = "pass"
        };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        var turnServer = servers[1];
        Assert.AreEqual(2, turnServer.Urls.Length);
    }

    // ── TURN with ephemeral credentials (coturn) ─────────────────

    [TestMethod]
    public void GetIceServers_EphemeralCredentials_ReturnsCredentials()
    {
        var fixedTime = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fixedTime);

        var options = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478"],
            EnableEphemeralCredentials = true,
            TurnSharedSecret = "mysecret",
            CredentialTtlSeconds = 86400
        };
        var service = CreateService(options, timeProvider);
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual(2, servers.Count);
        var turnServer = servers[1];
        Assert.IsNotNull(turnServer.Username);
        Assert.IsNotNull(turnServer.Credential);
        Assert.IsTrue(turnServer.Username!.Contains(":")); // format: "timestamp:randomId"
    }

    [TestMethod]
    public void GetIceServers_EphemeralCredentials_UsernameContainsExpiryTimestamp()
    {
        var fixedTime = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fixedTime);
        var expectedExpiry = fixedTime.AddSeconds(86400).ToUnixTimeSeconds();

        var options = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478"],
            EnableEphemeralCredentials = true,
            TurnSharedSecret = "mysecret",
            CredentialTtlSeconds = 86400
        };
        var service = CreateService(options, timeProvider);
        var servers = service.GetIceServers("myhost.example.com");

        var username = servers[1].Username!;
        var parts = username.Split(':');
        Assert.AreEqual(2, parts.Length);
        Assert.AreEqual(expectedExpiry.ToString(), parts[0]);
    }

    [TestMethod]
    public void GetIceServers_EphemeralCredentials_CredentialIsHmacSha1()
    {
        var fixedTime = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fixedTime);

        var options = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478"],
            EnableEphemeralCredentials = true,
            TurnSharedSecret = "mysecret",
            CredentialTtlSeconds = 86400
        };
        var service = CreateService(options, timeProvider);
        var servers = service.GetIceServers("myhost.example.com");

        var username = servers[1].Username!;
        var credential = servers[1].Credential!;

        // Verify it matches HMAC-SHA1
        var expectedCredential = IceServerService.ComputeHmacSha1("mysecret", username);
        Assert.AreEqual(expectedCredential, credential);
    }

    [TestMethod]
    public void GetIceServers_EphemeralCredentials_DifferentSecretsProduceDifferentCredentials()
    {
        var fixedTime = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);

        var options1 = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478"],
            EnableEphemeralCredentials = true,
            TurnSharedSecret = "secret1",
            CredentialTtlSeconds = 86400
        };
        var options2 = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478"],
            EnableEphemeralCredentials = true,
            TurnSharedSecret = "secret2",
            CredentialTtlSeconds = 86400
        };

        var service1 = CreateService(options1, new FakeTimeProvider(fixedTime));
        var service2 = CreateService(options2, new FakeTimeProvider(fixedTime));

        var cred1 = service1.GetIceServers("host")[1].Credential;
        var cred2 = service2.GetIceServers("host")[1].Credential;

        Assert.AreNotEqual(cred1, cred2);
    }

    [TestMethod]
    public void GetIceServers_EphemeralCredentials_CustomTtl()
    {
        var fixedTime = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fixedTime);
        var expectedExpiry = fixedTime.AddSeconds(3600).ToUnixTimeSeconds();

        var options = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478"],
            EnableEphemeralCredentials = true,
            TurnSharedSecret = "mysecret",
            CredentialTtlSeconds = 3600
        };
        var service = CreateService(options, timeProvider);
        var servers = service.GetIceServers("host");

        var username = servers[1].Username!;
        Assert.IsTrue(username.StartsWith(expectedExpiry.ToString()));
    }

    [TestMethod]
    public void GetIceServers_EphemeralCredentials_TakePriorityOverStaticCredentials()
    {
        var fixedTime = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fixedTime);

        var options = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478"],
            TurnUsername = "static-user",
            TurnCredential = "static-pass",
            EnableEphemeralCredentials = true,
            TurnSharedSecret = "mysecret"
        };
        var service = CreateService(options, timeProvider);
        var servers = service.GetIceServers("host");

        // Ephemeral credentials should be used, not static
        Assert.AreNotEqual("static-user", servers[1].Username);
        Assert.AreNotEqual("static-pass", servers[1].Credential);
    }

    [TestMethod]
    public void GetIceServers_EphemeralCredentialsNoSecret_NoTurnReturned()
    {
        var options = new IceServerOptions
        {
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478"],
            EnableEphemeralCredentials = true,
            TurnSharedSecret = "" // empty secret
        };
        var service = CreateService(options);
        var servers = service.GetIceServers("host");

        Assert.AreEqual(1, servers.Count); // only built-in STUN
    }

    // ── IceTransportPolicy ───────────────────────────────────────

    [TestMethod]
    public void IceTransportPolicy_ReturnsConfiguredValue()
    {
        var options = new IceServerOptions { IceTransportPolicy = "relay" };
        var service = CreateService(options);
        Assert.AreEqual("relay", service.IceTransportPolicy);
    }

    [TestMethod]
    public void IceTransportPolicy_DefaultsToAll()
    {
        var service = CreateService(DefaultOptions());
        Assert.AreEqual("all", service.IceTransportPolicy);
    }

    // ── ComputeHmacSha1 ─────────────────────────────────────────

    [TestMethod]
    public void ComputeHmacSha1_ProducesBase64Output()
    {
        var result = IceServerService.ComputeHmacSha1("secret", "message");
        Assert.IsNotNull(result);

        // Should be valid Base64
        var bytes = Convert.FromBase64String(result);
        Assert.AreEqual(20, bytes.Length); // SHA1 = 20 bytes
    }

    [TestMethod]
    public void ComputeHmacSha1_SameInputSameOutput()
    {
        var result1 = IceServerService.ComputeHmacSha1("secret", "message");
        var result2 = IceServerService.ComputeHmacSha1("secret", "message");
        Assert.AreEqual(result1, result2);
    }

    [TestMethod]
    public void ComputeHmacSha1_DifferentSecretsDifferentOutput()
    {
        var result1 = IceServerService.ComputeHmacSha1("secret1", "message");
        var result2 = IceServerService.ComputeHmacSha1("secret2", "message");
        Assert.AreNotEqual(result1, result2);
    }

    [TestMethod]
    public void ComputeHmacSha1_DifferentMessagesDifferentOutput()
    {
        var result1 = IceServerService.ComputeHmacSha1("secret", "message1");
        var result2 = IceServerService.ComputeHmacSha1("secret", "message2");
        Assert.AreNotEqual(result1, result2);
    }

    // ── Full integration scenario ────────────────────────────────

    [TestMethod]
    public void GetIceServers_FullSetup_StunAndAdditionalAndTurn()
    {
        var fixedTime = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fixedTime);

        var options = new IceServerOptions
        {
            EnableBuiltInStun = true,
            StunPort = 3478,
            AdditionalStunUrls = ["stun:stun.cloudflare.com:3478"],
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478", "turns:turn.example.com:5349"],
            EnableEphemeralCredentials = true,
            TurnSharedSecret = "coturn-secret",
            CredentialTtlSeconds = 43200
        };
        var service = CreateService(options, timeProvider);
        var servers = service.GetIceServers("cloud.example.com");

        Assert.AreEqual(3, servers.Count);

        // Built-in STUN
        Assert.AreEqual("stun:cloud.example.com:3478", servers[0].Urls[0]);
        Assert.IsNull(servers[0].Username);

        // Additional STUN
        Assert.AreEqual("stun:stun.cloudflare.com:3478", servers[1].Urls[0]);
        Assert.IsNull(servers[1].Username);

        // TURN with ephemeral creds
        Assert.AreEqual(2, servers[2].Urls.Length);
        Assert.IsNotNull(servers[2].Username);
        Assert.IsNotNull(servers[2].Credential);
    }

    [TestMethod]
    public void GetIceServers_MinimalSetup_OnlyBuiltInStun()
    {
        var service = CreateService(DefaultOptions());
        var servers = service.GetIceServers("myserver.com");

        Assert.AreEqual(1, servers.Count);
        Assert.AreEqual("stun:myserver.com:3478", servers[0].Urls[0]);
    }

    [TestMethod]
    public void GetIceServers_EverythingDisabled_EmptyList()
    {
        var options = new IceServerOptions
        {
            EnableBuiltInStun = false,
            EnableTurn = false
        };
        var service = CreateService(options);
        var servers = service.GetIceServers("myhost.example.com");

        Assert.AreEqual(0, servers.Count);
    }

    /// <summary>
    /// Fake TimeProvider for deterministic testing.
    /// </summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}

using DotNetCloud.Modules.Chat.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="IceServerOptions"/> configuration class.
/// </summary>
[TestClass]
public sealed class IceServerOptionsTests
{
    [TestMethod]
    public void SectionName_IsCorrect()
    {
        Assert.AreEqual("Chat:IceServers", IceServerOptions.SectionName);
    }

    [TestMethod]
    public void Defaults_EnableBuiltInStun_True()
    {
        var options = new IceServerOptions();
        Assert.IsTrue(options.EnableBuiltInStun);
    }

    [TestMethod]
    public void Defaults_StunPort_3478()
    {
        var options = new IceServerOptions();
        Assert.AreEqual(3478, options.StunPort);
    }

    [TestMethod]
    public void Defaults_StunPublicHost_Empty()
    {
        var options = new IceServerOptions();
        Assert.AreEqual(string.Empty, options.StunPublicHost);
    }

    [TestMethod]
    public void Defaults_AdditionalStunUrls_Empty()
    {
        var options = new IceServerOptions();
        Assert.AreEqual(0, options.AdditionalStunUrls.Length);
    }

    [TestMethod]
    public void Defaults_EnableTurn_False()
    {
        var options = new IceServerOptions();
        Assert.IsFalse(options.EnableTurn);
    }

    [TestMethod]
    public void Defaults_TurnUrls_Empty()
    {
        var options = new IceServerOptions();
        Assert.AreEqual(0, options.TurnUrls.Length);
    }

    [TestMethod]
    public void Defaults_TurnUsername_Empty()
    {
        var options = new IceServerOptions();
        Assert.AreEqual(string.Empty, options.TurnUsername);
    }

    [TestMethod]
    public void Defaults_TurnCredential_Empty()
    {
        var options = new IceServerOptions();
        Assert.AreEqual(string.Empty, options.TurnCredential);
    }

    [TestMethod]
    public void Defaults_EnableEphemeralCredentials_False()
    {
        var options = new IceServerOptions();
        Assert.IsFalse(options.EnableEphemeralCredentials);
    }

    [TestMethod]
    public void Defaults_TurnSharedSecret_Empty()
    {
        var options = new IceServerOptions();
        Assert.AreEqual(string.Empty, options.TurnSharedSecret);
    }

    [TestMethod]
    public void Defaults_CredentialTtlSeconds_86400()
    {
        var options = new IceServerOptions();
        Assert.AreEqual(86400, options.CredentialTtlSeconds);
    }

    [TestMethod]
    public void Defaults_IceTransportPolicy_All()
    {
        var options = new IceServerOptions();
        Assert.AreEqual("all", options.IceTransportPolicy);
    }

    [TestMethod]
    public void CustomValues_AllPropertiesSettable()
    {
        var options = new IceServerOptions
        {
            EnableBuiltInStun = false,
            StunPort = 19302,
            StunPublicHost = "stun.example.com",
            AdditionalStunUrls = ["stun:extra.example.com:3478"],
            EnableTurn = true,
            TurnUrls = ["turn:turn.example.com:3478"],
            TurnUsername = "user",
            TurnCredential = "pass",
            EnableEphemeralCredentials = true,
            TurnSharedSecret = "secret123",
            CredentialTtlSeconds = 3600,
            IceTransportPolicy = "relay"
        };

        Assert.IsFalse(options.EnableBuiltInStun);
        Assert.AreEqual(19302, options.StunPort);
        Assert.AreEqual("stun.example.com", options.StunPublicHost);
        Assert.AreEqual(1, options.AdditionalStunUrls.Length);
        Assert.IsTrue(options.EnableTurn);
        Assert.AreEqual(1, options.TurnUrls.Length);
        Assert.AreEqual("user", options.TurnUsername);
        Assert.AreEqual("pass", options.TurnCredential);
        Assert.IsTrue(options.EnableEphemeralCredentials);
        Assert.AreEqual("secret123", options.TurnSharedSecret);
        Assert.AreEqual(3600, options.CredentialTtlSeconds);
        Assert.AreEqual("relay", options.IceTransportPolicy);
    }
}

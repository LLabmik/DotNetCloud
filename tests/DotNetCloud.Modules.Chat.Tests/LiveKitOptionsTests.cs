using DotNetCloud.Modules.Chat.Services;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="LiveKitOptions"/> configuration validation and defaults.
/// </summary>
[TestClass]
public class LiveKitOptionsTests
{
    // ══════════════════════════════════════════════════════════════
    //  Default Values
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Defaults_EnabledIsFalse()
    {
        var options = new LiveKitOptions();
        Assert.IsFalse(options.Enabled);
    }

    [TestMethod]
    public void Defaults_ServerUrlIsEmpty()
    {
        var options = new LiveKitOptions();
        Assert.AreEqual(string.Empty, options.ServerUrl);
    }

    [TestMethod]
    public void Defaults_ApiKeyIsEmpty()
    {
        var options = new LiveKitOptions();
        Assert.AreEqual(string.Empty, options.ApiKey);
    }

    [TestMethod]
    public void Defaults_ApiSecretIsEmpty()
    {
        var options = new LiveKitOptions();
        Assert.AreEqual(string.Empty, options.ApiSecret);
    }

    [TestMethod]
    public void Defaults_DefaultMaxParticipantsIs50()
    {
        var options = new LiveKitOptions();
        Assert.AreEqual(50, options.DefaultMaxParticipants);
    }

    [TestMethod]
    public void Defaults_TokenTtlSecondsIs3600()
    {
        var options = new LiveKitOptions();
        Assert.AreEqual(3600, options.TokenTtlSeconds);
    }

    [TestMethod]
    public void Defaults_MaxP2PParticipantsIs3()
    {
        var options = new LiveKitOptions();
        Assert.AreEqual(3, options.MaxP2PParticipants);
    }

    [TestMethod]
    public void Defaults_EmptyRoomTimeoutSecondsIs300()
    {
        var options = new LiveKitOptions();
        Assert.AreEqual(300, options.EmptyRoomTimeoutSeconds);
    }

    [TestMethod]
    public void SectionName_IsChatLiveKit()
    {
        string sectionName = LiveKitOptions.SectionName;
        Assert.AreEqual("Chat:LiveKit", sectionName);
    }

    // ══════════════════════════════════════════════════════════════
    //  IsValid Tests — Disabled
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void IsValid_WhenDisabled_ReturnsTrue()
    {
        var options = new LiveKitOptions { Enabled = false };
        Assert.IsTrue(options.IsValid());
    }

    [TestMethod]
    public void IsValid_WhenDisabledWithEmptyFields_ReturnsTrue()
    {
        var options = new LiveKitOptions
        {
            Enabled = false,
            ServerUrl = "",
            ApiKey = "",
            ApiSecret = ""
        };
        Assert.IsTrue(options.IsValid());
    }

    // ══════════════════════════════════════════════════════════════
    //  IsValid Tests — Enabled
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void IsValid_WhenEnabledWithAllFieldsSet_ReturnsTrue()
    {
        var options = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = "https://livekit.example.com",
            ApiKey = "APIkey123",
            ApiSecret = "secret456"
        };
        Assert.IsTrue(options.IsValid());
    }

    [TestMethod]
    public void IsValid_WhenEnabledWithMissingServerUrl_ReturnsFalse()
    {
        var options = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = "",
            ApiKey = "APIkey123",
            ApiSecret = "secret456"
        };
        Assert.IsFalse(options.IsValid());
    }

    [TestMethod]
    public void IsValid_WhenEnabledWithMissingApiKey_ReturnsFalse()
    {
        var options = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = "https://livekit.example.com",
            ApiKey = "",
            ApiSecret = "secret456"
        };
        Assert.IsFalse(options.IsValid());
    }

    [TestMethod]
    public void IsValid_WhenEnabledWithMissingApiSecret_ReturnsFalse()
    {
        var options = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = "https://livekit.example.com",
            ApiKey = "APIkey123",
            ApiSecret = ""
        };
        Assert.IsFalse(options.IsValid());
    }

    [TestMethod]
    public void IsValid_WhenEnabledWithWhitespaceServerUrl_ReturnsFalse()
    {
        var options = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = "   ",
            ApiKey = "APIkey123",
            ApiSecret = "secret456"
        };
        Assert.IsFalse(options.IsValid());
    }

    [TestMethod]
    public void IsValid_WhenEnabledWithWhitespaceApiKey_ReturnsFalse()
    {
        var options = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = "https://livekit.example.com",
            ApiKey = "   ",
            ApiSecret = "secret456"
        };
        Assert.IsFalse(options.IsValid());
    }

    [TestMethod]
    public void IsValid_WhenEnabledWithWhitespaceApiSecret_ReturnsFalse()
    {
        var options = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = "https://livekit.example.com",
            ApiKey = "APIkey123",
            ApiSecret = "   "
        };
        Assert.IsFalse(options.IsValid());
    }

    [TestMethod]
    public void IsValid_WhenEnabledWithNullServerUrl_ReturnsFalse()
    {
        var options = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = null!,
            ApiKey = "APIkey123",
            ApiSecret = "secret456"
        };
        Assert.IsFalse(options.IsValid());
    }

    [TestMethod]
    public void IsValid_WhenEnabledWithAllFieldsMissing_ReturnsFalse()
    {
        var options = new LiveKitOptions { Enabled = true };
        Assert.IsFalse(options.IsValid());
    }

    // ══════════════════════════════════════════════════════════════
    //  Custom Values
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void CustomValues_AreRespected()
    {
        var options = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = "ws://localhost:7880",
            ApiKey = "mykey",
            ApiSecret = "mysecret",
            DefaultMaxParticipants = 100,
            TokenTtlSeconds = 7200,
            MaxP2PParticipants = 5,
            EmptyRoomTimeoutSeconds = 600
        };

        Assert.IsTrue(options.Enabled);
        Assert.AreEqual("ws://localhost:7880", options.ServerUrl);
        Assert.AreEqual("mykey", options.ApiKey);
        Assert.AreEqual("mysecret", options.ApiSecret);
        Assert.AreEqual(100, options.DefaultMaxParticipants);
        Assert.AreEqual(7200, options.TokenTtlSeconds);
        Assert.AreEqual(5, options.MaxP2PParticipants);
        Assert.AreEqual(600, options.EmptyRoomTimeoutSeconds);
    }
}

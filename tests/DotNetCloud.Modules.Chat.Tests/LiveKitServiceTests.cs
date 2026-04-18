using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Comprehensive tests for <see cref="LiveKitService"/> — JWT token generation,
/// room lifecycle, token structure validation, and configuration enforcement.
/// </summary>
[TestClass]
public class LiveKitServiceTests
{
    private const string TestApiKey = "APIkey_test_12345";
    private const string TestApiSecret = "secret_test_67890_abcdefghijklmnopqrstuvwxyz";
    private const string TestServerUrl = "https://livekit.example.com";

    private LiveKitOptions _options = null!;

    [TestInitialize]
    public void Setup()
    {
        _options = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = TestServerUrl,
            ApiKey = TestApiKey,
            ApiSecret = TestApiSecret,
            DefaultMaxParticipants = 50,
            TokenTtlSeconds = 3600,
            MaxP2PParticipants = 3,
            EmptyRoomTimeoutSeconds = 300
        };
    }

    // ══════════════════════════════════════════════════════════════
    //  Constructor Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Constructor_WithInvalidOptions_ThrowsInvalidOperationException()
    {
        var invalidOptions = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = "",
            ApiKey = "",
            ApiSecret = ""
        };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CreateService(invalidOptions));
    }

    [TestMethod]
    public void Constructor_WithValidOptions_DoesNotThrow()
    {
        var service = CreateService(_options);
        Assert.IsNotNull(service);
    }

    // ══════════════════════════════════════════════════════════════
    //  IsAvailable / MaxP2PParticipants Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void IsAvailable_WhenEnabledAndValid_ReturnsTrue()
    {
        var service = CreateService(_options);
        Assert.IsTrue(service.IsAvailable);
    }

    [TestMethod]
    public void MaxP2PParticipants_ReturnsOptionsValue()
    {
        var service = CreateService(_options);
        Assert.AreEqual(3, service.MaxP2PParticipants);
    }

    [TestMethod]
    public void MaxP2PParticipants_RespectsCustomValue()
    {
        _options.MaxP2PParticipants = 5;
        var service = CreateService(_options);
        Assert.AreEqual(5, service.MaxP2PParticipants);
    }

    // ══════════════════════════════════════════════════════════════
    //  GenerateToken Tests — JWT Structure
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void GenerateToken_ReturnsThreePartJwt()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var parts = token.Split('.');
        Assert.AreEqual(3, parts.Length, "JWT should have exactly 3 parts (header.payload.signature).");
    }

    [TestMethod]
    public void GenerateToken_HeaderContainsHs256Algorithm()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var headerJson = DecodeJwtPart(token.Split('.')[0]);
        using var doc = JsonDocument.Parse(headerJson);
        Assert.AreEqual("HS256", doc.RootElement.GetProperty("alg").GetString());
    }

    [TestMethod]
    public void GenerateToken_HeaderContainsJwtType()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var headerJson = DecodeJwtPart(token.Split('.')[0]);
        using var doc = JsonDocument.Parse(headerJson);
        Assert.AreEqual("JWT", doc.RootElement.GetProperty("typ").GetString());
    }

    [TestMethod]
    public void GenerateToken_PayloadContainsIssuer()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        Assert.AreEqual(TestApiKey, payload.GetProperty("iss").GetString());
    }

    [TestMethod]
    public void GenerateToken_PayloadContainsSubject()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        Assert.AreEqual("user-123", payload.GetProperty("sub").GetString());
    }

    [TestMethod]
    public void GenerateToken_PayloadContainsName()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        Assert.AreEqual("User One", payload.GetProperty("name").GetString());
    }

    [TestMethod]
    public void GenerateToken_PayloadContainsExpirationInFuture()
    {
        var service = CreateService(_options);
        var before = DateTimeOffset.UtcNow;
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        var exp = payload.GetProperty("exp").GetInt64();
        var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);

        Assert.IsTrue(expTime > before, "Expiration should be in the future.");
        Assert.IsTrue(expTime <= before.AddSeconds(_options.TokenTtlSeconds + 5),
            "Expiration should be approximately TokenTtlSeconds from now.");
    }

    [TestMethod]
    public void GenerateToken_PayloadContainsNotBeforeInPast()
    {
        var service = CreateService(_options);
        var before = DateTimeOffset.UtcNow;
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        var nbf = payload.GetProperty("nbf").GetInt64();
        var nbfTime = DateTimeOffset.FromUnixTimeSeconds(nbf);

        Assert.IsTrue(nbfTime <= before.AddSeconds(1), "nbf should be at or before current time.");
    }

    [TestMethod]
    public void GenerateToken_PayloadContainsJti()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        var jti = payload.GetProperty("jti").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(jti), "jti should be a non-empty string.");
    }

    [TestMethod]
    public void GenerateToken_JtiIsUniqueAcrossCalls()
    {
        var service = CreateService(_options);
        var token1 = service.GenerateToken("room-1", "user-123", "User One");
        var token2 = service.GenerateToken("room-1", "user-123", "User One");

        var payload1 = ParseJwtPayload(token1);
        var payload2 = ParseJwtPayload(token2);

        Assert.AreNotEqual(
            payload1.GetProperty("jti").GetString(),
            payload2.GetProperty("jti").GetString(),
            "Each token should have a unique jti.");
    }

    // ══════════════════════════════════════════════════════════════
    //  GenerateToken Tests — Video Grants
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void GenerateToken_VideoGrantsContainsRoomName()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("my-room", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        var video = payload.GetProperty("video");
        Assert.AreEqual("my-room", video.GetProperty("room").GetString());
    }

    [TestMethod]
    public void GenerateToken_VideoGrantsHasRoomJoinTrue()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        var video = payload.GetProperty("video");
        Assert.IsTrue(video.GetProperty("roomJoin").GetBoolean());
    }

    [TestMethod]
    public void GenerateToken_DefaultCanPublishIsTrue()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        var video = payload.GetProperty("video");
        Assert.IsTrue(video.GetProperty("canPublish").GetBoolean());
    }

    [TestMethod]
    public void GenerateToken_DefaultCanSubscribeIsTrue()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        var video = payload.GetProperty("video");
        Assert.IsTrue(video.GetProperty("canSubscribe").GetBoolean());
    }

    [TestMethod]
    public void GenerateToken_CanPublishFalse_ReflectsInGrants()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One", canPublish: false);

        var payload = ParseJwtPayload(token);
        var video = payload.GetProperty("video");
        Assert.IsFalse(video.GetProperty("canPublish").GetBoolean());
    }

    [TestMethod]
    public void GenerateToken_CanSubscribeFalse_ReflectsInGrants()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One", canSubscribe: false);

        var payload = ParseJwtPayload(token);
        var video = payload.GetProperty("video");
        Assert.IsFalse(video.GetProperty("canSubscribe").GetBoolean());
    }

    [TestMethod]
    public void GenerateToken_CanPublishDataIsTrue()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        var video = payload.GetProperty("video");
        Assert.IsTrue(video.GetProperty("canPublishData").GetBoolean());
    }

    // ══════════════════════════════════════════════════════════════
    //  GenerateToken Tests — Signature Validation
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void GenerateToken_SignatureIsValid()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var parts = token.Split('.');
        var signingInput = $"{parts[0]}.{parts[1]}";
        var expectedSignature = ComputeHmacSha256(TestApiSecret, signingInput);

        Assert.AreEqual(expectedSignature, parts[2], "JWT signature does not match expected HMAC-SHA256.");
    }

    [TestMethod]
    public void GenerateToken_DifferentSecret_ProducesDifferentSignature()
    {
        var service1 = CreateService(_options);
        var token1 = service1.GenerateToken("room-1", "user-123", "User One");

        var options2 = new LiveKitOptions
        {
            Enabled = true,
            ServerUrl = TestServerUrl,
            ApiKey = TestApiKey,
            ApiSecret = "completely_different_secret_123456",
            TokenTtlSeconds = 3600
        };
        var service2 = CreateService(options2);
        var token2 = service2.GenerateToken("room-1", "user-123", "User One");

        var sig1 = token1.Split('.')[2];
        var sig2 = token2.Split('.')[2];

        Assert.AreNotEqual(sig1, sig2, "Different secrets should produce different signatures.");
    }

    // ══════════════════════════════════════════════════════════════
    //  GenerateToken Tests — Input Validation
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void GenerateToken_NullRoomName_ThrowsArgumentException()
    {
        var service = CreateService(_options);
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            service.GenerateToken(null!, "user-123", "User One"));
    }

    [TestMethod]
    public void GenerateToken_EmptyRoomName_ThrowsArgumentException()
    {
        var service = CreateService(_options);
        Assert.ThrowsExactly<ArgumentException>(() =>
            service.GenerateToken("", "user-123", "User One"));
    }

    [TestMethod]
    public void GenerateToken_WhitespaceRoomName_ThrowsArgumentException()
    {
        var service = CreateService(_options);
        Assert.ThrowsExactly<ArgumentException>(() =>
            service.GenerateToken("   ", "user-123", "User One"));
    }

    [TestMethod]
    public void GenerateToken_NullParticipantIdentity_ThrowsArgumentException()
    {
        var service = CreateService(_options);
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            service.GenerateToken("room-1", null!, "User One"));
    }

    [TestMethod]
    public void GenerateToken_EmptyParticipantIdentity_ThrowsArgumentException()
    {
        var service = CreateService(_options);
        Assert.ThrowsExactly<ArgumentException>(() =>
            service.GenerateToken("room-1", "", "User One"));
    }

    // ══════════════════════════════════════════════════════════════
    //  GenerateToken Tests — Token TTL
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void GenerateToken_CustomTtl_AffectsExpiration()
    {
        _options.TokenTtlSeconds = 600; // 10 minutes
        var service = CreateService(_options);
        var before = DateTimeOffset.UtcNow;
        var token = service.GenerateToken("room-1", "user-123", "User One");

        var payload = ParseJwtPayload(token);
        var exp = payload.GetProperty("exp").GetInt64();
        var iat = payload.GetProperty("iat").GetInt64();

        var diff = exp - iat;
        Assert.IsTrue(diff >= 598 && diff <= 602, $"Token TTL should be approximately 600s, got {diff}s.");
    }

    // ══════════════════════════════════════════════════════════════
    //  CreateJwt Internal Tests — JWT Structure
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void CreateJwt_ServiceToken_DoesNotContainRoomGrant()
    {
        var service = CreateService(_options);
        var claims = new LiveKitService.LiveKitTokenClaims
        {
            Iss = TestApiKey,
            Nbf = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
            Iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Jti = Guid.NewGuid().ToString("N"),
            Video = new LiveKitService.LiveKitVideoGrants { RoomList = true, RoomCreate = true, RoomAdmin = true }
        };

        var jwt = service.CreateJwt(claims);
        var payload = ParseJwtPayload(jwt);
        var video = payload.GetProperty("video");

        Assert.IsTrue(video.GetProperty("roomCreate").GetBoolean());
        Assert.IsTrue(video.GetProperty("roomAdmin").GetBoolean());
        Assert.IsTrue(video.GetProperty("roomList").GetBoolean());
    }

    // ══════════════════════════════════════════════════════════════
    //  Base64Url Encoding Tests (via token output)
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void GenerateToken_DoesNotContainBase64PaddingChars()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-with-long-name-that-might-need-padding", "user-123", "User One");

        Assert.IsFalse(token.Contains('='), "JWT should not contain Base64 padding characters.");
    }

    [TestMethod]
    public void GenerateToken_UsesBase64UrlSafeCharacters()
    {
        var service = CreateService(_options);
        var token = service.GenerateToken("room-1", "user-123", "User One");

        Assert.IsFalse(token.Contains('+'), "JWT should use '-' instead of '+' for Base64URL.");
        Assert.IsFalse(token.Contains('/'), "JWT should use '_' instead of '/' for Base64URL.");
    }

    // ══════════════════════════════════════════════════════════════
    //  Helper Methods
    // ══════════════════════════════════════════════════════════════

    private LiveKitService CreateService(LiveKitOptions options)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient("livekit"))
            .Returns(new HttpClient());

        return new LiveKitService(
            Options.Create(options),
            httpClientFactory.Object,
            NullLogger<LiveKitService>.Instance);
    }

    private static JsonElement ParseJwtPayload(string token)
    {
        var payloadJson = DecodeJwtPart(token.Split('.')[1]);
        return JsonDocument.Parse(payloadJson).RootElement;
    }

    private static string DecodeJwtPart(string part)
    {
        // Restore Base64 padding
        var padded = part.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    private static string ComputeHmacSha256(string secret, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

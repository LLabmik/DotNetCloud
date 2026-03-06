using System.Security.Cryptography;
using System.Text;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class WopiProofKeyValidatorTests
{
    // Generate a real RSA key pair for testing
    private static readonly RSA TestKey = RSA.Create(2048);
    private static readonly RSA TestOldKey = RSA.Create(2048);

    private static readonly string TestKeySpki =
        Convert.ToBase64String(TestKey.ExportSubjectPublicKeyInfo());

    private static readonly string TestKeyModulus =
        Convert.ToBase64String(TestKey.ExportParameters(false).Modulus!);

    private static readonly string TestKeyExponent =
        Convert.ToBase64String(TestKey.ExportParameters(false).Exponent!);

    private static readonly string TestOldKeySpki =
        Convert.ToBase64String(TestOldKey.ExportSubjectPublicKeyInfo());

    private static WopiProofKeyValidator CreateValidator(
        string? proofKeyValue = null,
        string? oldProofKeyValue = null,
        bool validationEnabled = true)
    {
        var discovery = new Mock<ICollaboraDiscoveryService>();
        discovery.Setup(d => d.DiscoverAsync(default)).ReturnsAsync(new CollaboraDiscoveryResult
        {
            IsAvailable = true,
            ProofKeyValue = proofKeyValue ?? TestKeySpki,
            OldProofKeyValue = oldProofKeyValue ?? TestOldKeySpki
        });

        var options = MsOptions.Options.Create(new CollaboraOptions
        {
            EnableProofKeyValidation = validationEnabled
        });

        return new WopiProofKeyValidator(discovery.Object, options, NullLogger<WopiProofKeyValidator>.Instance);
    }

    private static (string proof, string timestamp) MakeProof(
        string accessToken, string requestUrl, RSA key, long? overrideTimestamp = null)
    {
        var ticks = overrideTimestamp ?? DateTime.UtcNow.ToFileTimeUtc();
        var proofBytes = WopiProofKeyValidator.BuildProofBytes(
            accessToken, requestUrl.ToUpperInvariant(), ticks);

        var signature = key.SignData(proofBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return (Convert.ToBase64String(signature), ticks.ToString());
    }

    private static (string proof, string timestamp) MakeProofWithDateTimeTicks(
        string accessToken, string requestUrl, RSA key, long? overrideTicks = null)
    {
        var ticks = overrideTicks ?? DateTime.UtcNow.Ticks;
        var proofBytes = WopiProofKeyValidator.BuildProofBytes(
            accessToken, requestUrl.ToUpperInvariant(), ticks);

        var signature = key.SignData(proofBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return (Convert.ToBase64String(signature), ticks.ToString());
    }

    [TestMethod]
    public async Task ValidateAsync_ValidProofCurrentKey_ReturnsTrue()
    {
        var validator = CreateValidator();
        const string token = "test-token";
        const string url = "https://example.com/api/v1/wopi/files/abc?access_token=test-token";

        var (proof, timestamp) = MakeProof(token, url, TestKey);

        var result = await validator.ValidateAsync(token, url, proof, null, timestamp);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ValidateAsync_MalformedSpkiWithValidModulusExponent_FallsBackAndReturnsTrue()
    {
        var discovery = new Mock<ICollaboraDiscoveryService>();
        discovery.Setup(d => d.DiscoverAsync(default)).ReturnsAsync(new CollaboraDiscoveryResult
        {
            IsAvailable = true,
            ProofKeyValue = Convert.ToBase64String(Encoding.UTF8.GetBytes("not-spki")),
            ProofKey = TestKeyModulus,
            ProofKeyExponent = TestKeyExponent,
            OldProofKeyValue = null,
            OldProofKey = null,
            OldProofKeyExponent = null
        });

        var validator = new WopiProofKeyValidator(
            discovery.Object,
            MsOptions.Options.Create(new CollaboraOptions { EnableProofKeyValidation = true }),
            NullLogger<WopiProofKeyValidator>.Instance);

        const string token = "test-token";
        const string url = "https://example.com/api/v1/wopi/files/abc?access_token=test-token";
        var (proof, timestamp) = MakeProof(token, url, TestKey);

        var result = await validator.ValidateAsync(token, url, proof, null, timestamp);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ValidateAsync_ValidProofWithDateTimeTicksTimestamp_ReturnsTrue()
    {
        var validator = CreateValidator();
        const string token = "test-token";
        const string url = "https://example.com/api/v1/wopi/files/abc?access_token=test-token";

        var (proof, timestamp) = MakeProofWithDateTimeTicks(token, url, TestKey);

        var result = await validator.ValidateAsync(token, url, proof, null, timestamp);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ValidateAsync_ValidProofOldKey_ReturnsTrue()
    {
        var validator = CreateValidator();
        const string token = "test-token";
        const string url = "https://example.com/api/v1/wopi/files/abc?access_token=test-token";

        // Sign with old key, send as current proof
        var (proof, timestamp) = MakeProof(token, url, TestOldKey);

        var result = await validator.ValidateAsync(token, url, proof, null, timestamp);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ValidateAsync_OldProofCurrentKey_ReturnsTrue()
    {
        var validator = CreateValidator();
        const string token = "test-token";
        const string url = "https://example.com/api/v1/wopi/files/abc?access_token=test-token";

        // Old proof = in-flight request signed with old key during rotation
        var (oldProof, timestamp) = MakeProof(token, url, TestOldKey);
        var (newProof, _) = MakeProof(token, url, TestKey, long.Parse(timestamp));

        // Validator tries: newProof+currentKey, newProof+oldKey, oldProof+currentKey
        // Here newProof+currentKey succeeds
        var result = await validator.ValidateAsync(token, url, newProof, oldProof, timestamp);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ValidateAsync_WrongKey_ReturnsFalse()
    {
        var validator = CreateValidator();
        const string token = "test-token";
        const string url = "https://example.com/api/v1/wopi/files/abc";

        // Sign with an unregistered key
        using var wrongKey = RSA.Create(2048);
        var (proof, timestamp) = MakeProof(token, url, wrongKey);

        var result = await validator.ValidateAsync(token, url, proof, null, timestamp);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ValidateAsync_ExpiredTimestamp_ReturnsFalse()
    {
        var validator = CreateValidator();
        const string token = "test-token";
        const string url = "https://example.com/api/v1/wopi/files/abc";

        // Timestamp 25 minutes ago
        var oldTicks = DateTime.UtcNow.AddMinutes(-25).ToFileTimeUtc();
        var (proof, timestamp) = MakeProof(token, url, TestKey, oldTicks);

        var result = await validator.ValidateAsync(token, url, proof, null, timestamp);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ValidateAsync_MissingProof_ReturnsFalse()
    {
        var validator = CreateValidator();
        const string token = "test-token";
        const string url = "https://example.com/api/v1/wopi/files/abc";

        var result = await validator.ValidateAsync(token, url, proof: null, proofOld: null,
            timestamp: DateTime.UtcNow.ToFileTimeUtc().ToString());

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ValidateAsync_MissingTimestamp_ReturnsFalse()
    {
        var validator = CreateValidator();
        const string token = "test-token";
        const string url = "https://example.com/api/v1/wopi/files/abc";

        var (proof, _) = MakeProof(token, url, TestKey);

        var result = await validator.ValidateAsync(token, url, proof, null, timestamp: null);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ValidateAsync_InvalidBase64Proof_ReturnsFalse()
    {
        var validator = CreateValidator();
        var result = await validator.ValidateAsync(
            "token", "https://example.com/wopi", "not valid base64!!!", null,
            DateTime.UtcNow.ToFileTimeUtc().ToString());

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ValidateAsync_NoProofKeysInDiscovery_ReturnsFalse()
    {
        var discovery = new Mock<ICollaboraDiscoveryService>();
        discovery.Setup(d => d.DiscoverAsync(default)).ReturnsAsync(new CollaboraDiscoveryResult
        {
            IsAvailable = false,
            ProofKeyValue = null,
            OldProofKeyValue = null
        });

        var validator = new WopiProofKeyValidator(
            discovery.Object,
            MsOptions.Options.Create(new CollaboraOptions { EnableProofKeyValidation = true }),
            NullLogger<WopiProofKeyValidator>.Instance);

        var (proof, timestamp) = MakeProof("token", "https://example.com", TestKey);
        var result = await validator.ValidateAsync("token", "https://example.com", proof, null, timestamp);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void BuildProofBytes_KnownValues_ProducesExpectedLayout()
    {
        const string token = "abc";
        const string upperUrl = "HTTPS://EXAMPLE.COM/WOPI";
        const long ticks = 1000L;

        var bytes = WopiProofKeyValidator.BuildProofBytes(token, upperUrl, ticks);

        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var urlBytes = Encoding.UTF8.GetBytes(upperUrl);
        int expectedLen = 4 + tokenBytes.Length + 4 + urlBytes.Length + 4 + 8;

        Assert.AreEqual(expectedLen, bytes.Length);

        // Verify token length at offset 0
        int tokenLen = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        Assert.AreEqual(tokenBytes.Length, tokenLen);

        // Verify url length at offset 4 + tokenLen
        int urlOffset = 4 + tokenBytes.Length;
        int urlLen = (bytes[urlOffset] << 24) | (bytes[urlOffset + 1] << 16)
            | (bytes[urlOffset + 2] << 8) | bytes[urlOffset + 3];
        Assert.AreEqual(urlBytes.Length, urlLen);

        // Verify size-of-long (8) at offset urlOffset + 4 + urlLen
        int timestampSizeOffset = urlOffset + 4 + urlBytes.Length;
        int sizeOfLong = (bytes[timestampSizeOffset] << 24) | (bytes[timestampSizeOffset + 1] << 16)
            | (bytes[timestampSizeOffset + 2] << 8) | bytes[timestampSizeOffset + 3];
        Assert.AreEqual(8, sizeOfLong);
    }
}

using DotNetCloud.Core.Auth.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Core.Auth.Tests.Security;

[TestClass]
public sealed class OidcKeyManagerTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dnc-oidc-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // ── LoadOrCreateKey ───────────────────────────────────────────────

    [TestMethod]
    public void LoadOrCreateKey_FileNotExists_CreatesNewKey()
    {
        var filePath = Path.Combine(_tempDir, "signing-key.pem");
        var key = OidcKeyManager.LoadOrCreateKey(filePath);

        Assert.IsNotNull(key);
        Assert.IsTrue(File.Exists(filePath), "Key file should have been created");

        var content = File.ReadAllText(filePath);
        Assert.IsTrue(content.Contains("BEGIN RSA PRIVATE KEY"), "Should be PEM format");
    }

    [TestMethod]
    public void LoadOrCreateKey_FileExists_LoadsExistingKey()
    {
        var filePath = Path.Combine(_tempDir, "signing-key.pem");
        var firstKey = OidcKeyManager.LoadOrCreateKey(filePath);

        var secondKey = OidcKeyManager.LoadOrCreateKey(filePath);

        Assert.IsNotNull(secondKey);
        Assert.AreEqual(
            firstKey.KeySize,
            secondKey.KeySize,
            "Loaded key should have same key size");
    }

    // ── GenerateRotatedKey ────────────────────────────────────────────

    [TestMethod]
    public void GenerateRotatedKey_CreatesVersionedFile()
    {
        var key = OidcKeyManager.GenerateRotatedKey(_tempDir, "signing-key");

        Assert.IsNotNull(key);

        var files = Directory.GetFiles(_tempDir, "signing-key-*.pem");
        Assert.IsTrue(files.Length > 0, "Should create a versioned key file");

        var content = File.ReadAllText(files[0]);
        Assert.IsTrue(content.Contains("BEGIN RSA PRIVATE KEY"));
    }

    [TestMethod]
    public void GenerateRotatedKey_AvoidsNameCollisions()
    {
        var key1 = OidcKeyManager.GenerateRotatedKey(_tempDir, "signing-key");
        var key2 = OidcKeyManager.GenerateRotatedKey(_tempDir, "signing-key");

        var files = Directory.GetFiles(_tempDir, "signing-key-*.pem");
        Assert.AreEqual(2, files.Length, "Should create two unique key files");
    }

    // ── LoadAllKeys ───────────────────────────────────────────────────

    [TestMethod]
    public void LoadAllKeys_NoKeys_ReturnsEmpty()
    {
        var keys = OidcKeyManager.LoadAllKeys(_tempDir, "signing-key");
        Assert.AreEqual(0, keys.Count);
    }

    [TestMethod]
    public void LoadAllKeys_MultipleKeys_LoadsAll()
    {
        OidcKeyManager.GenerateRotatedKey(_tempDir, "signing-key");
        OidcKeyManager.GenerateRotatedKey(_tempDir, "signing-key");

        var keys = OidcKeyManager.LoadAllKeys(_tempDir, "signing-key");
        Assert.AreEqual(2, keys.Count, "Should load both keys");
    }

    [TestMethod]
    public void LoadAllKeys_FiltersByPrefix()
    {
        OidcKeyManager.GenerateRotatedKey(_tempDir, "signing-key");
        OidcKeyManager.GenerateRotatedKey(_tempDir, "encryption-key");

        var signingKeys = OidcKeyManager.LoadAllKeys(_tempDir, "signing-key");
        var encryptionKeys = OidcKeyManager.LoadAllKeys(_tempDir, "encryption-key");

        Assert.AreEqual(1, signingKeys.Count, "Should load only signing keys");
        Assert.AreEqual(1, encryptionKeys.Count, "Should load only encryption keys");
    }

    // ── ShouldRotate ──────────────────────────────────────────────────

    [TestMethod]
    public void ShouldRotate_NoKeys_ReturnsTrue()
    {
        var result = OidcKeyManager.ShouldRotate(_tempDir, "signing-key", TimeSpan.FromDays(90));
        Assert.IsTrue(result, "Should rotate when no keys exist");
    }

    [TestMethod]
    public void ShouldRotate_NewKey_ReturnsFalse()
    {
        OidcKeyManager.GenerateRotatedKey(_tempDir, "signing-key");

        var result = OidcKeyManager.ShouldRotate(_tempDir, "signing-key", TimeSpan.FromDays(90));
        Assert.IsFalse(result, "Should not rotate when key is new");
    }

    // ── CleanupOldKeys ────────────────────────────────────────────────

    [TestMethod]
    public void CleanupOldKeys_NoOldKeys_DoesNothing()
    {
        OidcKeyManager.GenerateRotatedKey(_tempDir, "signing-key");

        // Should not throw
        OidcKeyManager.CleanupOldKeys(_tempDir, "signing-key", TimeSpan.FromDays(-1));
        var files = Directory.GetFiles(_tempDir, "signing-key*.pem");
        Assert.AreEqual(1, files.Length, "Should keep the only key");
    }

    [TestMethod]
    public void CleanupOldKeys_AlwaysKeepsNewest()
    {
        // Create an old key by directly writing a file with an old timestamp
        var oldFile = Path.Combine(_tempDir, "signing-key-2020-01-01.pem");
        GenerateKeyFile(oldFile);
        File.SetLastWriteTimeUtc(oldFile, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        OidcKeyManager.GenerateRotatedKey(_tempDir, "signing-key");

        OidcKeyManager.CleanupOldKeys(_tempDir, "signing-key", TimeSpan.FromDays(30));

        var remaining = Directory.GetFiles(_tempDir, "signing-key*.pem");
        Assert.AreEqual(1, remaining.Length, "Should keep the newest key only");
    }

    // ── GetNewestKeyFile ──────────────────────────────────────────────

    [TestMethod]
    public void GetNewestKeyFile_NoKeys_ReturnsNull()
    {
        var result = OidcKeyManager.GetNewestKeyFile(_tempDir, "signing-key");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetNewestKeyFile_ReturnsNewest()
    {
        OidcKeyManager.GenerateRotatedKey(_tempDir, "signing-key");
        OidcKeyManager.GenerateRotatedKey(_tempDir, "signing-key");

        var newest = OidcKeyManager.GetNewestKeyFile(_tempDir, "signing-key");
        Assert.IsNotNull(newest);
        Assert.IsTrue(newest.Contains(DateTime.UtcNow.ToString("yyyy-MM-dd")),
            "Newest file should contain today's date");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void GenerateKeyFile(string filePath)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var pem = rsa.ExportRSAPrivateKeyPem();
        File.WriteAllText(filePath, pem);
    }
}

using DotNetCloud.Client.Core.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.Auth;

[TestClass]
public class EncryptedFileTokenStoreTests
{
    private string _tempDir = null!;
    private EncryptedFileTokenStore _store = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _store = new EncryptedFileTokenStore(_tempDir, NullLogger<EncryptedFileTokenStore>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task SaveAndLoad_RoundTripsTokenInfo()
    {
        var tokens = new TokenInfo
        {
            AccessToken = "access-token-abc",
            RefreshToken = "refresh-token-xyz",
            ExpiresAt = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

        await _store.SaveAsync("account-key-1", tokens);
        var loaded = await _store.LoadAsync("account-key-1");

        Assert.IsNotNull(loaded);
        Assert.AreEqual(tokens.AccessToken, loaded.AccessToken);
        Assert.AreEqual(tokens.RefreshToken, loaded.RefreshToken);
        Assert.AreEqual(tokens.ExpiresAt, loaded.ExpiresAt);
    }

    [TestMethod]
    public async Task LoadAsync_NonExistentKey_ReturnsNull()
    {
        var result = await _store.LoadAsync("no-such-key");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesToken()
    {
        var tokens = new TokenInfo { AccessToken = "tok", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
        await _store.SaveAsync("to-delete", tokens);

        await _store.DeleteAsync("to-delete");

        var result = await _store.LoadAsync("to-delete");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SaveAsync_DifferentKeys_StoresSeparately()
    {
        var tokens1 = new TokenInfo { AccessToken = "token-1", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
        var tokens2 = new TokenInfo { AccessToken = "token-2", ExpiresAt = DateTimeOffset.UtcNow.AddHours(2) };

        await _store.SaveAsync("key-1", tokens1);
        await _store.SaveAsync("key-2", tokens2);

        var loaded1 = await _store.LoadAsync("key-1");
        var loaded2 = await _store.LoadAsync("key-2");

        Assert.AreEqual("token-1", loaded1?.AccessToken);
        Assert.AreEqual("token-2", loaded2?.AccessToken);
    }

    [TestMethod]
    public async Task SaveAsync_WhenExistingFileNotWritable_ThrowsActionableUnauthorizedAccess()
    {
        var tokens = new TokenInfo { AccessToken = "tok", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
        await _store.SaveAsync("account-key-1", tokens);

        // Make the existing token file read-only so the overwrite is denied. This mirrors the
        // real-world case where a token file is owned by another user / created elevated.
        var tokFile = Directory.EnumerateFiles(_tempDir).Single();
        File.SetAttributes(tokFile, FileAttributes.ReadOnly);
        try
        {
            var ex = await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
                () => _store.SaveAsync("account-key-1", tokens));

            // The message should explain the cause and how to recover, not be a bare OS error.
            StringAssert.Contains(ex.Message, "denied");
            // Remediation wording is platform-specific: Windows suggests repairing the sync data
            // folder (icacls), while Unix/macOS points at sync data directory ownership.
            var expectedWording = OperatingSystem.IsWindows() ? "sync data folder" : "sync data directory";
            StringAssert.Contains(ex.Message, expectedWording);
        }
        finally
        {
            File.SetAttributes(tokFile, FileAttributes.Normal);
        }
    }

    [TestMethod]
    public async Task DeleteAsync_WhenExistingFileNotWritable_ThrowsUnauthorizedAccess()
    {
        if (!OperatingSystem.IsWindows())
        {
            // On Unix, deletion is governed by directory permissions, not the file's read-only
            // bit, so this scenario cannot be simulated portably there.
            return;
        }

        var tokens = new TokenInfo { AccessToken = "tok", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
        await _store.SaveAsync("to-delete", tokens);

        var tokFile = Directory.EnumerateFiles(_tempDir).Single();
        File.SetAttributes(tokFile, FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
                () => _store.DeleteAsync("to-delete"));
        }
        finally
        {
            File.SetAttributes(tokFile, FileAttributes.Normal);
        }
    }
}

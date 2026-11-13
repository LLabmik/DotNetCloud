using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Data.Services;
using DotNetCloud.Core.Modules;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Tests.Modules;

/// <summary>
/// Tests for <see cref="ModuleSettingsStore"/>.
/// </summary>
[TestClass]
public class ModuleSettingsStoreTests
{
    private CoreDbContext _context = null!;
    private ModuleSettingsStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: $"ModuleSettingsTestDb_{Guid.CreateVersion7()}")
            .Options;

        _context = new CoreDbContext(options, new PostgreSqlNamingStrategy());
        _store = new ModuleSettingsStore(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context?.Dispose();

    [TestMethod]
    public async Task SetAsync_NewKey_ThenGetAsync_ReturnsValue()
    {
        await _store.SetAsync("dotnetcloud.example", "theme", "dark");

        var value = await _store.GetAsync("dotnetcloud.example", "theme");

        Assert.AreEqual("dark", value);
    }

    [TestMethod]
    public async Task GetAsync_MissingKey_ReturnsNull()
    {
        var value = await _store.GetAsync("dotnetcloud.example", "missing");

        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task SetAsync_ExistingKey_UpdatesInPlace()
    {
        await _store.SetAsync("dotnetcloud.example", "theme", "dark");
        await _store.SetAsync("dotnetcloud.example", "theme", "light");

        var value = await _store.GetAsync("dotnetcloud.example", "theme");
        var rows = await _context.ModuleSettings.CountAsync(s => s.Key == "theme");

        Assert.AreEqual("light", value);
        Assert.AreEqual(1, rows);
    }

    [TestMethod]
    public async Task SetAsync_SameKeyForTwoModules_IsNamespaced()
    {
        await _store.SetAsync("dotnetcloud.one", "theme", "dark");
        await _store.SetAsync("dotnetcloud.two", "theme", "light");

        Assert.AreEqual("dark", await _store.GetAsync("dotnetcloud.one", "theme"));
        Assert.AreEqual("light", await _store.GetAsync("dotnetcloud.two", "theme"));
    }

    [TestMethod]
    public async Task SetAsync_InvalidKey_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _store.SetAsync("dotnetcloud.example", "bad key\nwith newline", "value"));
    }

    [TestMethod]
    public async Task SetAsync_OversizedValue_Throws()
    {
        var oversized = new string('x', ModuleSettingLimits.MaxValueLength + 1);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _store.SetAsync("dotnetcloud.example", "blob", oversized));
    }

    [TestMethod]
    public async Task SetAsync_RecordsActingUserAndSecretFlag()
    {
        var userId = Guid.CreateVersion7();
        var store = new ModuleSettingsStore(_context, new TestCipher());

        await store.SetAsync("dotnetcloud.example", "token", "abc", isSecret: true, updatedByUserId: userId);

        var row = await _context.ModuleSettings.SingleAsync(s => s.Key == "token");
        Assert.IsTrue(row.IsSecret);
        Assert.AreEqual(userId, row.UpdatedByUserId);
    }

    [TestMethod]
    public async Task SetAsync_Secret_StoresCipherTextAndReadsBackPlainText()
    {
        var store = new ModuleSettingsStore(_context, new TestCipher());

        await store.SetAsync("dotnetcloud.ai", "ApiKey", "sk-live-secret", isSecret: true);

        var row = await _context.ModuleSettings.SingleAsync(s => s.Key == "ApiKey");
        Assert.AreNotEqual("sk-live-secret", row.Value, "a secret must never be stored as plain text");
        StringAssert.StartsWith(row.Value, "enc:");
        Assert.AreEqual("sk-live-secret", await store.GetAsync("dotnetcloud.ai", "ApiKey"));
    }

    [TestMethod]
    public async Task SetAsync_SecretWithoutCipher_FailsClosed()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _store.SetAsync("dotnetcloud.ai", "ApiKey", "sk-live-secret", isSecret: true));

        Assert.AreEqual(0, await _context.ModuleSettings.CountAsync());
    }

    [TestMethod]
    public async Task ListAsync_SecretValueIsMaskedAndPlainValueIsReturned()
    {
        var store = new ModuleSettingsStore(_context, new TestCipher());
        await store.SetAsync("dotnetcloud.ai", "Provider", "ollama");
        await store.SetAsync("dotnetcloud.ai", "ApiKey", "sk-live-secret", isSecret: true);

        var entries = await store.ListAsync("dotnetcloud.ai");

        var provider = entries.Single(e => e.Key == "Provider");
        Assert.IsFalse(provider.IsSecret);
        Assert.AreEqual("ollama", provider.Value);

        var apiKey = entries.Single(e => e.Key == "ApiKey");
        Assert.IsTrue(apiKey.IsSecret);
        Assert.IsTrue(apiKey.HasValue);
        Assert.IsNull(apiKey.Value, "a listing must never return a secret value");
    }

    [TestMethod]
    public async Task ListAsync_IsNamespacedByModule()
    {
        await _store.SetAsync("dotnetcloud.one", "theme", "dark");
        await _store.SetAsync("dotnetcloud.two", "theme", "light");

        var entries = await _store.ListAsync("dotnetcloud.one");

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("dotnetcloud.one", entries[0].ModuleId);
    }

    [TestMethod]
    public async Task DeleteAsync_ExistingKey_RemovesRowAndReturnsTrue()
    {
        await _store.SetAsync("dotnetcloud.example", "theme", "dark");

        var deleted = await _store.DeleteAsync("dotnetcloud.example", "theme");

        Assert.IsTrue(deleted);
        Assert.IsNull(await _store.GetAsync("dotnetcloud.example", "theme"));
        Assert.AreEqual(0, await _context.ModuleSettings.CountAsync());
    }

    [TestMethod]
    public async Task DeleteAsync_MissingKey_ReturnsFalse()
    {
        Assert.IsFalse(await _store.DeleteAsync("dotnetcloud.example", "missing"));
    }

    /// <summary>Deterministic stand-in for the DataProtection-backed cipher.</summary>
    private sealed class TestCipher : IModuleSettingCipher
    {
        public string Protect(string moduleId, string plainText) => $"enc:{moduleId}:{plainText}";

        public string Unprotect(string moduleId, string cipherText) =>
            cipherText.StartsWith($"enc:{moduleId}:", StringComparison.Ordinal)
                ? cipherText[($"enc:{moduleId}:".Length)..]
                : throw new InvalidOperationException("Wrong module scope.");
    }
}

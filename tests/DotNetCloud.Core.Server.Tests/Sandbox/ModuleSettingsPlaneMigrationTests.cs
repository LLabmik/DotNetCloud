using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Data.Services;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Sandbox;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Tests.Sandbox;

/// <summary>
/// Tests for <see cref="ModuleSettingsPlaneMigrator"/>: a converted module reads the module settings
/// plane, so its existing <c>SystemSettings</c> values must move there — otherwise an upgraded install
/// silently reverts to the built-in defaults (the operator's model and provider endpoint appear to be
/// ignored).
/// </summary>
[TestClass]
public class ModuleSettingsPlaneMigrationTests
{
    private CoreDbContext _context = null!;
    private ModuleSettingsStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: $"ModuleSettingsMigration_{Guid.CreateVersion7()}")
            .Options;

        _context = new CoreDbContext(options, new PostgreSqlNamingStrategy());
        _store = new ModuleSettingsStore(_context, new TestCipher());
    }

    [TestCleanup]
    public void Cleanup() => _context?.Dispose();

    [TestMethod]
    public async Task MigrateAsync_ConvertedModule_CopiesValuesAndRemovesStaleRows()
    {
        Seed("dotnetcloud.ai", "Provider", "ollama");
        Seed("dotnetcloud.ai", "ApiBaseUrl", "http://llm.internal:11434/");
        Seed("dotnetcloud.ai", "DefaultModel", "gemma4:12b");
        Seed("dotnetcloud.ai", "ApiKey", "sk-live-secret");
        await _context.SaveChangesAsync();

        var report = await ModuleSettingsPlaneMigrator.MigrateAsync(_context, _store);

        Assert.AreEqual(4, report.Copied);
        Assert.AreEqual(4, report.RemovedSystemSettings);
        Assert.AreEqual("gemma4:12b", await _store.GetAsync("dotnetcloud.ai", "DefaultModel"));
        Assert.AreEqual("http://llm.internal:11434/", await _store.GetAsync("dotnetcloud.ai", "ApiBaseUrl"));
        Assert.AreEqual(0, await _context.SystemSettings.CountAsync(s => s.Module == "dotnetcloud.ai"));
    }

    [TestMethod]
    public async Task MigrateAsync_CredentialKey_IsStoredAsSecret()
    {
        Seed("dotnetcloud.ai", "ApiKey", "sk-live-secret");
        await _context.SaveChangesAsync();

        await ModuleSettingsPlaneMigrator.MigrateAsync(_context, _store);

        var row = await _context.ModuleSettings.SingleAsync(s => s.Key == "ApiKey");
        Assert.IsTrue(row.IsSecret, "an API key must never be stored as a plain module setting");
        StringAssert.StartsWith(row.Value, "enc:", "the value must be encrypted at rest");
        Assert.AreEqual("sk-live-secret", await _store.GetAsync("dotnetcloud.ai", "ApiKey"));
    }

    [TestMethod]
    public async Task MigrateAsync_OtherModulesSettings_AreLeftAlone()
    {
        Seed("dotnetcloud.chat", "Limits:MaxMessageLength", "10000");
        await _context.SaveChangesAsync();

        var report = await ModuleSettingsPlaneMigrator.MigrateAsync(_context, _store);

        Assert.AreEqual(0, report.Copied);
        Assert.AreEqual(1, await _context.SystemSettings.CountAsync(s => s.Module == "dotnetcloud.chat"));
        Assert.AreEqual(0, await _context.ModuleSettings.CountAsync());
    }

    [TestMethod]
    public async Task MigrateAsync_ValueAlreadyInTheSettingsPlane_IsNotOverwritten()
    {
        Seed("dotnetcloud.ai", "DefaultModel", "old-from-system-settings");
        await _context.SaveChangesAsync();
        await _store.SetAsync("dotnetcloud.ai", "DefaultModel", "new-from-admin-panel");

        var report = await ModuleSettingsPlaneMigrator.MigrateAsync(_context, _store);

        Assert.AreEqual(0, report.Copied);
        Assert.AreEqual("new-from-admin-panel", await _store.GetAsync("dotnetcloud.ai", "DefaultModel"));
        Assert.AreEqual(0, await _context.SystemSettings.CountAsync(s => s.Module == "dotnetcloud.ai"),
            "the stale system row is removed even when the new value wins");
    }

    [TestMethod]
    public async Task MigrateAsync_EmptyValue_IsNotCopied()
    {
        Seed("dotnetcloud.ai", "ApiKey", string.Empty);
        await _context.SaveChangesAsync();

        var report = await ModuleSettingsPlaneMigrator.MigrateAsync(_context, _store);

        Assert.AreEqual(0, report.Copied);
        Assert.AreEqual(0, await _context.ModuleSettings.CountAsync(),
            "an empty key must stay absent so the module falls back to its own default");
        Assert.AreEqual(1, report.RemovedSystemSettings);
    }

    [TestMethod]
    public async Task MigrateAsync_SecondRun_IsANoOp()
    {
        Seed("dotnetcloud.ai", "DefaultModel", "gemma4:12b");
        await _context.SaveChangesAsync();

        await ModuleSettingsPlaneMigrator.MigrateAsync(_context, _store);
        var second = await ModuleSettingsPlaneMigrator.MigrateAsync(_context, _store);

        Assert.AreEqual(0, second.Copied);
        Assert.AreEqual(0, second.RemovedSystemSettings);
        Assert.AreEqual(1, await _context.ModuleSettings.CountAsync());
    }

    private void Seed(string module, string key, string value) =>
        _context.SystemSettings.Add(new SystemSetting { Module = module, Key = key, Value = value });

    /// <summary>Deterministic stand-in for the DataProtection-backed cipher.</summary>
    private sealed class TestCipher : IModuleSettingCipher
    {
        public string Protect(string moduleId, string plainText) => $"enc:{moduleId}:{plainText}";

        public string Unprotect(string moduleId, string cipherText) =>
            cipherText.StartsWith($"enc:{moduleId}:", StringComparison.Ordinal)
                ? cipherText[$"enc:{moduleId}:".Length..]
                : throw new InvalidOperationException("Wrong module scope.");
    }
}

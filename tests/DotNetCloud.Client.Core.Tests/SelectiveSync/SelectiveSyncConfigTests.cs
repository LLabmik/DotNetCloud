using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.SelectiveSync;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.SelectiveSync;

[TestClass]
public class SelectiveSyncConfigTests
{
    private SelectiveSyncConfig _config = null!;
    private readonly Guid _contextId = Guid.CreateVersion7();

    [TestInitialize]
    public void Initialize()
    {
        _config = new SelectiveSyncConfig();
    }

    // ── IsIncluded (default behavior) ───────────────────────────────────────

    [TestMethod]
    public void IsIncluded_NoRules_DefaultsToTrue()
    {
        Assert.IsTrue(_config.IsIncluded(_contextId, "/any/path"));
    }

    [TestMethod]
    public void IsIncluded_DotNetCloudRoot_IsAlwaysExcluded()
    {
        Assert.IsFalse(_config.IsIncluded(_contextId, "/_DotNetCloud"));
        Assert.IsFalse(_config.IsIncluded(_contextId, "/_DotNetCloud/Shared/Mounted.txt"));
    }

    // ── Include / Exclude ───────────────────────────────────────────────────

    [TestMethod]
    public void Exclude_ExcludesFolderAndSubfolders()
    {
        _config.Exclude(_contextId, "/photos/private");

        Assert.IsFalse(_config.IsIncluded(_contextId, "/photos/private"));
        Assert.IsFalse(_config.IsIncluded(_contextId, "/photos/private/vacation"));
    }

    [TestMethod]
    public void Include_OverridesExcludeForMoreSpecificPath()
    {
        _config.Exclude(_contextId, "/photos");
        _config.Include(_contextId, "/photos/work");

        Assert.IsFalse(_config.IsIncluded(_contextId, "/photos/personal"));
        Assert.IsTrue(_config.IsIncluded(_contextId, "/photos/work"));
        Assert.IsTrue(_config.IsIncluded(_contextId, "/photos/work/2025"));
    }

    [TestMethod]
    public void Include_CannotOverrideReservedDotNetCloudExclusion()
    {
        _config.Include(_contextId, "/_DotNetCloud");

        Assert.IsFalse(_config.IsIncluded(_contextId, "/_DotNetCloud/Admin Share"));
        Assert.AreEqual(0, _config.GetRules(_contextId).Count);
    }

    [TestMethod]
    public void Exclude_ReplacesExistingRuleForSamePath()
    {
        _config.Include(_contextId, "/docs");
        _config.Exclude(_contextId, "/docs"); // Replace

        Assert.IsFalse(_config.IsIncluded(_contextId, "/docs/report.txt"));
    }

    [TestMethod]
    public void ClearRules_RemovesAllRulesForContext()
    {
        _config.Exclude(_contextId, "/private");
        _config.ClearRules(_contextId);

        Assert.IsTrue(_config.IsIncluded(_contextId, "/private"));
    }

    // ── GetRules ────────────────────────────────────────────────────────────

    [TestMethod]
    public void GetRules_NoRules_ReturnsEmpty()
    {
        var rules = _config.GetRules(_contextId);
        Assert.AreEqual(0, rules.Count);
    }

    [TestMethod]
    public void GetRules_AfterAddingRules_ReturnsAll()
    {
        _config.Include(_contextId, "/work");
        _config.Exclude(_contextId, "/work/temp");

        var rules = _config.GetRules(_contextId);
        Assert.AreEqual(2, rules.Count);
    }

    // ── Multi-context isolation ─────────────────────────────────────────────

    [TestMethod]
    public void Rules_IsolatedPerContext()
    {
        var ctx2 = Guid.CreateVersion7();
        _config.Exclude(_contextId, "/private");

        Assert.IsFalse(_config.IsIncluded(_contextId, "/private/doc.txt"));
        Assert.IsTrue(_config.IsIncluded(ctx2, "/private/doc.txt")); // ctx2 unaffected
    }

    // ── Persistence (per-context SQLite state DB) ───────────────────────────

    [TestMethod]
    public async Task SaveAndLoad_PersistsRules()
    {
        _config.Exclude(_contextId, "/private");
        _config.Include(_contextId, "/work");
        var dbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db");
        var stateDb = new LocalStateDb(NullLogger<LocalStateDb>.Instance);

        try
        {
            await stateDb.InitializeAsync(dbPath);
            await _config.SaveAsync(stateDb, dbPath, _contextId);

            var loaded = new SelectiveSyncConfig();
            await loaded.LoadAsync(stateDb, dbPath, _contextId);

            var rules = loaded.GetRules(_contextId);
            Assert.AreEqual(2, rules.Count);
            Assert.IsTrue(rules.Any(r => r.FolderPath == "/private" && !r.IsInclude));
            Assert.IsTrue(rules.Any(r => r.FolderPath == "/work" && r.IsInclude));
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [TestMethod]
    public async Task Load_EmptyDb_NoOp()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db");
        var stateDb = new LocalStateDb(NullLogger<LocalStateDb>.Instance);

        try
        {
            await stateDb.InitializeAsync(dbPath);
            await _config.LoadAsync(stateDb, dbPath, _contextId); // Should not throw
            Assert.AreEqual(0, _config.GetRules(_contextId).Count);
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [TestMethod]
    public async Task Load_SizeLimitRule_PreservesSource()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db");
        var stateDb = new LocalStateDb(NullLogger<LocalStateDb>.Instance);

        try
        {
            await stateDb.InitializeAsync(dbPath);
            await stateDb.ReplaceSyncFolderRulesAsync(
                dbPath,
                [new SyncFolderRule { RelativePath = "bigfiles", IsInclude = false, Source = "SizeLimit" }]);

            var loaded = new SelectiveSyncConfig();
            await loaded.LoadAsync(stateDb, dbPath, _contextId);

            var rules = loaded.GetRules(_contextId);
            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual("/bigfiles", rules[0].FolderPath);
            Assert.IsFalse(rules[0].IsInclude);
            Assert.AreEqual("SizeLimit", rules[0].Source);
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }
}

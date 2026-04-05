using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class EqPresetServiceTests
{
    private MusicDbContext _db;
    private EqPresetService _service;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new EqPresetService(_db, NullLogger<EqPresetService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── ListPresets ──────────────────────────────────────────────────

    [TestMethod]
    public async Task ListPresets_ReturnsBuiltInAndCustom()
    {
        await TestHelpers.SeedEqPresetAsync(_db, name: "Flat", isBuiltIn: true);
        await TestHelpers.SeedEqPresetAsync(_db, _caller.UserId, "My Custom");

        var result = await _service.ListPresetsAsync(_caller);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task ListPresets_BuiltInFirst()
    {
        await TestHelpers.SeedEqPresetAsync(_db, _caller.UserId, "Custom");
        await TestHelpers.SeedEqPresetAsync(_db, name: "Built-In", isBuiltIn: true);

        var result = await _service.ListPresetsAsync(_caller);

        Assert.IsTrue(result[0].IsBuiltIn);
    }

    [TestMethod]
    public async Task ListPresets_ExcludesOtherUserCustom()
    {
        var otherUser = Guid.NewGuid();
        await TestHelpers.SeedEqPresetAsync(_db, otherUser, "Other's Preset");
        await TestHelpers.SeedEqPresetAsync(_db, _caller.UserId, "My Preset");

        var result = await _service.ListPresetsAsync(_caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("My Preset", result[0].Name);
    }

    // ─── GetPreset ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetPreset_Existing_ReturnsDto()
    {
        var preset = await TestHelpers.SeedEqPresetAsync(_db, _caller.UserId, "Rock Boost");

        var result = await _service.GetPresetAsync(preset.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Rock Boost", result.Name);
    }

    [TestMethod]
    public async Task GetPreset_NonExistent_ReturnsNull()
    {
        var result = await _service.GetPresetAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetPreset_DeserializesBands()
    {
        var preset = await TestHelpers.SeedEqPresetAsync(_db, _caller.UserId);

        var result = await _service.GetPresetAsync(preset.Id, _caller);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Bands.Count > 0);
    }

    // ─── CreatePreset ─────────────────────────────────────────────────

    [TestMethod]
    public async Task CreatePreset_PersistsToDatabase()
    {
        var dto = new SaveEqPresetDto
        {
            Name = "New Preset",
            Bands = new Dictionary<string, double> { { "60Hz", 3.0 }, { "230Hz", -1.0 } }
        };

        var result = await _service.CreatePresetAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("New Preset", result.Name);
        Assert.IsFalse(result.IsBuiltIn);
    }

    [TestMethod]
    public async Task CreatePreset_HasCorrectBands()
    {
        var dto = new SaveEqPresetDto
        {
            Name = "Band Test",
            Bands = new Dictionary<string, double> { { "60Hz", 5.0 }, { "14kHz", -3.0 } }
        };

        var result = await _service.CreatePresetAsync(dto, _caller);

        Assert.AreEqual(5.0, result.Bands["60Hz"]);
        Assert.AreEqual(-3.0, result.Bands["14kHz"]);
    }

    // ─── UpdatePreset ─────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdatePreset_UpdatesName()
    {
        var preset = await TestHelpers.SeedEqPresetAsync(_db, _caller.UserId, "Old Name");

        var dto = new SaveEqPresetDto
        {
            Name = "New Name",
            Bands = new Dictionary<string, double> { { "60Hz", 0 } }
        };
        var result = await _service.UpdatePresetAsync(preset.Id, dto, _caller);

        Assert.AreEqual("New Name", result.Name);
    }

    [TestMethod]
    public async Task UpdatePreset_BuiltIn_Throws()
    {
        var preset = await TestHelpers.SeedEqPresetAsync(_db, name: "Flat", isBuiltIn: true);

        var dto = new SaveEqPresetDto
        {
            Name = "Hacked Flat",
            Bands = new Dictionary<string, double> { { "60Hz", 10 } }
        };

        await Assert.ThrowsExactlyAsync<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.UpdatePresetAsync(preset.Id, dto, _caller));
    }

    [TestMethod]
    public async Task UpdatePreset_NonExistent_Throws()
    {
        var dto = new SaveEqPresetDto
        {
            Name = "Ghost",
            Bands = new Dictionary<string, double> { { "60Hz", 0 } }
        };

        await Assert.ThrowsExactlyAsync<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.UpdatePresetAsync(Guid.NewGuid(), dto, _caller));
    }

    // ─── DeletePreset ─────────────────────────────────────────────────

    [TestMethod]
    public async Task DeletePreset_RemovesFromDatabase()
    {
        var preset = await TestHelpers.SeedEqPresetAsync(_db, _caller.UserId);

        await _service.DeletePresetAsync(preset.Id, _caller);

        var entry = await _db.EqPresets.FindAsync(preset.Id);
        Assert.IsNull(entry);
    }

    [TestMethod]
    public async Task DeletePreset_BuiltIn_Throws()
    {
        var preset = await TestHelpers.SeedEqPresetAsync(_db, name: "Flat", isBuiltIn: true);

        await Assert.ThrowsExactlyAsync<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.DeletePresetAsync(preset.Id, _caller));
    }

    [TestMethod]
    public async Task DeletePreset_NonExistent_Throws()
    {
        await Assert.ThrowsExactlyAsync<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.DeletePresetAsync(Guid.NewGuid(), _caller));
    }

    // ─── SetActivePreset ──────────────────────────────────────────────

    [TestMethod]
    public async Task SetActivePreset_SetsPreferenceCorrectly()
    {
        var preset = await TestHelpers.SeedEqPresetAsync(_db, _caller.UserId, "Active Preset");

        await _service.SetActivePresetAsync(preset.Id, _caller);

        var prefs = _db.UserMusicPreferences.FirstOrDefault(p => p.UserId == _caller.UserId);
        Assert.IsNotNull(prefs);
        Assert.AreEqual(preset.Id, prefs.ActiveEqPresetId);
    }

    [TestMethod]
    public async Task SetActivePreset_UpdatesExistingPreference()
    {
        var p1 = await TestHelpers.SeedEqPresetAsync(_db, _caller.UserId, "Preset 1");
        var p2 = await TestHelpers.SeedEqPresetAsync(_db, _caller.UserId, "Preset 2");

        await _service.SetActivePresetAsync(p1.Id, _caller);
        await _service.SetActivePresetAsync(p2.Id, _caller);

        var prefs = _db.UserMusicPreferences.FirstOrDefault(p => p.UserId == _caller.UserId);
        Assert.AreEqual(p2.Id, prefs!.ActiveEqPresetId);
    }
}

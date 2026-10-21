using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Services;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class LocalEqStateTests
{
    private static double[] MakeBands(double first = 3.0)
    {
        var bands = new double[LocalEqState.BandCount];
        bands[0] = first;
        bands[^1] = -2.0;
        return bands;
    }

    private static EqPresetDto MakePreset(string name, Guid? id = null) => new()
    {
        Id = id ?? Guid.CreateVersion7(),
        Name = name,
        Bands = Enumerable.Range(0, LocalEqState.BandCount)
            .ToDictionary(i => $"band{i}", _ => 0.0)
    };

    // ─── Serialization ────────────────────────────────────────────────

    [TestMethod]
    public void ToJson_ThenTryParse_RoundTripsState()
    {
        var presetId = Guid.CreateVersion7();
        var state = new LocalEqState
        {
            PresetId = presetId,
            PresetName = "Rock",
            Bands = MakeBands()
        };

        var parsed = LocalEqState.TryParse(state.ToJson());

        Assert.IsNotNull(parsed);
        Assert.AreEqual(presetId, parsed.PresetId);
        Assert.AreEqual("Rock", parsed.PresetName);
        Assert.AreEqual(3.0, parsed.Bands[0]);
        Assert.AreEqual(-2.0, parsed.Bands[^1]);
    }

    [TestMethod]
    public void TryParse_NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(LocalEqState.TryParse(null));
        Assert.IsNull(LocalEqState.TryParse(string.Empty));
        Assert.IsNull(LocalEqState.TryParse("   "));
    }

    [TestMethod]
    public void TryParse_MalformedJson_ReturnsNull()
    {
        Assert.IsNull(LocalEqState.TryParse("{not json"));
    }

    [TestMethod]
    public void TryParse_WrongBandCount_ReturnsNull()
    {
        var json = $"{{\"bands\":[{string.Join(',', Enumerable.Repeat("0", LocalEqState.BandCount - 1))}]}}";

        Assert.IsNull(LocalEqState.TryParse(json));
    }

    [TestMethod]
    public void TryParse_MissingBands_ReturnsNull()
    {
        Assert.IsNull(LocalEqState.TryParse("{\"presetName\":\"Rock\"}"));
    }

    [TestMethod]
    public void IsValid_NonFiniteBand_ReturnsFalse()
    {
        var bands = MakeBands();
        bands[4] = double.NaN;

        var state = new LocalEqState { Bands = bands };

        Assert.IsFalse(state.IsValid);
    }

    // ─── Preset resolution ────────────────────────────────────────────

    [TestMethod]
    public void ResolvePresetId_MatchingId_ReturnsStoredId()
    {
        var presetId = Guid.CreateVersion7();
        var state = new LocalEqState
        {
            PresetId = presetId,
            PresetName = "Renamed",
            Bands = MakeBands()
        };

        var resolved = state.ResolvePresetId([MakePreset("Rock", presetId)]);

        Assert.AreEqual(presetId, resolved);
    }

    [TestMethod]
    public void ResolvePresetId_IdMissing_FallsBackToName()
    {
        var available = MakePreset("Rock", Guid.CreateVersion7());
        var state = new LocalEqState
        {
            PresetId = Guid.CreateVersion7(),
            PresetName = "Rock",
            Bands = MakeBands()
        };

        var resolved = state.ResolvePresetId([available]);

        Assert.AreEqual(available.Id, resolved);
    }

    [TestMethod]
    public void ResolvePresetId_NameMatchIsCaseInsensitive()
    {
        var available = MakePreset("Jazz", Guid.CreateVersion7());
        var state = new LocalEqState
        {
            PresetName = "jazz",
            Bands = MakeBands()
        };

        var resolved = state.ResolvePresetId([available]);

        Assert.AreEqual(available.Id, resolved);
    }

    [TestMethod]
    public void ResolvePresetId_NoMatch_ReturnsNull()
    {
        var state = new LocalEqState
        {
            PresetId = Guid.CreateVersion7(),
            PresetName = "Deleted",
            Bands = MakeBands()
        };

        var resolved = state.ResolvePresetId([MakePreset("Rock")]);

        Assert.IsNull(resolved);
    }
}

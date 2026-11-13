using DotNetCloud.Core.Server.ModuleLoading;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Tests for <see cref="ModuleCompatibility"/>.
/// </summary>
[TestClass]
public class ModuleCompatibilityTests
{
    private static ModuleManifestData CreateManifest(string? minCoreVersion) => new()
    {
        Id = "test.module",
        Name = "Test Module",
        Version = "1.0.0",
        MinCoreVersion = minCoreVersion
    };

    [TestMethod]
    [DataRow("0.7.0", 0, 7, 0)]
    [DataRow("1.2.3-beta.4", 1, 2, 3)]
    [DataRow("v2.3.4+build.9", 2, 3, 4)]
    [DataRow("1.0", 1, 0, 0)]
    public void TryParseVersion_ParseableVersion_ReturnsNumericTriple(
        string value, int major, int minor, int build)
    {
        var parsed = ModuleCompatibility.TryParseVersion(value, out var version);

        Assert.IsTrue(parsed);
        Assert.AreEqual(new Version(major, minor, build), version);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not-a-version")]
    public void TryParseVersion_UnparsableValue_ReturnsFalse(string? value)
    {
        var parsed = ModuleCompatibility.TryParseVersion(value, out _);

        Assert.IsFalse(parsed);
    }

    [TestMethod]
    public void CoreVersion_IsResolvedFromProductVersion()
    {
        Assert.IsTrue(ModuleCompatibility.CoreVersion >= new Version(0, 1, 0));
    }

    [TestMethod]
    public void TryGetIncompatibilityReason_NoMinCoreVersion_IsCompatible()
    {
        var incompatible = ModuleCompatibility.TryGetIncompatibilityReason(
            CreateManifest(minCoreVersion: null), out var reason);

        Assert.IsFalse(incompatible);
        Assert.IsNull(reason);
    }

    [TestMethod]
    public void TryGetIncompatibilityReason_OlderMinimum_IsCompatible()
    {
        var incompatible = ModuleCompatibility.TryGetIncompatibilityReason(
            CreateManifest("0.0.1"), out var reason);

        Assert.IsFalse(incompatible);
        Assert.IsNull(reason);
    }

    [TestMethod]
    public void TryGetIncompatibilityReason_NewerMinimum_IsIncompatibleWithReason()
    {
        var future = new Version(
            ModuleCompatibility.CoreVersion.Major + 1,
            ModuleCompatibility.CoreVersion.Minor,
            ModuleCompatibility.CoreVersion.Build);

        var incompatible = ModuleCompatibility.TryGetIncompatibilityReason(
            CreateManifest(future.ToString()), out var reason);

        Assert.IsTrue(incompatible);
        Assert.IsNotNull(reason);
        StringAssert.Contains(reason, "requires core");
    }

    [TestMethod]
    public void TryGetIncompatibilityReason_UnparsableMinimum_IsIncompatible()
    {
        var incompatible = ModuleCompatibility.TryGetIncompatibilityReason(
            CreateManifest("latest"), out var reason);

        Assert.IsTrue(incompatible);
        Assert.IsNotNull(reason);
        StringAssert.Contains(reason, "minCoreVersion");
    }
}

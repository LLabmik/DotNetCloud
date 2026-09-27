using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Options;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for <see cref="FileVersioningAdminSettings"/> — the overlay that lets /admin/files override
/// the configuration-bound version retention options.
/// </summary>
[TestClass]
public class FileVersioningAdminSettingsTests
{
    [TestMethod]
    public void Apply_PresentValues_OverrideConfigurationAndLeaveTheBaselineAlone()
    {
        var baseline = new VersionRetentionOptions
        {
            Enabled = false,
            MaxVersionCount = 50,
            RetentionDays = 0
        };

        var effective = FileVersioningAdminSettings.Copy(baseline);

        FileVersioningAdminSettings.Apply(effective, new Dictionary<string, string>
        {
            ["VersionRetention:Enabled"] = "true",
            ["VersionRetention:MaxNumber"] = "10",
            ["VersionRetention:MaxDays"] = "90"
        });

        Assert.IsTrue(effective.Enabled);
        Assert.AreEqual(10, effective.MaxVersionCount);
        Assert.AreEqual(90, effective.RetentionDays);

        // The shared configuration instance must never be mutated by an overlay.
        Assert.IsFalse(baseline.Enabled);
        Assert.AreEqual(50, baseline.MaxVersionCount);
        Assert.AreEqual(0, baseline.RetentionDays);
    }

    [TestMethod]
    public void Apply_AbsentBlankOrMalformedValues_KeepTheConfiguredValue()
    {
        var baseline = new VersionRetentionOptions
        {
            Enabled = true,
            MaxVersionCount = 50,
            RetentionDays = 7
        };

        var effective = FileVersioningAdminSettings.Copy(baseline);

        FileVersioningAdminSettings.Apply(effective, new Dictionary<string, string>
        {
            ["VersionRetention:Enabled"] = "  ",
            ["VersionRetention:MaxNumber"] = "not-a-number",
            ["VersionRetention:MaxDays"] = "-5"
        });

        Assert.IsTrue(effective.Enabled);
        Assert.AreEqual(50, effective.MaxVersionCount);
        Assert.AreEqual(7, effective.RetentionDays);
    }

    [TestMethod]
    public void Copy_IsIndependentOfTheSource()
    {
        var source = new VersionRetentionOptions { MaxVersionCount = 3, CleanupInterval = TimeSpan.FromHours(6) };

        var copy = FileVersioningAdminSettings.Copy(source);
        copy.MaxVersionCount = 99;
        copy.Enabled = false;

        Assert.AreEqual(3, source.MaxVersionCount);
        Assert.IsTrue(source.Enabled);
        Assert.AreEqual(TimeSpan.FromHours(6), copy.CleanupInterval);
    }

    [TestMethod]
    public void EffectiveMaxVersionCount_VersioningDisabled_IsAlwaysTheCurrentVersionOnly()
    {
        // Disabled means "keep the current version only", even when the count limit says unlimited.
        Assert.AreEqual(1, FileVersioningAdminSettings.EffectiveMaxVersionCount(
            new VersionRetentionOptions { Enabled = false, MaxVersionCount = 0 }));
        Assert.AreEqual(1, FileVersioningAdminSettings.EffectiveMaxVersionCount(
            new VersionRetentionOptions { Enabled = false, MaxVersionCount = 50 }));

        // Enabled passes the configured limit through, including 0 = unlimited.
        Assert.AreEqual(0, FileVersioningAdminSettings.EffectiveMaxVersionCount(
            new VersionRetentionOptions { Enabled = true, MaxVersionCount = 0 }));
        Assert.AreEqual(25, FileVersioningAdminSettings.EffectiveMaxVersionCount(
            new VersionRetentionOptions { Enabled = true, MaxVersionCount = 25 }));
    }

    [TestMethod]
    public void Apply_OverriddenKeys_IncludeEveryFieldTheAdminPageWrites()
    {
        // Guards against the page gaining a field the overlay silently ignores.
        string[] pageKeys =
        [
            "VersionRetention:Enabled",
            "VersionRetention:MaxNumber",
            "VersionRetention:MaxDays"
        ];

        foreach (var key in pageKeys)
        {
            CollectionAssert.Contains(
                FileVersioningAdminSettings.Keys.ToArray(), key,
                $"The overlay does not handle '{key}', so saving it on /admin/files would have no effect.");
        }
    }
}

using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Options;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for <see cref="CollaboraAdminSettings"/> — the overlay that lets /admin/collabora override
/// the configuration-bound Collabora options.
/// </summary>
[TestClass]
public class CollaboraAdminSettingsTests
{
    [TestMethod]
    public void Apply_PresentValues_OverrideConfigurationAndLeaveTheBaselineAlone()
    {
        var baseline = new CollaboraOptions
        {
            Enabled = false,
            ServerUrl = "https://config",
            TokenLifetimeMinutes = 480,
            SupportedMimeTypes = ["application/vnd.oasis.opendocument.text"]
        };

        var effective = CollaboraAdminSettings.Copy(baseline);

        CollaboraAdminSettings.Apply(effective, new Dictionary<string, string>
        {
            ["Collabora:Enabled"] = "true",
            ["Collabora:ServerUrl"] = "  https://admin  ",
            ["Collabora:TokenLifetimeMinutes"] = "90",
            ["Collabora:UseBuiltInCollabora"] = "true",
            ["Collabora:SupportedMimeTypes"] = "text/plain, application/pdf"
        });

        Assert.IsTrue(effective.Enabled);
        Assert.AreEqual("https://admin", effective.ServerUrl);
        Assert.AreEqual(90, effective.TokenLifetimeMinutes);
        Assert.IsTrue(effective.UseBuiltInCollabora);
        CollectionAssert.AreEqual(
            new[] { "text/plain", "application/pdf" }, effective.SupportedMimeTypes);

        // The shared configuration instance must never be mutated by an overlay.
        Assert.IsFalse(baseline.Enabled);
        Assert.AreEqual("https://config", baseline.ServerUrl);
        Assert.AreEqual(480, baseline.TokenLifetimeMinutes);
    }

    [TestMethod]
    public void Apply_AbsentBlankOrMalformedValues_KeepTheConfiguredValue()
    {
        var baseline = new CollaboraOptions
        {
            Enabled = true,
            ServerUrl = "https://config",
            TokenLifetimeMinutes = 480,
            SupportedMimeTypes = ["text/plain"]
        };

        var effective = CollaboraAdminSettings.Copy(baseline);

        CollaboraAdminSettings.Apply(effective, new Dictionary<string, string>
        {
            ["Collabora:ServerUrl"] = "   ",
            ["Collabora:TokenLifetimeMinutes"] = "not-a-number",
            ["Collabora:Enabled"] = "yes",
            ["Collabora:SupportedMimeTypes"] = ""
        });

        Assert.IsTrue(effective.Enabled);
        Assert.AreEqual("https://config", effective.ServerUrl);
        Assert.AreEqual(480, effective.TokenLifetimeMinutes);
        CollectionAssert.AreEqual(new[] { "text/plain" }, effective.SupportedMimeTypes);
    }

    [TestMethod]
    public void Copy_IsDeepEnoughThatAnEditDoesNotLeakToTheSource()
    {
        var source = new CollaboraOptions { SupportedMimeTypes = ["a"] };

        var copy = CollaboraAdminSettings.Copy(source);
        copy.SupportedMimeTypes.Add("b");

        Assert.AreEqual(1, source.SupportedMimeTypes.Count);
    }

    [TestMethod]
    public void Apply_OverriddenKeys_IncludeEveryFieldTheAdminPageWrites()
    {
        // Guards against the page gaining a field the overlay silently ignores.
        string[] pageKeys =
        [
            "Collabora:Enabled", "Collabora:ServerUrl", "Collabora:WopiBaseUrl",
            "Collabora:TokenSigningKey", "Collabora:TokenLifetimeMinutes",
            "Collabora:EnableProofKeyValidation", "Collabora:AutoSaveIntervalSeconds",
            "Collabora:MaxConcurrentSessions", "Collabora:SupportedMimeTypes",
            "Collabora:UseBuiltInCollabora", "Collabora:CollaboraInstallDirectory",
            "Collabora:CollaboraExecutablePath", "Collabora:CollaboraMaxRestartAttempts"
        ];

        foreach (var key in pageKeys)
        {
            CollectionAssert.Contains(
                CollaboraAdminSettings.Keys.ToArray(), key,
                $"The overlay does not handle '{key}', so saving it on /admin/collabora would have no effect.");
        }
    }
}

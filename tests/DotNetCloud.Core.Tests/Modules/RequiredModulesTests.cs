namespace DotNetCloud.Core.Tests.Modules;

using DotNetCloud.Core.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="RequiredModules"/> — the single source of truth for which modules are
/// architecturally required. Required status is derived from this list (never from a manifest id
/// claim), and it is what blocks a module from being disabled, uninstalled or replaced by a store
/// package, and what pins its <c>InstalledModules</c> trust tier to <c>Required</c>.
/// </summary>
[TestClass]
public class RequiredModulesTests
{
    /// <summary>
    /// The required set is enumerated exactly. Adding or removing a module here is an
    /// architectural decision, so an accidental edit must fail the build rather than silently
    /// making a core part disableable (or a disableable module permanent).
    /// </summary>
    [TestMethod]
    public void ModuleIds_ContainsTheArchitecturallyRequiredSet()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                "dotnetcloud.files",
                "dotnetcloud.chat",
                "dotnetcloud.contacts",
                "dotnetcloud.calendar",
                "dotnetcloud.notes",
                "dotnetcloud.about",
                "dotnetcloud.extraction"
            },
            RequiredModules.ModuleIds.ToArray());
    }

    /// <summary>
    /// The extraction worker is required: it is the parser half of the core search capability and
    /// must not be stoppable from the admin UI or the CLI.
    /// </summary>
    [TestMethod]
    [DataRow("dotnetcloud.extraction")]
    [DataRow("extraction")]
    [DataRow("DOTNETCLOUD.EXTRACTION")]
    [DataRow("Extraction")]
    public void IsRequired_ExtractionWorker_ReturnsTrue(string moduleId)
    {
        Assert.IsTrue(RequiredModules.IsRequired(moduleId));
    }

    /// <summary>
    /// Optional modules stay optional — promoting extraction must not widen the set by accident.
    /// </summary>
    [TestMethod]
    [DataRow("dotnetcloud.tracks")]
    [DataRow("dotnetcloud.music")]
    [DataRow("dotnetcloud.photos")]
    [DataRow("dotnetcloud.video")]
    [DataRow("dotnetcloud.bookmarks")]
    [DataRow("dotnetcloud.email")]
    [DataRow("dotnetcloud.ai")]
    [DataRow("dotnetcloud.not-a-real-module")]
    public void IsRequired_OptionalOrUnknownModule_ReturnsFalse(string moduleId)
    {
        Assert.IsFalse(RequiredModules.IsRequired(moduleId));
    }
}

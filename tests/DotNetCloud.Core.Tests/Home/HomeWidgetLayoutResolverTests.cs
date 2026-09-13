using DotNetCloud.Core.DTOs.Home;
using DotNetCloud.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Core.Tests.Home;

/// <summary>
/// Tests for resolving a user's widget order and visibility against the registered catalog.
/// </summary>
[TestClass]
public sealed class HomeWidgetLayoutResolverTests
{
    private static readonly HomeWidgetCatalogEntry[] Catalog =
    [
        new("dotnetcloud.files", 10),
        new("dotnetcloud.chat", 20),
        new("dotnetcloud.contacts", 30),
        new("dotnetcloud.calendar", 40),
    ];

    private static HomeWidgetPreferences Preferences(params HomeWidgetPreferenceItem[] items)
        => new() { Items = [.. items] };

    private static HomeWidgetPreferenceItem Slot(string moduleId, bool visible = true)
        => new() { ModuleId = moduleId, Visible = visible };

    [TestMethod]
    public void Resolve_NullPreferences_ReturnsCatalogOrderAllVisible()
    {
        var layout = HomeWidgetLayoutResolver.Resolve(Catalog, null);

        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.files", "dotnetcloud.chat", "dotnetcloud.contacts", "dotnetcloud.calendar" },
            layout.Select(x => x.ModuleId).ToArray());
        Assert.IsTrue(layout.All(x => x.Visible));
    }

    [TestMethod]
    public void Resolve_PersistedOrder_WinsOverCatalogOrder()
    {
        var prefs = Preferences(Slot("dotnetcloud.calendar"), Slot("dotnetcloud.files"));

        var layout = HomeWidgetLayoutResolver.Resolve(Catalog, prefs);

        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.calendar", "dotnetcloud.files", "dotnetcloud.chat", "dotnetcloud.contacts" },
            layout.Select(x => x.ModuleId).ToArray());
    }

    [TestMethod]
    public void Resolve_HiddenWidget_IsReturnedWithVisibleFalseAndKeepsItsSlot()
    {
        var prefs = Preferences(Slot("dotnetcloud.chat", visible: false), Slot("dotnetcloud.files"));

        var layout = HomeWidgetLayoutResolver.Resolve(Catalog, prefs);

        // chat is first in the persisted order, so it must still report position 0 while hidden.
        Assert.AreEqual("dotnetcloud.chat", layout[0].ModuleId);
        Assert.IsFalse(layout[0].Visible);
        Assert.AreEqual(1, layout.Count(x => !x.Visible));
    }

    [TestMethod]
    public void Resolve_ModuleAbsentFromPreferences_AppendsAfterArrangedOnes()
    {
        var prefs = Preferences(Slot("dotnetcloud.chat"), Slot("dotnetcloud.files"));

        var layout = HomeWidgetLayoutResolver.Resolve(Catalog, prefs);

        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.chat", "dotnetcloud.files", "dotnetcloud.contacts", "dotnetcloud.calendar" },
            layout.Select(x => x.ModuleId).ToArray());
    }

    [TestMethod]
    public void Resolve_PersistedModuleNotInCatalog_IsIgnored()
    {
        var prefs = Preferences(Slot("dotnetcloud.uninstalled"), Slot("dotnetcloud.files"));

        var layout = HomeWidgetLayoutResolver.Resolve(Catalog, prefs);

        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.files", "dotnetcloud.chat", "dotnetcloud.contacts", "dotnetcloud.calendar" },
            layout.Select(x => x.ModuleId).ToArray());
    }

    [TestMethod]
    public void Resolve_ModuleIdCaseDiffers_StillMatches()
    {
        var prefs = Preferences(Slot("DotNetCloud.FILES"));

        var layout = HomeWidgetLayoutResolver.Resolve(Catalog, prefs);

        Assert.AreEqual("dotnetcloud.files", layout[0].ModuleId);
    }

    [TestMethod]
    public void Resolve_BlankCatalogEntries_AreSkipped()
    {
        HomeWidgetCatalogEntry[] catalog =
        [
            new("dotnetcloud.files", 10),
            new("   ", 15),
            new("dotnetcloud.chat", 20),
        ];

        var layout = HomeWidgetLayoutResolver.Resolve(catalog, null);

        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.files", "dotnetcloud.chat" },
            layout.Select(x => x.ModuleId).ToArray());
    }

    [TestMethod]
    public void Resolve_EmptyCatalog_ReturnsEmpty()
    {
        var layout = HomeWidgetLayoutResolver.Resolve([], Preferences());

        Assert.AreEqual(0, layout.Count);
    }

    [TestMethod]
    public void BuildPreferences_ResolvedLayout_PersistsOrderVisibilityAndStyle()
    {
        var layout = new HomeWidgetLayoutItem[]
        {
            new("dotnetcloud.calendar", true),
            new("dotnetcloud.files", false),
        };

        var prefs = HomeWidgetLayoutResolver.BuildPreferences(HomeWidgetStyles.HardCopy, layout);

        Assert.AreEqual(HomeWidgetStyles.HardCopy, prefs.Style);
        Assert.AreEqual(HomeWidgetPreferences.CurrentVersion, prefs.Version);
        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.calendar", "dotnetcloud.files" },
            prefs.Items.Select(x => x.ModuleId).ToArray());
        Assert.IsFalse(prefs.Items[1].Visible);
    }

    [TestMethod]
    public void BuildPreferences_UninstalledSlotInExisting_IsRetainedAfterLayout()
    {
        var existing = Preferences(Slot("dotnetcloud.uninstalled", visible: false));
        var layout = new HomeWidgetLayoutItem[] { new("dotnetcloud.files", true) };

        var prefs = HomeWidgetLayoutResolver.BuildPreferences(
            HomeWidgetStyles.ArtDepartment,
            layout,
            existing);

        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.files", "dotnetcloud.uninstalled" },
            prefs.Items.Select(x => x.ModuleId).ToArray());
        Assert.IsFalse(prefs.Items[1].Visible);
    }

    [TestMethod]
    public void BuildPreferences_UnknownStyleToken_NormalizesToDefault()
    {
        var prefs = HomeWidgetLayoutResolver.BuildPreferences(
            "neon-chaos",
            [new HomeWidgetLayoutItem("dotnetcloud.files", true)]);

        Assert.AreEqual(HomeWidgetStyles.DefaultToken, prefs.Style);
    }

    [TestMethod]
    public void BuildPreferences_DuplicateLayoutEntries_AreDeduped()
    {
        var layout = new HomeWidgetLayoutItem[]
        {
            new("dotnetcloud.files", true),
            new("DotNetCloud.FILES", false),
        };

        var prefs = HomeWidgetLayoutResolver.BuildPreferences(HomeWidgetStyles.ArtDepartment, layout);

        Assert.AreEqual(1, prefs.Items.Count);
        Assert.IsTrue(prefs.Items[0].Visible);
    }

    [TestMethod]
    public void ResolveThenBuild_RoundTrip_IsStable()
    {
        var prefs = HomeWidgetLayoutResolver.BuildPreferences(
            HomeWidgetStyles.HardCopy,
            HomeWidgetLayoutResolver.Resolve(Catalog, null));

        var relayout = HomeWidgetLayoutResolver.Resolve(Catalog, prefs);
        var rebuilt = HomeWidgetLayoutResolver.BuildPreferences(HomeWidgetStyles.HardCopy, relayout, prefs);

        Assert.AreEqual(HomeWidgetStyles.HardCopy, rebuilt.Style);
        Assert.AreEqual(prefs.Items.Count, rebuilt.Items.Count);
        CollectionAssert.AreEqual(
            prefs.Items.Select(x => x.ModuleId).ToArray(),
            rebuilt.Items.Select(x => x.ModuleId).ToArray());
    }
}

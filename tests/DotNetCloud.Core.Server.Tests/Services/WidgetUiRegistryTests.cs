using DotNetCloud.UI.Web.Services;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="WidgetUiRegistry"/>.
/// </summary>
[TestClass]
public class WidgetUiRegistryTests
{
    [TestMethod]
    public void RegisterWidget_ValidWidget_AddsWidgetWithProperties()
    {
        var registry = new WidgetUiRegistry();

        registry.RegisterWidget("dotnetcloud.files", "Files", "folder", "/apps/files", typeof(string), sortOrder: 10);

        Assert.AreEqual(1, registry.Widgets.Count);
        var widget = registry.Widgets[0];
        Assert.AreEqual("dotnetcloud.files", widget.ModuleId);
        Assert.AreEqual("Files", widget.Title);
        Assert.AreEqual("folder", widget.Icon);
        Assert.AreEqual("/apps/files", widget.Href);
        Assert.AreEqual(typeof(string), widget.ComponentType);
        Assert.AreEqual(10, widget.SortOrder);
    }

    [TestMethod]
    public void RegisterWidget_SameModuleIdTwice_ReplacesExistingEntry()
    {
        var registry = new WidgetUiRegistry();

        registry.RegisterWidget("dotnetcloud.files", "Files", "folder", "/apps/files", typeof(string), 10);
        registry.RegisterWidget("dotnetcloud.files", "Files Updated", "folder", "/apps/files2", typeof(string), 20);

        Assert.AreEqual(1, registry.Widgets.Count);
        Assert.AreEqual("Files Updated", registry.Widgets[0].Title);
        Assert.AreEqual(20, registry.Widgets[0].SortOrder);
    }

    [TestMethod]
    public void Widgets_SortedBySortOrderAscending_ReturnsOrderedWidgets()
    {
        var registry = new WidgetUiRegistry();

        registry.RegisterWidget("module.c", "C", "c", "/c", typeof(string), 30);
        registry.RegisterWidget("module.a", "A", "a", "/a", typeof(string), 10);
        registry.RegisterWidget("module.b", "B", "b", "/b", typeof(string), 20);

        Assert.AreEqual(3, registry.Widgets.Count);
        CollectionAssert.AreEqual(
            new[] { "module.a", "module.b", "module.c" },
            registry.Widgets.Select(w => w.ModuleId).ToArray());
    }

    [TestMethod]
    public void UnregisterModule_RegisteredModule_RemovesItsWidget()
    {
        var registry = new WidgetUiRegistry();
        registry.RegisterWidget("module.a", "A", "a", "/a", typeof(string), 10);
        registry.RegisterWidget("module.b", "B", "b", "/b", typeof(string), 20);

        registry.UnregisterModule("module.a");

        Assert.AreEqual(1, registry.Widgets.Count);
        Assert.AreEqual("module.b", registry.Widgets[0].ModuleId);
    }

    [TestMethod]
    public void UnregisterModule_UnknownModule_IsNoOp()
    {
        var registry = new WidgetUiRegistry();
        registry.RegisterWidget("module.a", "A", "a", "/a", typeof(string), 10);

        registry.UnregisterModule("does-not-exist");

        Assert.AreEqual(1, registry.Widgets.Count);
    }

    [TestMethod]
    public void RegisterWidget_NullComponentType_ThrowsArgumentNull()
    {
        var registry = new WidgetUiRegistry();

        Assert.ThrowsExactly<ArgumentNullException>(
            () => registry.RegisterWidget("module.a", "A", "a", "/a", null!, 10));
    }

    [TestMethod]
    public void RegisterWidget_RaisesOnChange()
    {
        var registry = new WidgetUiRegistry();
        var changed = 0;
        registry.OnChange += () => changed++;

        registry.RegisterWidget("module.a", "A", "a", "/a", typeof(string), 10);
        registry.UnregisterModule("module.a");

        Assert.AreEqual(2, changed);
    }
}

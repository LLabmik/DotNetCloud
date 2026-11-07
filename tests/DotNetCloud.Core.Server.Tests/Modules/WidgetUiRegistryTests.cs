using DotNetCloud.UI.Web.Services;

namespace DotNetCloud.Core.Server.Tests.Modules;

/// <summary>
/// Tests for the widget registry's iframe descriptor, the seam that lets a module render its Home
/// widget from its own process instead of a compiled Blazor component.
/// </summary>
[TestClass]
public class WidgetUiRegistryTests
{
    [TestMethod]
    public void RegisterIframeWidget_ProducesAnIframeDescriptor()
    {
        var registry = new WidgetUiRegistry();

        registry.RegisterIframeWidget(
            "vendor.receipts", "Receipts", "receipt_long", "/modules/vendor.receipts", "/widget");

        var widget = registry.Widgets.Single();
        Assert.IsNull(widget.ComponentType);
        Assert.IsTrue(widget.IsIframe);
        Assert.AreEqual("/widget", widget.IframePath);
        Assert.AreEqual("receipt_long", widget.Icon);
    }

    [TestMethod]
    public void RegisterIframeWidget_NormalizesThePath()
    {
        var registry = new WidgetUiRegistry();

        registry.RegisterIframeWidget(
            "vendor.receipts", "Receipts", "receipt_long", "/modules/vendor.receipts", "widget");

        Assert.AreEqual("/widget", registry.Widgets.Single().IframePath);
    }

    [TestMethod]
    public void RegisterIframeWidget_UnsafePath_FallsBackToRoot()
    {
        var registry = new WidgetUiRegistry();

        registry.RegisterIframeWidget(
            "vendor.receipts", "Receipts", "receipt_long", "/modules/vendor.receipts", "//evil.example.org/w");

        Assert.AreEqual("/", registry.Widgets.Single().IframePath);
    }

    [TestMethod]
    public void RegisterWidget_ForTheSameModule_ReplacesTheIframeWidget()
    {
        var registry = new WidgetUiRegistry();
        registry.RegisterIframeWidget(
            "vendor.receipts", "Receipts", "receipt_long", "/modules/vendor.receipts", "/widget");

        registry.RegisterWidget(
            "vendor.receipts", "Receipts", "receipt_long", "/modules/vendor.receipts", typeof(object));

        var widget = registry.Widgets.Single();
        Assert.IsNotNull(widget.ComponentType);
        Assert.IsFalse(widget.IsIframe);
    }

    [TestMethod]
    public void RegisterWidget_RejectsANullComponentType()
    {
        var registry = new WidgetUiRegistry();

        Assert.ThrowsExactly<ArgumentNullException>(
            () => registry.RegisterWidget("vendor.receipts", "R", "receipt_long", "/x", null!));
    }

    [TestMethod]
    public void UnregisterModule_RemovesTheIframeWidget()
    {
        var registry = new WidgetUiRegistry();
        registry.RegisterIframeWidget(
            "vendor.receipts", "Receipts", "receipt_long", "/modules/vendor.receipts", "/widget");

        registry.UnregisterModule("vendor.receipts");

        Assert.AreEqual(0, registry.Widgets.Count);
    }
}

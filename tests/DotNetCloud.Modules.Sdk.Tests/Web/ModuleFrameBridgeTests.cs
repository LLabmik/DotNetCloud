using System.Runtime.CompilerServices;
using DotNetCloud.Modules.Sdk.Web;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace DotNetCloud.Modules.Sdk.Tests.Web;

/// <summary>
/// Covers the C# side of the shared frame bridge.
/// </summary>
/// <remarks>
/// The consent call is pinned as a single array argument on purpose: a <c>params string[]</c> forwarded
/// straight to JS interop spreads into N arguments, the SDK bridge sees a bare string, and the consent
/// prompt vanishes without an error.
/// </remarks>
[TestClass]
public class ModuleFrameBridgeTests
{
    [TestMethod]
    public async Task RequestConsentAsync_TwoScopes_PassesOneArrayArgument()
    {
        var (js, captured) = CreateVoidRuntime();

        var bridge = new ModuleFrameBridge(js.Object);
        await bridge.RequestConsentAsync("profile", "files:read");

        Assert.IsNotNull(captured.Value, "the scope array must reach JavaScript");
        Assert.AreEqual(1, captured.Value!.Length, "a params array must arrive as one argument, not N");

        var scopes = captured.Value[0] as string[];
        Assert.IsNotNull(scopes, "the single argument must be a string[]");
        CollectionAssert.AreEqual(new[] { "profile", "files:read" }, scopes);
    }

    [TestMethod]
    public async Task RequestConsentAsync_OneScope_StillPassesAnArray()
    {
        var (js, captured) = CreateVoidRuntime();

        await new ModuleFrameBridge(js.Object).RequestConsentAsync("files:read");

        var scopes = captured.Value![0] as string[];
        Assert.IsNotNull(scopes);
        CollectionAssert.AreEqual(new[] { "files:read" }, scopes);
    }

    [TestMethod]
    public async Task OnThemeChanged_Dark_UpdatesTheThemeAndRaisesChanged()
    {
        var bridge = new ModuleFrameBridge(new Mock<IJSRuntime>().Object);
        var raised = 0;
        bridge.Changed += () => raised++;

        bridge.OnThemeChanged("dark", "en-GB");

        Assert.AreEqual("dark", bridge.Theme);
        Assert.AreEqual("en-GB", bridge.Locale);
        Assert.AreEqual(1, raised);
        await Task.CompletedTask;
    }

    [TestMethod]
    public void OnThemeChanged_Blank_UsesTheLightDefault()
    {
        var bridge = new ModuleFrameBridge(new Mock<IJSRuntime>().Object);

        bridge.OnThemeChanged("", "");

        Assert.AreEqual("light", bridge.Theme);
        Assert.AreEqual(string.Empty, bridge.Locale);
    }

    [TestMethod]
    public async Task InitializeAsync_CalledTwice_SubscribesOnce()
    {
        var calls = new List<string>();
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<IJSVoidResult>(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((identifier, _) => calls.Add(identifier))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        var bridge = new ModuleFrameBridge(js.Object);
        await bridge.InitializeAsync();
        await bridge.InitializeAsync();

        CollectionAssert.AreEqual(new[] { "dncModuleFrame.onTheme" }, calls);
    }

    [TestMethod]
    public async Task MediaUrlAsync_ReturnsTheUrlTheBridgeProduced()
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<string>("dncModuleFrame.mediaUrl", It.IsAny<object?[]>()))
            .Returns(ValueTask.FromResult("http://module.local/photo.jpg?dnc-frame-ticket=abc"));

        var url = await new ModuleFrameBridge(js.Object).MediaUrlAsync("photo.jpg");

        Assert.AreEqual("http://module.local/photo.jpg?dnc-frame-ticket=abc", url);
    }

    private static (Mock<IJSRuntime> Js, StrongBox<object?[]?> Captured) CreateVoidRuntime()
    {
        var captured = new StrongBox<object?[]?>(null);
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<IJSVoidResult>(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((_, args) => captured.Value = args)
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        return (js, captured);
    }
}

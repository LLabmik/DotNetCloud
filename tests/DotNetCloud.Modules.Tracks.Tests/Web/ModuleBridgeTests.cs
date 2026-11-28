using DotNetCloud.Modules.Tracks.Web.Services;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.Tests.Web;

/// <summary>
/// Guards the frame bridge's JavaScript interop arguments.
/// </summary>
/// <remarks>
/// <c>InvokeVoidAsync(name, someArray)</c> treats an array as the <i>argument list</i> and spreads it,
/// so a single-scope consent request reached JavaScript as a bare string. The SDK bridge only forwards
/// an array (<c>Array.isArray</c>), so the prompt never appeared and the failure was silent — a live
/// click-through found it, nothing in the C# compiler could. These tests pin the argument shape.
/// </remarks>
[TestClass]
public class ModuleBridgeTests
{
    [TestMethod]
    public async Task RequestConsentAsync_TwoScopes_ArriveAsOneArrayArgument()
    {
        var js = new RecordingJSRuntime();
        var bridge = new ModuleBridge(js);

        await bridge.RequestConsentAsync(["files:read", "notifications:send"]);

        var call = js.Single();
        Assert.AreEqual("tracksBridge.requestConsent", call.Identifier);
        Assert.IsNotNull(call.Args);
        Assert.AreEqual(1, call.Args!.Length, "the scopes must be one array argument, not spread across parameters");

        var scopes = call.Args[0] as string[];
        Assert.IsNotNull(scopes, "the argument must be a string[] so the bridge's Array.isArray check passes");
        CollectionAssert.AreEqual(new[] { "files:read", "notifications:send" }, scopes);
    }

    [TestMethod]
    public async Task RequestConsentAsync_OneScope_StillArrivesAsAnArray()
    {
        var js = new RecordingJSRuntime();
        var bridge = new ModuleBridge(js);

        await bridge.RequestConsentAsync("profile");

        var call = js.Single();
        Assert.AreEqual(1, call.Args!.Length);
        var scopes = call.Args[0] as string[];
        Assert.IsNotNull(scopes, "a single scope must not degrade to a bare string");
        CollectionAssert.AreEqual(new[] { "profile" }, scopes);
    }

    [TestMethod]
    public async Task NotifyAsync_SendsTheMessageAsItsOwnArgument()
    {
        var js = new RecordingJSRuntime();
        var bridge = new ModuleBridge(js);

        await bridge.NotifyAsync("Saved");

        var call = js.Single();
        Assert.AreEqual("tracksBridge.notify", call.Identifier);
        Assert.AreEqual(1, call.Args!.Length);
        Assert.AreEqual("Saved", call.Args[0]);
    }

    [TestMethod]
    public async Task InitializeAsync_SubscribesOnceToTheShellTheme()
    {
        var js = new RecordingJSRuntime();
        var bridge = new ModuleBridge(js);

        await bridge.InitializeAsync();
        await bridge.InitializeAsync();

        Assert.AreEqual(1, js.Calls.Count, "subscribing twice would register two theme handlers");
        Assert.AreEqual("tracksBridge.onTheme", js.Calls[0].Identifier);
    }

    private sealed class RecordingJSRuntime : IJSRuntime
    {
        public List<(string Identifier, object?[]? Args)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add((identifier, args));
            return ValueTask.FromResult(default(TValue)!);
        }

        public (string Identifier, object?[]? Args) Single()
        {
            Assert.AreEqual(1, Calls.Count, "expected exactly one interop call");
            return Calls[0];
        }
    }
}

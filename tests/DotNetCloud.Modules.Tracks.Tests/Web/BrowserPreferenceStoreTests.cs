using DotNetCloud.Modules.Tracks.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Tracks.Tests.Web;

/// <summary>
/// Preference storage in a sandboxed frame: <c>localStorage</c> throws, so the store must keep working
/// from memory and must stop paying for a failing JS call.
/// </summary>
[TestClass]
public sealed class BrowserPreferenceStoreTests
{
    [TestMethod]
    public async Task SetAsync_BrowserStorageUnavailable_KeepsValueInMemory()
    {
        var store = new BrowserPreferenceStore(new UnavailableStorageJsRuntime(), NullLogger<BrowserPreferenceStore>.Instance);

        await store.SetAsync("selectedOrg", "org-1");

        Assert.AreEqual("org-1", await store.GetAsync("selectedOrg"));
    }

    [TestMethod]
    public async Task GetAsync_UnavailableStorageAndUnknownKey_ReturnsNull()
    {
        var store = new BrowserPreferenceStore(new UnavailableStorageJsRuntime(), NullLogger<BrowserPreferenceStore>.Instance);

        Assert.IsNull(await store.GetAsync("missing"));
    }

    [TestMethod]
    public async Task SetAsync_AvailableStorage_WritesThroughToLocalStorage()
    {
        var js = new RecordingJsRuntime();
        var store = new BrowserPreferenceStore(js, NullLogger<BrowserPreferenceStore>.Instance);

        await store.SetAsync("selectedOrg", "org-2");

        var call = js.Calls.Single();
        Assert.AreEqual("localStorage.setItem", call.Identifier);
        Assert.AreEqual("selectedOrg", call.Args[0]);
        Assert.AreEqual("org-2", call.Args[1]);
    }

    [TestMethod]
    public async Task GetAsync_AvailableStorage_ReadsFromLocalStorage()
    {
        var js = new RecordingJsRuntime((identifier, _) => identifier == "localStorage.getItem" ? "org-3" : null);
        var store = new BrowserPreferenceStore(js, NullLogger<BrowserPreferenceStore>.Instance);

        var value = await store.GetAsync("selectedOrg");

        Assert.AreEqual("org-3", value);
        Assert.AreEqual("localStorage.getItem", js.Calls.Single().Identifier);
    }

    [TestMethod]
    public async Task GetAsync_AfterFailedWrite_DoesNotCallStorageAgain()
    {
        var js = new UnavailableStorageJsRuntime();
        var store = new BrowserPreferenceStore(js, NullLogger<BrowserPreferenceStore>.Instance);

        await store.SetAsync("selectedOrg", "org-1");
        await store.RemoveAsync("selectedOrg");

        // Both calls did not throw even though the runtime fails every invocation, and the value is gone.
        Assert.IsNull(await store.GetAsync("selectedOrg"));
    }
}

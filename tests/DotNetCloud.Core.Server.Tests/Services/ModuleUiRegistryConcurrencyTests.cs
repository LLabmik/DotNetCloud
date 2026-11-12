using System.Collections.Concurrent;
using DotNetCloud.UI.Web.Services;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Concurrency guards for <see cref="ModuleUiRegistry"/>.
/// </summary>
/// <remarks>
/// Two hosted services write this singleton at the same time:
/// <c>ModuleUiRegistrationHostedService</c> (first-party Blazor pages) and
/// <c>ModuleAppRegistrationHostedService</c> (manifest-declared iframe apps). In production the
/// second one crashed on startup with "Collection was modified; enumeration operation may not
/// execute" inside <c>UnregisterModule</c>, and the fault ended the service — every iframe module
/// (AI, Bookmarks, Email, Music) silently lost its sidebar entry until the next restart.
/// </remarks>
[TestClass]
public class ModuleUiRegistryConcurrencyTests
{
    private static readonly Type PageType = typeof(FakePage);

    /// <summary>
    /// A subscriber that throws must not abort the mutation or starve the other subscribers.
    /// </summary>
    /// <remarks>
    /// Live failure this prevents: <c>NavMenu</c> subscribed <c>StateHasChanged</c> directly, so the
    /// change event delivered from a background reconciler threw "The current thread is not
    /// associated with the Dispatcher" out of <c>UnregisterModule</c>. The registry had already
    /// removed the app, the caller's remaining work never ran, and the module lost its UI until the
    /// next restart.
    /// </remarks>
    [TestMethod]
    public void RegisterApp_WhenASubscriberThrows_StillStoresTheAppAndNotifiesLaterSubscribers()
    {
        var registry = new ModuleUiRegistry();
        var laterSubscriberCalled = false;

        registry.OnChange += () => throw new InvalidOperationException("The current thread is not associated with the Dispatcher.");
        registry.OnChange += () => laterSubscriberCalled = true;

        var app = new ModuleAppDescriptor("vendor.receipts", "Receipts", "/apps/vendor.receipts", "/");

        registry.RegisterApp(app);

        Assert.IsNotNull(registry.GetApp("vendor.receipts"));
        Assert.IsTrue(laterSubscriberCalled, "a throwing subscriber must not prevent the next one from being notified");
    }

    /// <summary>
    /// Unregistering with a throwing subscriber must still remove the module.
    /// </summary>
    [TestMethod]
    public void UnregisterModule_WhenASubscriberThrows_StillRemovesTheModule()
    {
        var registry = new ModuleUiRegistry();
        registry.RegisterApp(new ModuleAppDescriptor("vendor.receipts", "Receipts", "/apps/vendor.receipts", "/"));

        registry.OnChange += () => throw new InvalidOperationException("The current thread is not associated with the Dispatcher.");

        registry.UnregisterModule("vendor.receipts");

        Assert.IsNull(registry.GetApp("vendor.receipts"));
    }

    /// <summary>
    /// Unregistering a module while another thread registers pages must never throw, and must leave
    /// the registry usable.
    /// </summary>
    [TestMethod]
    public void UnregisterModule_WhilePagesAreRegistered_DoesNotThrow()
    {
        var registry = new ModuleUiRegistry();
        var errors = new ConcurrentQueue<Exception>();

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var writer = Task.Run(() =>
        {
            var index = 0;
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    registry.RegisterPage($"vendor.receipts.page{index++ % 8}", PageType);
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }
        });

        var unregisterer = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    registry.UnregisterModule("vendor.receipts");
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }
        });

        Task.WaitAll(writer, unregisterer);

        Assert.AreEqual(
            0,
            errors.Count,
            errors.TryPeek(out var first) ? first.ToString() : "no error captured");

        // The registry still accepts writes after the storm.
        registry.RegisterNavItem("vendor.receipts", "Receipts", "/modules/vendor.receipts", "receipt_long");
        Assert.AreEqual(1, registry.NavItems.Count);
    }

    /// <summary>
    /// Readers must observe a stable snapshot: a render in progress cannot be upset by a background
    /// registration.
    /// </summary>
    [TestMethod]
    public void NavItems_AfterRegistration_ReturnsSnapshot()
    {
        var registry = new ModuleUiRegistry();
        registry.RegisterNavItem("a.module", "A", "/apps/a", "folder", 100);

        var snapshot = registry.NavItems;
        registry.RegisterNavItem("b.module", "B", "/apps/b", "folder", 200);

        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual(2, registry.NavItems.Count);
    }

    /// <summary>
    /// Unregistering removes the module's navigation item, app descriptor and page routes.
    /// </summary>
    [TestMethod]
    public void UnregisterModule_RemovesNavigationAppAndPages()
    {
        var registry = new ModuleUiRegistry();
        registry.RegisterNavItem("vendor.receipts", "Receipts", "/modules/vendor.receipts", "receipt_long");
        registry.RegisterApp(new ModuleAppDescriptor("vendor.receipts", "Receipts", "/apps/vendor.receipts", "/"));
        registry.RegisterPage("vendor.receipts.page", PageType);

        registry.UnregisterModule("vendor.receipts");

        Assert.AreEqual(0, registry.NavItems.Count);
        Assert.IsNull(registry.GetApp("vendor.receipts"));
        Assert.IsNull(registry.GetPage("vendor.receipts.page"));
    }

    private sealed class FakePage;
}

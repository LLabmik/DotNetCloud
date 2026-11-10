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

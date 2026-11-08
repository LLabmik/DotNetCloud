using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Initialization;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.UI.Shared.Services;
using DotNetCloud.UI.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Initialization;

/// <summary>
/// Tests for <see cref="ModuleAppRegistrationHostedService"/>: a module that declares
/// <c>ui.type = "iframe"</c> in its manifest must reach the shell's navigation and app registry,
/// and must disappear again when it is disabled.
/// </summary>
[TestClass]
public class ModuleAppRegistrationHostedServiceTests
{
    private const string IframeModule = "vendor.receipts";

    private string _dbName = null!;
    private ModuleManifestRegistry _manifests = null!;
    private ModuleUiRegistry _moduleRegistry = null!;
    private WidgetUiRegistry _widgetRegistry = null!;
    private ServiceProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        _dbName = $"ModuleAppRegistration_{Guid.NewGuid():N}";
        _manifests = new ModuleManifestRegistry();
        _moduleRegistry = new ModuleUiRegistry();
        _widgetRegistry = new WidgetUiRegistry();

        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        services.AddSingleton(_ => new CoreDbContext(options, new PostgreSqlNamingStrategy()));
        _provider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup() => _provider.Dispose();

    [TestMethod]
    public async Task RefreshAsync_EnabledIframeModule_RegistersAppAndNavigation()
    {
        SeedInstalled(IframeModule, "Enabled");
        DeclareUi(IframeModule, navItems: [("open", "Receipts", "receipt_long", "/")]);

        await CreateService().RefreshAsync(CancellationToken.None);

        var app = _moduleRegistry.GetApp(IframeModule);
        Assert.IsNotNull(app);
        Assert.AreEqual($"/apps/{IframeModule}", app.BasePath);
        Assert.AreEqual("/", app.EntryPath);
        Assert.AreEqual($"/apps/{IframeModule}/", app.EntryUrl);

        var nav = _moduleRegistry.NavItems.Where(n => n.ModuleId == IframeModule).ToList();
        Assert.AreEqual(1, nav.Count);
        Assert.AreEqual("/modules/vendor.receipts", nav[0].Href);
        Assert.AreEqual("Receipts", nav[0].Label);
        Assert.AreEqual("receipt_long", nav[0].Icon);
    }

    [TestMethod]
    public async Task RefreshAsync_NavPath_IsModuleRelativeUnderTheShellPath()
    {
        SeedInstalled(IframeModule, "Enabled");
        DeclareUi(IframeModule, navItems: [("settings", "Settings", "settings", "/settings")]);

        await CreateService().RefreshAsync(CancellationToken.None);

        Assert.AreEqual($"/modules/{IframeModule}/settings", _moduleRegistry.NavItems.Single().Href);
    }

    [TestMethod]
    public async Task RefreshAsync_DeclaredIcon_IsResolvedThroughModuleIconProvider()
    {
        SeedInstalled(IframeModule, "Enabled");
        DeclareUi(IframeModule, navItems: [("open", "Receipts", Icon: null, Path: "/")]);

        await CreateService().RefreshAsync(CancellationToken.None);

        // No declared icon -> the module's canonical Material icon, never the module id itself.
        Assert.AreEqual(ModuleIconProvider.GetIcon(IframeModule, "widgets"), _moduleRegistry.NavItems.Single().Icon);
    }

    [TestMethod]
    public async Task RefreshAsync_EmojiIconDeclaration_FallsBackToTheCanonicalIcon()
    {
        SeedInstalled(IframeModule, "Enabled");
        DeclareUi(IframeModule, navItems: [("open", "Receipts", Icon: "\U0001F4C1", Path: "/")]);

        await CreateService().RefreshAsync(CancellationToken.None);

        Assert.AreEqual(ModuleIconProvider.GetIcon(IframeModule, "widgets"), _moduleRegistry.NavItems.Single().Icon);
    }

    [TestMethod]
    public async Task RefreshAsync_ModuleWithNoNavItems_GetsADefaultEntry()
    {
        SeedInstalled(IframeModule, "Enabled");
        DeclareUi(IframeModule, navItems: []);

        await CreateService().RefreshAsync(CancellationToken.None);

        Assert.AreEqual($"/modules/{IframeModule}", _moduleRegistry.NavItems.Single().Href);
    }

    [TestMethod]
    public async Task RefreshAsync_RequiredModuleWithUi_IsIgnored()
    {
        const string requiredModule = "dotnetcloud.files";
        SeedInstalled(requiredModule, "Enabled");
        DeclareUi(requiredModule, navItems: [("open", "Files", "folder", "/")]);

        await CreateService().RefreshAsync(CancellationToken.None);

        Assert.IsNull(_moduleRegistry.GetApp(requiredModule));
        Assert.AreEqual(0, _moduleRegistry.NavItems.Count);
    }

    [TestMethod]
    public async Task RefreshAsync_DisabledModule_IsNotRegistered()
    {
        SeedInstalled(IframeModule, "Disabled");
        DeclareUi(IframeModule, navItems: [("open", "Receipts", "receipt_long", "/")]);

        await CreateService().RefreshAsync(CancellationToken.None);

        Assert.IsNull(_moduleRegistry.GetApp(IframeModule));
        Assert.AreEqual(0, _moduleRegistry.NavItems.Count);
    }

    [TestMethod]
    public async Task RefreshAsync_ModuleWithWidget_RegistersAnIframeWidgetCard()
    {
        SeedInstalled(IframeModule, "Enabled");
        DeclareUi(
            IframeModule,
            navItems: [("open", "Receipts", "receipt_long", "/")],
            widgetPath: "/widget/index.html");

        await CreateService().RefreshAsync(CancellationToken.None);

        var widget = _widgetRegistry.Widgets.Single(w => w.ModuleId == IframeModule);
        Assert.IsTrue(widget.IsIframe, "a manifest widget is a sandboxed frame, not a Blazor component");
        Assert.AreEqual("/widget/index.html", widget.IframePath);
        Assert.AreEqual($"/modules/{IframeModule}", widget.Href, "the card's open link is the module's app shell");
    }

    [TestMethod]
    public async Task RefreshAsync_ModuleDropsItsWidget_UnregistersTheCard()
    {
        SeedInstalled(IframeModule, "Enabled");
        DeclareUi(
            IframeModule,
            navItems: [("open", "Receipts", "receipt_long", "/")],
            widgetPath: "/widget/index.html");

        var service = CreateService();
        await service.RefreshAsync(CancellationToken.None);
        Assert.AreEqual(1, _widgetRegistry.Widgets.Count);

        DeclareUi(IframeModule, navItems: [("open", "Receipts", "receipt_long", "/")]);
        await service.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(0, _widgetRegistry.Widgets.Count, "a removed ui.widget block must not leave a stale card");
    }

    [TestMethod]
    public async Task RefreshAsync_ModuleWithoutUi_IsNotRegistered()
    {
        SeedInstalled(IframeModule, "Enabled");
        _manifests.Record(IframeModule, new ModuleManifestData
        {
            Id = IframeModule,
            Name = "Receipts",
            Version = "1.0.0",
            ManifestVersion = 2,
        });

        await CreateService().RefreshAsync(CancellationToken.None);

        Assert.IsNull(_moduleRegistry.GetApp(IframeModule));
    }

    [TestMethod]
    public async Task RefreshAsync_ModuleBecomesDisabled_UnregistersEverything()
    {
        SeedInstalled(IframeModule, "Enabled");
        DeclareUi(IframeModule, navItems: [("open", "Receipts", "receipt_long", "/")]);

        var service = CreateService();
        await service.RefreshAsync(CancellationToken.None);
        Assert.IsNotNull(_moduleRegistry.GetApp(IframeModule));

        UpdateStatus(IframeModule, "Disabled");
        await service.RefreshAsync(CancellationToken.None);

        Assert.IsNull(_moduleRegistry.GetApp(IframeModule));
        Assert.AreEqual(0, _moduleRegistry.NavItems.Count, "Stale navigation entries must be removed.");
    }

    [TestMethod]
    public async Task RefreshAsync_CalledTwice_DoesNotDuplicateNavigation()
    {
        SeedInstalled(IframeModule, "Enabled");
        DeclareUi(IframeModule, navItems: [("open", "Receipts", "receipt_long", "/")]);

        var service = CreateService();
        await service.RefreshAsync(CancellationToken.None);
        await service.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(1, _moduleRegistry.NavItems.Count);
    }

    [TestMethod]
    public async Task RefreshAsync_SecondNavItem_GetsAnIncrementingSortOrder()
    {
        SeedInstalled(IframeModule, "Enabled");
        DeclareUi(IframeModule, navItems:
        [
            ("a", "First", "looks_one", "/"),
            ("b", "Second", "looks_two", "/second"),
        ]);

        await CreateService().RefreshAsync(CancellationToken.None);

        var nav = _moduleRegistry.NavItems.OrderBy(n => n.SortOrder).ToList();
        Assert.AreEqual(2, nav.Count);
        Assert.IsTrue(nav[0].SortOrder < nav[1].SortOrder);
    }

    private ModuleAppRegistrationHostedService CreateService() => new(
        _provider.GetRequiredService<IServiceScopeFactory>(),
        _manifests,
        _moduleRegistry,
        _widgetRegistry,
        NullLogger<ModuleAppRegistrationHostedService>.Instance);

    private void SeedInstalled(string moduleId, string status)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        if (db.InstalledModules.Any(m => m.ModuleId == moduleId))
        {
            UpdateStatus(moduleId, status);
            return;
        }

        db.InstalledModules.Add(new InstalledModule { ModuleId = moduleId, Version = "1.0.0", Status = status });
        db.SaveChanges();
    }

    private void UpdateStatus(string moduleId, string status)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var module = db.InstalledModules.Single(m => m.ModuleId == moduleId);
        module.Status = status;
        db.SaveChanges();
    }

    private void DeclareUi(
        string moduleId,
        (string Id, string Label, string? Icon, string Path)[] navItems,
        string? widgetPath = null) =>
        _manifests.Record(moduleId, new ModuleManifestData
        {
            Id = moduleId,
            Name = "Receipts",
            Version = "2.0.0",
            ManifestVersion = 2,
            Ui = new ManifestUi
            {
                Type = "iframe",
                EntryPath = "/",
                NavItems = navItems
                    .Select(item => new ManifestNavItem
                    {
                        Id = item.Id,
                        Label = item.Label,
                        Icon = item.Icon,
                        Path = item.Path,
                    })
                    .ToList(),
                Widget = widgetPath is null ? null : new ManifestWidget { Path = widgetPath },
            },
        });
}

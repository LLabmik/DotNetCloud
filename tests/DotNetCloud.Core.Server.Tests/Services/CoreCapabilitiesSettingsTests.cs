using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the <c>GetSetting</c> / <c>SetSetting</c> capability RPCs.
/// </summary>
/// <remarks>
/// Regression: both RPCs used to be no-op stubs, and the module id in the request payload was the
/// only namespace. They must persist through <see cref="IModuleSettingsStore"/> and always use the
/// credential-verified module identity.
/// </remarks>
[TestClass]
public class CoreCapabilitiesSettingsTests
{
    private const string AuthenticatedModuleId = "dotnetcloud.example";
    private const string SpoofedModuleId = "dotnetcloud.files";

    private static (CoreCapabilitiesServiceImpl Service, Mock<IModuleSettingsStore> Store) CreateService()
    {
        var store = new Mock<IModuleSettingsStore>();
        var services = new ServiceCollection()
            .AddScoped(_ => store.Object)
            .BuildServiceProvider();

        var service = new CoreCapabilitiesServiceImpl(
            NullLogger<CoreCapabilitiesServiceImpl>.Instance,
            services,
            new SearchIndexingService(Mock.Of<IServiceScopeFactory>(), NullLogger<SearchIndexingService>.Instance),
            new ModuleEventInbox(
                Mock.Of<DotNetCloud.Core.Events.IEventBus>(),
                Mock.Of<DotNetCloud.Core.Server.ModuleLoading.IModuleManifestRegistry>(),
                NullLogger<ModuleEventInbox>.Instance));

        return (service, store);
    }

    private static TestServerCallContext CreateContext(string moduleId = AuthenticatedModuleId)
    {
        var context = new TestServerCallContext();
        context.UserState["ModuleId"] = moduleId;
        context.UserState["CallerContext"] = new CallerContext(Guid.CreateVersion7(), [], CallerType.Module);
        return context;
    }

    [TestMethod]
    public async Task SetSetting_SpoofedModuleId_UsesAuthenticatedModuleId()
    {
        var (service, store) = CreateService();

        var response = await service.SetSetting(
            new SetSettingRequest { ModuleId = SpoofedModuleId, Key = "theme", Value = "dark" },
            CreateContext());

        Assert.IsTrue(response.Success);
        store.Verify(
            s => s.SetAsync(
                AuthenticatedModuleId,
                "theme",
                "dark",
                false,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetSetting_SpoofedModuleId_ReadsAuthenticatedModule()
    {
        var (service, store) = CreateService();
        store.Setup(s => s.GetAsync(AuthenticatedModuleId, "theme", It.IsAny<CancellationToken>()))
            .ReturnsAsync("dark");

        var response = await service.GetSetting(
            new GetSettingRequest { ModuleId = SpoofedModuleId, Key = "theme" },
            CreateContext());

        Assert.IsTrue(response.Found);
        Assert.AreEqual("dark", response.Value);
    }

    [TestMethod]
    public async Task GetSetting_MissingKey_ReturnsNotFound()
    {
        var (service, _) = CreateService();

        var response = await service.GetSetting(
            new GetSettingRequest { Key = "missing" },
            CreateContext());

        Assert.IsFalse(response.Found);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("bad key")]
    [DataRow("../escape")]
    public async Task GetSetting_InvalidKey_ReturnsNotFoundWithoutTouchingStore(string key)
    {
        var (service, store) = CreateService();

        var response = await service.GetSetting(
            new GetSettingRequest { Key = key },
            CreateContext());

        Assert.IsFalse(response.Found);
        store.Verify(
            s => s.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SetSetting_OversizedValue_ReturnsFailureWithoutPersisting()
    {
        var (service, store) = CreateService();
        var oversized = new string('x', ModuleSettingLimits.MaxValueLength + 1);

        var response = await service.SetSetting(
            new SetSettingRequest { Key = "blob", Value = oversized },
            CreateContext());

        Assert.IsFalse(response.Success);
        store.Verify(
            s => s.SetAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public void ModuleSettingLimits_KeyRules_AreConservative()
    {
        Assert.IsTrue(ModuleSettingLimits.IsValidKey("theme"));
        Assert.IsTrue(ModuleSettingLimits.IsValidKey("media.auto-fetch:enabled"));
        Assert.IsFalse(ModuleSettingLimits.IsValidKey("-leading"));
        Assert.IsFalse(ModuleSettingLimits.IsValidKey("_leading"));
        Assert.IsFalse(ModuleSettingLimits.IsValidKey(new string('a', ModuleSettingLimits.MaxKeyLength + 1)));
    }
}

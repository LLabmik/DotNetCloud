using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.About.Tests;

/// <summary>
/// Tests for <see cref="AboutModule"/> lifecycle implementation.
/// </summary>
[TestClass]
public class AboutModuleTests
{
    private AboutModule _module;

    [TestInitialize]
    public void Setup()
    {
        _module = new AboutModule();
    }

    [TestMethod]
    public void Manifest_ContainsCorrectModuleId()
    {
        var manifest = _module.Manifest;

        Assert.IsNotNull(manifest);
        Assert.AreEqual("dotnetcloud.about", manifest.Id);
    }

    [TestMethod]
    public void Manifest_VersionIsNotEmpty()
    {
        var manifest = _module.Manifest;

        Assert.IsFalse(string.IsNullOrWhiteSpace(manifest.Version));
    }

    [TestMethod]
    public void Manifest_HasNoRequiredCapabilities()
    {
        var manifest = _module.Manifest;

        Assert.IsNotNull(manifest.RequiredCapabilities);
        Assert.AreEqual(0, manifest.RequiredCapabilities.Count);
    }

    [TestMethod]
    public void Manifest_HasNoPublishedEvents()
    {
        var manifest = _module.Manifest;

        Assert.IsNotNull(manifest.PublishedEvents);
        Assert.AreEqual(0, manifest.PublishedEvents.Count);
    }

    [TestMethod]
    public void Manifest_HasNoSubscribedEvents()
    {
        var manifest = _module.Manifest;

        Assert.IsNotNull(manifest.SubscribedEvents);
        Assert.AreEqual(0, manifest.SubscribedEvents.Count);
    }

    [TestMethod]
    public void Manifest_NameIsAbout()
    {
        Assert.AreEqual("About", _module.Manifest.Name);
    }

    [TestMethod]
    public async Task InitializeAsync_ValidContext_SetsIsInitialized()
    {
        var context = CreateInitContext();

        Assert.IsFalse(_module.IsInitialized);
        await _module.InitializeAsync(context);
        Assert.IsTrue(_module.IsInitialized);
    }

    [TestMethod]
    public async Task StartAsync_AfterInitialize_SetsIsRunning()
    {
        var context = CreateInitContext();

        await _module.InitializeAsync(context);
        Assert.IsFalse(_module.IsRunning);

        await _module.StartAsync();
        Assert.IsTrue(_module.IsRunning);
    }

    [TestMethod]
    public async Task StopAsync_AfterStart_ClearsIsRunning()
    {
        var context = CreateInitContext();

        await _module.InitializeAsync(context);
        await _module.StartAsync();
        await _module.StopAsync();

        Assert.IsFalse(_module.IsRunning);
        Assert.IsTrue(_module.IsInitialized);
    }

    private static ModuleInitializationContext CreateInitContext()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = new Dictionary<string, object>();
        var systemCaller = new CallerContext(Guid.Empty, ["admin"], CallerType.System);
        return new ModuleInitializationContext
        {
            ModuleId = "dotnetcloud.about",
            Services = services,
            Configuration = config,
            SystemCaller = systemCaller
        };
    }
}

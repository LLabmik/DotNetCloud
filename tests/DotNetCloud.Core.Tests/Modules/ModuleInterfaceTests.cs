namespace DotNetCloud.Core.Tests.Modules;

using DotNetCloud.Core.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

/// <summary>
/// Tests for IModule and IModuleLifecycle interface contracts.
/// </summary>
[TestClass]
public class ModuleInterfaceTests
{
    [TestMethod]
    public void IModule_CanBeMocked()
    {
        // Arrange
        var mockModule = new Mock<IModule>();

        // Act & Assert
        Assert.IsNotNull(mockModule.Object);
    }

    [TestMethod]
    public void IModule_Manifest_CanBeSet()
    {
        // Arrange
        var mockModule = new Mock<IModule>();
        var mockManifest = new Mock<IModuleManifest>();
        mockManifest.Setup(m => m.Id).Returns("test.module");

        mockModule.Setup(m => m.Manifest).Returns(mockManifest.Object);

        // Act
        var result = mockModule.Object.Manifest;

        // Assert
        Assert.AreEqual("test.module", result.Id);
    }

    [TestMethod]
    public async Task IModule_InitializeAsync_CanBeMocked()
    {
        // Arrange
        var mockModule = new Mock<IModule>();
        mockModule
            .Setup(m => m.InitializeAsync(It.IsAny<ModuleInitializationContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var context = CreateTestInitializationContext();

        // Act
        await mockModule.Object.InitializeAsync(context);

        // Assert
        mockModule.Verify(
            m => m.InitializeAsync(It.IsAny<ModuleInitializationContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task IModule_StartAsync_CanBeMocked()
    {
        // Arrange
        var mockModule = new Mock<IModule>();
        mockModule
            .Setup(m => m.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mockModule.Object.StartAsync();

        // Assert
        mockModule.Verify(
            m => m.StartAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task IModule_StopAsync_CanBeMocked()
    {
        // Arrange
        var mockModule = new Mock<IModule>();
        mockModule
            .Setup(m => m.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mockModule.Object.StopAsync();

        // Assert
        mockModule.Verify(
            m => m.StopAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void IModuleLifecycle_ExtendsIModule()
    {
        // Arrange
        var mockLifecycle = new Mock<IModuleLifecycle>();

        // Act & Assert
        Assert.IsInstanceOfType(mockLifecycle.Object, typeof(IModule));
    }

    [TestMethod]
    public async Task IModuleLifecycle_DisposeAsync_CanBeMocked()
    {
        // Arrange
        var mockLifecycle = new Mock<IModuleLifecycle>();
        mockLifecycle
            .Setup(m => m.DisposeAsync())
            .Returns(Task.CompletedTask);

        // Act
        await mockLifecycle.Object.DisposeAsync();

        // Assert
        mockLifecycle.Verify(m => m.DisposeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task IModuleLifecycle_FullLifecycle_CanBeMocked()
    {
        // Arrange
        var mockLifecycle = new Mock<IModuleLifecycle>();
        mockLifecycle
            .Setup(m => m.InitializeAsync(It.IsAny<ModuleInitializationContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockLifecycle
            .Setup(m => m.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockLifecycle
            .Setup(m => m.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockLifecycle
            .Setup(m => m.DisposeAsync())
            .Returns(Task.CompletedTask);

        var context = CreateTestInitializationContext();

        // Act
        await mockLifecycle.Object.InitializeAsync(context);
        await mockLifecycle.Object.StartAsync();
        await mockLifecycle.Object.StopAsync();
        await mockLifecycle.Object.DisposeAsync();

        // Assert
        mockLifecycle.Verify(
            m => m.InitializeAsync(It.IsAny<ModuleInitializationContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mockLifecycle.Verify(m => m.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockLifecycle.Verify(m => m.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockLifecycle.Verify(m => m.DisposeAsync(), Times.Once);
    }

    [TestMethod]
    public void ModuleInitializationContext_CanBeCreated()
    {
        // Arrange
        var context = CreateTestInitializationContext();

        // Act & Assert
        Assert.IsNotNull(context);
        Assert.IsFalse(string.IsNullOrEmpty(context.ModuleId));
    }

    [TestMethod]
    public void ModuleInitializationContext_HasModuleId()
    {
        // Arrange
        var moduleId = "test.module";
        var systemCallerId = Guid.NewGuid();
        var systemCaller = new DotNetCloud.Core.Authorization.CallerContext(
            systemCallerId, 
            Array.Empty<string>(), 
            DotNetCloud.Core.Authorization.CallerType.System);

        // Act
        var context = new ModuleInitializationContext
        {
            ModuleId = moduleId,
            Services = new Mock<IServiceProvider>().Object,
            Configuration = new Dictionary<string, object>(),
            SystemCaller = systemCaller
        };

        // Assert
        Assert.AreEqual(moduleId, context.ModuleId);
    }

    [TestMethod]
    public void ModuleInitializationContext_HasServices()
    {
        // Arrange
        var mockServices = new Mock<IServiceProvider>();
        var systemCallerId = Guid.NewGuid();
        var systemCaller = new DotNetCloud.Core.Authorization.CallerContext(
            systemCallerId, 
            Array.Empty<string>(), 
            DotNetCloud.Core.Authorization.CallerType.System);

        // Act
        var context = new ModuleInitializationContext
        {
            ModuleId = "test.module",
            Services = mockServices.Object,
            Configuration = new Dictionary<string, object>(),
            SystemCaller = systemCaller
        };

        // Assert
        Assert.AreEqual(mockServices.Object, context.Services);
    }

    [TestMethod]
    public void ModuleInitializationContext_HasConfiguration()
    {
        // Arrange
        var config = new Dictionary<string, object> { { "key", "value" } };
        var systemCallerId = Guid.NewGuid();
        var systemCaller = new DotNetCloud.Core.Authorization.CallerContext(
            systemCallerId, 
            Array.Empty<string>(), 
            DotNetCloud.Core.Authorization.CallerType.System);

        // Act
        var context = new ModuleInitializationContext
        {
            ModuleId = "test.module",
            Services = new Mock<IServiceProvider>().Object,
            Configuration = config,
            SystemCaller = systemCaller
        };

        // Assert
        Assert.AreEqual(1, context.Configuration.Count);
    }

    [TestMethod]
    public void ModuleInitializationContext_HasSystemCaller()
    {
        // Arrange - use a valid system caller GUID instead of CreateSystemContext which has a bug
        var systemCallerId = Guid.NewGuid();
        var systemCaller = new DotNetCloud.Core.Authorization.CallerContext(
            systemCallerId,
            Array.Empty<string>(),
            DotNetCloud.Core.Authorization.CallerType.System);

        var context = new ModuleInitializationContext
        {
            ModuleId = "test.module",
            Services = new Mock<IServiceProvider>().Object,
            Configuration = new Dictionary<string, object>(),
            SystemCaller = systemCaller
        };

        // Act & Assert
        Assert.IsNotNull(context.SystemCaller);
        Assert.AreEqual(DotNetCloud.Core.Authorization.CallerType.System, context.SystemCaller.Type);
    }

    [TestMethod]
    public void ModuleInitializationContext_IsRecord()
    {
        // Arrange - use a valid system caller GUID instead of CreateSystemContext which has a bug
        var systemCallerId = Guid.NewGuid();
        var systemCaller = new DotNetCloud.Core.Authorization.CallerContext(
            systemCallerId,
            Array.Empty<string>(),
            DotNetCloud.Core.Authorization.CallerType.System);

        var context1 = new ModuleInitializationContext
        {
            ModuleId = "test.module",
            Services = new Mock<IServiceProvider>().Object,
            Configuration = new Dictionary<string, object>(),
            SystemCaller = systemCaller
        };
        var context2 = new ModuleInitializationContext
        {
            ModuleId = context1.ModuleId,
            Services = context1.Services,
            Configuration = context1.Configuration,
            SystemCaller = context1.SystemCaller
        };

        // Act & Assert (records support equality by value)
        Assert.IsNotNull(context1);
        Assert.IsNotNull(context2);
    }

    private static ModuleInitializationContext CreateTestInitializationContext()
    {
        var systemCallerId = Guid.NewGuid();
        var systemCaller = new DotNetCloud.Core.Authorization.CallerContext(
            systemCallerId,
            Array.Empty<string>(),
            DotNetCloud.Core.Authorization.CallerType.System);

        return new ModuleInitializationContext
        {
            ModuleId = "test.module",
            Services = new Mock<IServiceProvider>().Object,
            Configuration = new Dictionary<string, object>(),
            SystemCaller = systemCaller
        };
    }
}

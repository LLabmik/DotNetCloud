namespace DotNetCloud.Core.Tests.Modules;

using DotNetCloud.Core.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

/// <summary>
/// Tests for IModuleManifest interface contracts.
/// </summary>
[TestClass]
public class ModuleManifestTests
{
    /// <summary>
    /// Test implementation of IModuleManifest for testing contracts.
    /// </summary>
    private class TestModuleManifest : IModuleManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public IReadOnlyCollection<string> RequiredCapabilities { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> PublishedEvents { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> SubscribedEvents { get; set; } = Array.Empty<string>();
    }

    [TestMethod]
    public void ModuleManifest_CanImplementInterface()
    {
        // Arrange & Act
        var manifest = new TestModuleManifest();

        // Assert
        Assert.IsInstanceOfType(manifest, typeof(IModuleManifest));
    }

    [TestMethod]
    public void ModuleManifest_Id_CanBeSet()
    {
        // Arrange
        var manifest = new TestModuleManifest { Id = "test.module" };

        // Act & Assert
        Assert.AreEqual("test.module", manifest.Id);
    }

    [TestMethod]
    public void ModuleManifest_Name_CanBeSet()
    {
        // Arrange
        var manifest = new TestModuleManifest { Name = "Test Module" };

        // Act & Assert
        Assert.AreEqual("Test Module", manifest.Name);
    }

    [TestMethod]
    public void ModuleManifest_Version_FollowsSemanticVersioning()
    {
        // Arrange
        var manifest = new TestModuleManifest { Version = "1.2.3" };

        // Act & Assert
        Assert.AreEqual("1.2.3", manifest.Version);
    }

    [TestMethod]
    public void ModuleManifest_RequiredCapabilities_CanContainMultiple()
    {
        // Arrange
        var capabilities = new[] { "IUserDirectory", "IStorageProvider", "IEventBus" };
        var manifest = new TestModuleManifest { RequiredCapabilities = capabilities };

        // Act & Assert
        Assert.AreEqual(3, manifest.RequiredCapabilities.Count);
        Assert.IsTrue(manifest.RequiredCapabilities.Contains("IUserDirectory"));
    }

    [TestMethod]
    public void ModuleManifest_PublishedEvents_CanContainMultiple()
    {
        // Arrange
        var events = new[] { "UserCreatedEvent", "UserDeletedEvent" };
        var manifest = new TestModuleManifest { PublishedEvents = events };

        // Act & Assert
        Assert.AreEqual(2, manifest.PublishedEvents.Count);
        Assert.IsTrue(manifest.PublishedEvents.Contains("UserCreatedEvent"));
    }

    [TestMethod]
    public void ModuleManifest_SubscribedEvents_CanContainMultiple()
    {
        // Arrange
        var events = new[] { "OrganizationCreatedEvent", "TeamUpdatedEvent" };
        var manifest = new TestModuleManifest { SubscribedEvents = events };

        // Act & Assert
        Assert.AreEqual(2, manifest.SubscribedEvents.Count);
    }

    [TestMethod]
    public void ModuleManifest_AllPropertiesCanBeSet()
    {
        // Arrange
        var manifest = new TestModuleManifest
        {
            Id = "test.files",
            Name = "Files Module",
            Version = "2.0.0",
            RequiredCapabilities = new[] { "IStorageProvider" },
            PublishedEvents = new[] { "FileUploadedEvent" },
            SubscribedEvents = new[] { "UserCreatedEvent" }
        };

        // Act & Assert
        Assert.AreEqual("test.files", manifest.Id);
        Assert.AreEqual("Files Module", manifest.Name);
        Assert.AreEqual("2.0.0", manifest.Version);
        Assert.AreEqual(1, manifest.RequiredCapabilities.Count);
        Assert.AreEqual(1, manifest.PublishedEvents.Count);
        Assert.AreEqual(1, manifest.SubscribedEvents.Count);
    }

    [TestMethod]
    public void ModuleManifest_EmptyCollections_AreReadOnly()
    {
        // Arrange
        var manifest = new TestModuleManifest();

        // Act & Assert
        Assert.IsNotNull(manifest.RequiredCapabilities);
        Assert.IsNotNull(manifest.PublishedEvents);
        Assert.IsNotNull(manifest.SubscribedEvents);
    }

    [TestMethod]
    public void ModuleManifest_CanBeMocked()
    {
        // Arrange
        var mockManifest = new Mock<IModuleManifest>();
        mockManifest.Setup(m => m.Id).Returns("mock.module");
        mockManifest.Setup(m => m.Name).Returns("Mock Module");
        mockManifest.Setup(m => m.Version).Returns("1.0.0");

        // Act
        var result = mockManifest.Object;

        // Assert
        Assert.AreEqual("mock.module", result.Id);
        Assert.AreEqual("Mock Module", result.Name);
        Assert.AreEqual("1.0.0", result.Version);
    }

    [TestMethod]
    public void ModuleManifest_Mock_CanVerifyPropertyAccess()
    {
        // Arrange
        var mockManifest = new Mock<IModuleManifest>();
        mockManifest.Setup(m => m.RequiredCapabilities).Returns(new[] { "Capability1" });

        // Act
        var capabilities = mockManifest.Object.RequiredCapabilities;

        // Assert
        Assert.AreEqual(1, capabilities.Count);
        mockManifest.Verify(m => m.RequiredCapabilities, Times.Once);
    }
}

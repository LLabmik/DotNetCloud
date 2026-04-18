using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Photos.Events;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class PhotosModuleManifestTests
{
    [TestMethod]
    public void Manifest_HasCorrectId()
    {
        var manifest = new PhotosModuleManifest();
        Assert.AreEqual("dotnetcloud.photos", manifest.Id);
    }

    [TestMethod]
    public void Manifest_HasCorrectName()
    {
        var manifest = new PhotosModuleManifest();
        Assert.AreEqual("Photos", manifest.Name);
    }

    [TestMethod]
    public void Manifest_HasVersion()
    {
        var manifest = new PhotosModuleManifest();
        Assert.IsFalse(string.IsNullOrEmpty(manifest.Version));
    }

    [TestMethod]
    public void Manifest_DeclaresRequiredCapabilities()
    {
        var manifest = new PhotosModuleManifest();
        Assert.IsTrue(manifest.RequiredCapabilities.Count > 0);
    }

    [TestMethod]
    public void Manifest_DeclaresPublishableEvents()
    {
        var manifest = new PhotosModuleManifest();
        var eventTypes = manifest.PublishedEvents.ToList();

        Assert.IsTrue(eventTypes.Contains(nameof(PhotoUploadedEvent)));
        Assert.IsTrue(eventTypes.Contains(nameof(PhotoDeletedEvent)));
        Assert.IsTrue(eventTypes.Contains(nameof(AlbumCreatedEvent)));
    }

    [TestMethod]
    public void Manifest_DeclaresSubscribableEvents()
    {
        var manifest = new PhotosModuleManifest();
        Assert.IsTrue(manifest.SubscribedEvents.Any());
    }
}

[TestClass]
public class PhotosModuleLifecycleTests
{
    [TestMethod]
    public void Module_IsNotInitializedByDefault()
    {
        var module = new PhotosModule();
        Assert.IsFalse(module.IsInitialized);
    }

    [TestMethod]
    public void Module_IsNotRunningByDefault()
    {
        var module = new PhotosModule();
        Assert.IsFalse(module.IsRunning);
    }

    [TestMethod]
    public void Module_HasManifest()
    {
        var module = new PhotosModule();
        Assert.IsNotNull(module.Manifest);
        Assert.AreEqual("dotnetcloud.photos", module.Manifest.Id);
    }

    [TestMethod]
    public async Task Module_StartWithoutInit_ThrowsInvalidOperationException()
    {
        var module = new PhotosModule();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => module.StartAsync());
    }
}

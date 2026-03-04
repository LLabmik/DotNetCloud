using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Files.Events;

namespace DotNetCloud.Modules.Files.Tests;

/// <summary>
/// Tests for <see cref="FilesModuleManifest"/> verifying manifest metadata and contracts.
/// </summary>
[TestClass]
public class FilesModuleManifestTests
{
    private FilesModuleManifest _manifest = null!;

    [TestInitialize]
    public void Setup()
    {
        _manifest = new FilesModuleManifest();
    }

    [TestMethod]
    public void WhenCreatedThenIdIsDotnetcloudFiles()
    {
        Assert.AreEqual("dotnetcloud.files", _manifest.Id);
    }

    [TestMethod]
    public void WhenCreatedThenNameIsFiles()
    {
        Assert.AreEqual("Files", _manifest.Name);
    }

    [TestMethod]
    public void WhenCreatedThenVersionIsSemanticVersion()
    {
        Assert.AreEqual("1.0.0", _manifest.Version);
        Assert.IsTrue(Version.TryParse(_manifest.Version, out _));
    }

    [TestMethod]
    public void WhenCreatedThenIdIsLowercaseDotSeparated()
    {
        Assert.AreEqual(_manifest.Id, _manifest.Id.ToLowerInvariant());
        Assert.IsTrue(_manifest.Id.Contains('.'));
    }

    [TestMethod]
    public void WhenCreatedThenImplementsIModuleManifest()
    {
        Assert.IsInstanceOfType<IModuleManifest>(_manifest);
    }

    [TestMethod]
    public void WhenCreatedThenRequiredCapabilitiesContainsExpectedCapabilities()
    {
        var capabilities = _manifest.RequiredCapabilities;

        Assert.IsNotNull(capabilities);
        Assert.AreEqual(4, capabilities.Count);
        CollectionAssert.Contains(capabilities.ToList(), "INotificationService");
        CollectionAssert.Contains(capabilities.ToList(), "IStorageProvider");
        CollectionAssert.Contains(capabilities.ToList(), "IUserDirectory");
        CollectionAssert.Contains(capabilities.ToList(), "ICurrentUserContext");
    }

    [TestMethod]
    public void WhenCreatedThenPublishedEventsContainsExpectedEvents()
    {
        var events = _manifest.PublishedEvents;

        Assert.IsNotNull(events);
        Assert.AreEqual(5, events.Count);
        CollectionAssert.Contains(events.ToList(), nameof(FileUploadedEvent));
        CollectionAssert.Contains(events.ToList(), nameof(FileDeletedEvent));
        CollectionAssert.Contains(events.ToList(), nameof(FileMovedEvent));
        CollectionAssert.Contains(events.ToList(), nameof(FileSharedEvent));
        CollectionAssert.Contains(events.ToList(), nameof(FileRestoredEvent));
    }

    [TestMethod]
    public void WhenCreatedThenSubscribedEventsIsEmpty()
    {
        var events = _manifest.SubscribedEvents;

        Assert.IsNotNull(events);
        Assert.AreEqual(0, events.Count);
    }

    [TestMethod]
    public void WhenCreatedThenRequiredCapabilitiesIsNotNull()
    {
        Assert.IsNotNull(_manifest.RequiredCapabilities);
    }

    [TestMethod]
    public void WhenCreatedThenPublishedEventsIsNotNull()
    {
        Assert.IsNotNull(_manifest.PublishedEvents);
    }
}

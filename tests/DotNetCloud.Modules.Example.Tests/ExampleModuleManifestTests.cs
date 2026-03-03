using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Example.Events;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Tests for <see cref="ExampleModuleManifest"/> verifying manifest metadata and contracts.
/// </summary>
[TestClass]
public class ExampleModuleManifestTests
{
    private ExampleModuleManifest _manifest = null!;

    [TestInitialize]
    public void Setup()
    {
        _manifest = new ExampleModuleManifest();
    }

    [TestMethod]
    public void WhenCreatedThenIdIsDotnetcloudExample()
    {
        Assert.AreEqual("dotnetcloud.example", _manifest.Id);
    }

    [TestMethod]
    public void WhenCreatedThenNameIsExample()
    {
        Assert.AreEqual("Example", _manifest.Name);
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
        Assert.AreEqual(2, capabilities.Count);
        CollectionAssert.Contains(capabilities.ToList(), "INotificationService");
        CollectionAssert.Contains(capabilities.ToList(), "IStorageProvider");
    }

    [TestMethod]
    public void WhenCreatedThenPublishedEventsContainsExpectedEvents()
    {
        var events = _manifest.PublishedEvents;

        Assert.IsNotNull(events);
        Assert.AreEqual(2, events.Count);
        CollectionAssert.Contains(events.ToList(), nameof(NoteCreatedEvent));
        CollectionAssert.Contains(events.ToList(), nameof(NoteDeletedEvent));
    }

    [TestMethod]
    public void WhenCreatedThenSubscribedEventsIsEmpty()
    {
        Assert.IsNotNull(_manifest.SubscribedEvents);
        Assert.AreEqual(0, _manifest.SubscribedEvents.Count);
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

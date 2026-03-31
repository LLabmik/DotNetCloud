using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Chat.Events;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChatModuleManifest"/> verifying manifest metadata and contracts.
/// </summary>
[TestClass]
public class ChatModuleManifestTests
{
    private ChatModuleManifest _manifest = null!;

    [TestInitialize]
    public void Setup()
    {
        _manifest = new ChatModuleManifest();
    }

    [TestMethod]
    public void WhenCreatedThenIdIsDotnetcloudChat()
    {
        Assert.AreEqual("dotnetcloud.chat", _manifest.Id);
    }

    [TestMethod]
    public void WhenCreatedThenNameIsChat()
    {
        Assert.AreEqual("Chat", _manifest.Name);
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
        CollectionAssert.Contains(capabilities.ToList(), "IUserDirectory");
        CollectionAssert.Contains(capabilities.ToList(), "ICurrentUserContext");
        CollectionAssert.Contains(capabilities.ToList(), "IRealtimeBroadcaster");
    }

    [TestMethod]
    public void WhenCreatedThenPublishedEventsContainsExpectedEvents()
    {
        var events = _manifest.PublishedEvents;

        Assert.IsNotNull(events);
        Assert.AreEqual(6, events.Count);
        CollectionAssert.Contains(events.ToList(), nameof(MessageSentEvent));
        CollectionAssert.Contains(events.ToList(), nameof(ChannelCreatedEvent));
        CollectionAssert.Contains(events.ToList(), nameof(ChannelDeletedEvent));
        CollectionAssert.Contains(events.ToList(), nameof(UserJoinedChannelEvent));
        CollectionAssert.Contains(events.ToList(), nameof(UserLeftChannelEvent));
        CollectionAssert.Contains(events.ToList(), nameof(PresenceChangedEvent));
    }

    [TestMethod]
    public void WhenCreatedThenSubscribedEventsContainsFileUploadedEvent()
    {
        var events = _manifest.SubscribedEvents;

        Assert.IsNotNull(events);
        Assert.IsTrue(events.Count >= 1, "Should have at least one subscribed event");
        CollectionAssert.Contains(events.ToList(), "FileUploadedEvent");
    }

    [TestMethod]
    public void WhenCreatedThenRequiredCapabilitiesIsNotEmpty()
    {
        Assert.IsTrue(_manifest.RequiredCapabilities.Count > 0);
    }

    [TestMethod]
    public void WhenCreatedThenPublishedEventsIsNotEmpty()
    {
        Assert.IsTrue(_manifest.PublishedEvents.Count > 0);
    }

    [TestMethod]
    public void WhenCreatedThenSubscribedEventsIsNotEmpty()
    {
        Assert.IsTrue(_manifest.SubscribedEvents.Count > 0);
    }
}

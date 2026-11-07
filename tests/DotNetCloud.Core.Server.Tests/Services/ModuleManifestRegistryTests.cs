using DotNetCloud.Core.Server.ModuleLoading;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="ModuleManifestRegistry"/>: the supervisor records what each manifest declares
/// it publishes, and <c>PublishEvent</c> consults that record instead of a global allow-list alone.
/// </summary>
[TestClass]
public class ModuleManifestRegistryTests
{
    private static ModuleManifestData CreateManifest(
        int manifestVersion = 1,
        IReadOnlyList<string>? publishedEvents = null,
        IReadOnlyList<string>? v2Publishes = null) => new()
        {
            ManifestVersion = manifestVersion,
            Id = "dotnetcloud.test",
            Name = "Test",
            Version = "1.0.0",
            PublishedEvents = publishedEvents ?? [],
            Events = manifestVersion >= 2 && v2Publishes is not null
                ? new ManifestEvents { Publishes = v2Publishes, Subscribes = [] }
                : null,
        };

    [TestMethod]
    public void GetPublishedEvents_UnknownModule_ReturnsNull()
    {
        var registry = new ModuleManifestRegistry();

        Assert.IsNull(registry.GetPublishedEvents("dotnetcloud.unknown"));
    }

    [TestMethod]
    public void GetPublishedEvents_RecordedV1Manifest_ReturnsDeclaredEvents()
    {
        var registry = new ModuleManifestRegistry();
        registry.Record("dotnetcloud.calendar", CreateManifest(publishedEvents: ["ReminderTriggeredEvent"]));

        var events = registry.GetPublishedEvents("dotnetcloud.calendar");

        Assert.IsNotNull(events);
        CollectionAssert.AreEqual(new[] { "ReminderTriggeredEvent" }, events.ToArray());
    }

    [TestMethod]
    public void GetPublishedEvents_RecordedV2Manifest_UsesTheV2PublishesList()
    {
        var registry = new ModuleManifestRegistry();
        registry.Record(
            "org.example.app",
            CreateManifest(
                manifestVersion: 2,
                publishedEvents: ["IgnoredV1Event"],
                v2Publishes: ["ThingCreatedEvent"]));

        var events = registry.GetPublishedEvents("org.example.app");

        CollectionAssert.AreEqual(new[] { "ThingCreatedEvent" }, events!.ToArray());
    }

    [TestMethod]
    public void Record_BlankEntriesAndDuplicates_AreNormalized()
    {
        var registry = new ModuleManifestRegistry();
        registry.Record(
            "dotnetcloud.files",
            CreateManifest(publishedEvents: ["FileUploadedEvent", " ", "fileuploadedevent"]));

        var events = registry.GetPublishedEvents("dotnetcloud.files")!;

        Assert.AreEqual(1, events.Count);
    }

    [TestMethod]
    public void Record_CalledAgain_ReplacesThePreviousSet()
    {
        var registry = new ModuleManifestRegistry();
        registry.Record("dotnetcloud.files", CreateManifest(publishedEvents: ["FileUploadedEvent"]));
        registry.Record("dotnetcloud.files", CreateManifest(publishedEvents: ["ShareExpiringEvent"]));

        var events = registry.GetPublishedEvents("dotnetcloud.files")!;

        CollectionAssert.AreEqual(new[] { "ShareExpiringEvent" }, events.ToArray());
    }

    [TestMethod]
    public void GetPublishedEvents_ModuleIdCaseDiffers_StillResolves()
    {
        var registry = new ModuleManifestRegistry();
        registry.Record("dotnetcloud.files", CreateManifest(publishedEvents: ["FileUploadedEvent"]));

        Assert.IsNotNull(registry.GetPublishedEvents("DotNetCloud.Files"));
    }
}

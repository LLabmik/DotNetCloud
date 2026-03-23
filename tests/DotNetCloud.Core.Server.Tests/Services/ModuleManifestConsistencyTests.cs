using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Calendar;
using DotNetCloud.Modules.Contacts;
using DotNetCloud.Modules.Notes;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public class ModuleManifestConsistencyTests
{
    private static readonly IModuleManifest[] PimManifests =
    [
        new ContactsModuleManifest(),
        new CalendarModuleManifest(),
        new NotesModuleManifest()
    ];

    [TestMethod]
    public void AllPimModules_DeclareNotificationService()
    {
        foreach (var manifest in PimManifests)
        {
            Assert.IsTrue(
                manifest.RequiredCapabilities.Contains("INotificationService"),
                $"{manifest.Name} missing INotificationService");
        }
    }

    [TestMethod]
    public void AllPimModules_DeclareUserDirectory()
    {
        foreach (var manifest in PimManifests)
        {
            Assert.IsTrue(
                manifest.RequiredCapabilities.Contains("IUserDirectory"),
                $"{manifest.Name} missing IUserDirectory");
        }
    }

    [TestMethod]
    public void AllPimModules_DeclareCurrentUserContext()
    {
        foreach (var manifest in PimManifests)
        {
            Assert.IsTrue(
                manifest.RequiredCapabilities.Contains("ICurrentUserContext"),
                $"{manifest.Name} missing ICurrentUserContext");
        }
    }

    [TestMethod]
    public void AllPimModules_DeclareAuditLogger()
    {
        foreach (var manifest in PimManifests)
        {
            Assert.IsTrue(
                manifest.RequiredCapabilities.Contains("IAuditLogger"),
                $"{manifest.Name} missing IAuditLogger");
        }
    }

    [TestMethod]
    public void AllPimModules_DeclareCrossModuleLinkResolver()
    {
        foreach (var manifest in PimManifests)
        {
            Assert.IsTrue(
                manifest.RequiredCapabilities.Contains("ICrossModuleLinkResolver"),
                $"{manifest.Name} missing ICrossModuleLinkResolver");
        }
    }

    [TestMethod]
    public void AllPimModules_HaveUniqueIds()
    {
        var ids = PimManifests.Select(m => m.Id).ToList();
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "Duplicate module IDs detected");
    }

    [TestMethod]
    public void AllPimModules_PublishLifecycleEvents()
    {
        // Each PIM module should publish at least Created, Updated, Deleted events
        foreach (var manifest in PimManifests)
        {
            Assert.IsTrue(manifest.PublishedEvents.Count >= 3,
                $"{manifest.Name} should publish at least 3 lifecycle events, found {manifest.PublishedEvents.Count}");
        }
    }

    [TestMethod]
    public void AllPimModules_PublishResourceSharedEvent()
    {
        foreach (var manifest in PimManifests)
        {
            Assert.IsTrue(
                manifest.PublishedEvents.Contains("ResourceSharedEvent"),
                $"{manifest.Name} missing ResourceSharedEvent publication");
        }
    }

    [TestMethod]
    public void AllPimModules_SubscribeToCrossModuleEvents()
    {
        foreach (var manifest in PimManifests)
        {
            Assert.IsTrue(manifest.SubscribedEvents.Count > 0,
                $"{manifest.Name} should subscribe to at least one cross-module event");
        }
    }

    [TestMethod]
    public void CalendarModule_DeclaresContactDirectory()
    {
        var manifest = new CalendarModuleManifest();
        Assert.IsTrue(manifest.RequiredCapabilities.Contains("IContactDirectory"),
            "Calendar should require IContactDirectory for attendee resolution");
    }

    [TestMethod]
    public void NotesModule_DeclaresContactAndCalendarDirectories()
    {
        var manifest = new NotesModuleManifest();
        Assert.IsTrue(manifest.RequiredCapabilities.Contains("IContactDirectory"),
            "Notes should require IContactDirectory for cross-links");
        Assert.IsTrue(manifest.RequiredCapabilities.Contains("ICalendarDirectory"),
            "Notes should require ICalendarDirectory for cross-links");
    }

    [TestMethod]
    public void NotesModule_PublishesUserMentionedEvent()
    {
        var manifest = new NotesModuleManifest();
        Assert.IsTrue(manifest.PublishedEvents.Contains("UserMentionedEvent"),
            "Notes should publish UserMentionedEvent");
    }

    [TestMethod]
    public void CalendarModule_PublishesReminderTriggeredEvent()
    {
        var manifest = new CalendarModuleManifest();
        Assert.IsTrue(manifest.PublishedEvents.Contains("ReminderTriggeredEvent"),
            "Calendar should publish ReminderTriggeredEvent");
    }
}

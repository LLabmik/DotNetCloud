using DotNetCloud.Modules.Notes;

namespace DotNetCloud.Modules.Notes.Tests;

[TestClass]
public class NotesModuleTests
{
    [TestMethod]
    public void Manifest_HasCorrectId()
    {
        var module = new NotesModule();
        Assert.AreEqual("dotnetcloud.notes", module.Manifest.Id);
    }

    [TestMethod]
    public void Manifest_HasCorrectName()
    {
        var module = new NotesModule();
        Assert.AreEqual("Notes", module.Manifest.Name);
    }

    [TestMethod]
    public void Manifest_HasCorrectVersion()
    {
        var module = new NotesModule();
        Assert.AreEqual("1.0.0", module.Manifest.Version);
    }

    [TestMethod]
    public void Manifest_RequiresExpectedCapabilities()
    {
        var module = new NotesModule();
        Assert.IsTrue(module.Manifest.RequiredCapabilities.Contains("INotificationService"));
        Assert.IsTrue(module.Manifest.RequiredCapabilities.Contains("IUserDirectory"));
        Assert.IsTrue(module.Manifest.RequiredCapabilities.Contains("ICurrentUserContext"));
    }

    [TestMethod]
    public void Manifest_PublishesNoteEvents()
    {
        var module = new NotesModule();
        Assert.IsTrue(module.Manifest.PublishedEvents.Contains("NoteCreatedEvent"));
        Assert.IsTrue(module.Manifest.PublishedEvents.Contains("NoteUpdatedEvent"));
        Assert.IsTrue(module.Manifest.PublishedEvents.Contains("NoteDeletedEvent"));
    }

    [TestMethod]
    public void Manifest_SubscribesNoEvents()
    {
        var module = new NotesModule();
        Assert.AreEqual(0, module.Manifest.SubscribedEvents.Count);
    }

    [TestMethod]
    public void NewModule_IsNotInitialized()
    {
        var module = new NotesModule();
        Assert.IsFalse(module.IsInitialized);
        Assert.IsFalse(module.IsRunning);
    }

    [TestMethod]
    public async Task StartAsync_WithoutInitialize_Throws()
    {
        var module = new NotesModule();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => module.StartAsync());
    }
}

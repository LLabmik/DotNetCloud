using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Contacts.Tests;

/// <summary>
/// Tests for <see cref="ContactsModule"/> lifecycle and manifest.
/// </summary>
[TestClass]
public class ContactsModuleTests
{
    [TestMethod]
    public void Manifest_HasCorrectId()
    {
        var module = new ContactsModule();

        Assert.AreEqual("dotnetcloud.contacts", module.Manifest.Id);
    }

    [TestMethod]
    public void Manifest_HasCorrectName()
    {
        var module = new ContactsModule();

        Assert.AreEqual("Contacts", module.Manifest.Name);
    }

    [TestMethod]
    public void Manifest_RequiresExpectedCapabilities()
    {
        var module = new ContactsModule();

        Assert.IsTrue(module.Manifest.RequiredCapabilities.Contains("INotificationService"));
        Assert.IsTrue(module.Manifest.RequiredCapabilities.Contains("IUserDirectory"));
        Assert.IsTrue(module.Manifest.RequiredCapabilities.Contains("ICurrentUserContext"));
    }

    [TestMethod]
    public void Manifest_PublishesContactEvents()
    {
        var module = new ContactsModule();

        Assert.IsTrue(module.Manifest.PublishedEvents.Contains("ContactCreatedEvent"));
        Assert.IsTrue(module.Manifest.PublishedEvents.Contains("ContactUpdatedEvent"));
        Assert.IsTrue(module.Manifest.PublishedEvents.Contains("ContactDeletedEvent"));
    }

    [TestMethod]
    public void NewModule_IsNotInitialized()
    {
        var module = new ContactsModule();

        Assert.IsFalse(module.IsInitialized);
        Assert.IsFalse(module.IsRunning);
    }

    [TestMethod]
    public async Task StartAsync_WithoutInitialize_Throws()
    {
        var module = new ContactsModule();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => module.StartAsync());
    }
}

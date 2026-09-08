using DotNetCloud.Core.Server.SharedWithMe;
using DotNetCloud.Modules.Contacts.Models;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.SharedWithMe;

/// <summary>
/// Tests for <see cref="ContactsSharedWithMeProvider"/>: the Contacts module adapter that feeds
/// Files' virtual "_DotNetCloud/SharedWithMe/Contacts" folder.
/// </summary>
[TestClass]
public class ContactsSharedWithMeProviderTests
{
    /// <summary>
    /// Builds a provider whose DI scope resolves the given mocked module HTTP client.
    /// </summary>
    private static ContactsSharedWithMeProvider BuildProvider(IContactsApiClient? client = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(client ?? Mock.Of<IContactsApiClient>());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ContactsSharedWithMeProvider>>(
            NullLogger<ContactsSharedWithMeProvider>.Instance);
        services.AddSingleton<ContactsSharedWithMeProvider>();
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<ContactsSharedWithMeProvider>();
    }

    private static ContactSharedItem SharedContact(Guid contactId, string displayName, Guid sharedByUserId, DateTime updatedAt) => new()
    {
        ContactId = contactId,
        DisplayName = displayName,
        SharedByUserId = sharedByUserId,
        SharedWithUserId = Guid.CreateVersion7(),
        Permission = ContactSharePermission.ReadOnly,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = updatedAt
    };

    [TestMethod]
    public async Task ListAsync_ContactSharedWithUser_ReturnsMappedItemWithContactDeepLink()
    {
        var userId = Guid.CreateVersion7();
        var contactId = Guid.CreateVersion7();
        var updatedAt = DateTime.UtcNow.AddHours(-1);

        var client = new Mock<IContactsApiClient>();
        client
            .Setup(c => c.ListSharedWithMeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([SharedContact(contactId, "Jane Doe", Guid.CreateVersion7(), updatedAt)]);

        var provider = BuildProvider(client.Object);
        var items = await provider.ListAsync(userId);

        Assert.AreEqual(1, items.Count);
        var item = items.Single();
        Assert.AreEqual("contacts", item.ModuleId);
        Assert.AreEqual("Contacts", item.DisplayName);
        Assert.AreEqual("Contact", item.EntityType);
        Assert.AreEqual(contactId, item.EntityId);
        Assert.AreEqual("Jane Doe", item.Title);
        Assert.AreEqual($"/apps/contacts?contactId={contactId}", item.DeepLink);
        Assert.AreEqual("person", item.IconName);
        Assert.AreEqual(updatedAt, item.UpdatedAt);
        Assert.AreEqual(1, await provider.CountAsync(userId));
    }

    [TestMethod]
    public async Task ListAsync_SelfSharedContact_Excluded()
    {
        var userId = Guid.CreateVersion7();
        var contactId = Guid.CreateVersion7();

        var client = new Mock<IContactsApiClient>();
        client
            .Setup(c => c.ListSharedWithMeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                SharedContact(contactId, "Jane Doe", userId, DateTime.UtcNow),
                SharedContact(Guid.CreateVersion7(), "Bob Smith", Guid.CreateVersion7(), DateTime.UtcNow)
            ]);

        var provider = BuildProvider(client.Object);
        var items = await provider.ListAsync(userId);

        // The share the caller created themselves (e.g. to their own team) is not surfaced.
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual("Bob Smith", items.Single().Title);
    }

    [TestMethod]
    public async Task ListAsync_NoShares_ReturnsEmpty()
    {
        var client = new Mock<IContactsApiClient>();
        client
            .Setup(c => c.ListSharedWithMeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var provider = BuildProvider(client.Object);
        var items = await provider.ListAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, items.Count);
        Assert.AreEqual(0, await provider.CountAsync(Guid.CreateVersion7()));
    }

    [TestMethod]
    public async Task ListAsync_ModuleClientThrows_ReturnsEmpty()
    {
        var client = new Mock<IContactsApiClient>();
        client
            .Setup(c => c.ListSharedWithMeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Contacts module unavailable"));

        var provider = BuildProvider(client.Object);
        var items = await provider.ListAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, items.Count);
        Assert.AreEqual(0, await provider.CountAsync(Guid.CreateVersion7()));
    }
}

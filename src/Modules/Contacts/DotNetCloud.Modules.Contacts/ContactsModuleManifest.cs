using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Contacts;

/// <summary>
/// Manifest for the Contacts module.
/// Declares identity, capabilities, and event contracts for the module system.
/// </summary>
public sealed class ContactsModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.contacts";

    /// <inheritdoc />
    public string Name => "Contacts";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.INotificationService),
        nameof(Core.Capabilities.IUserDirectory),
        nameof(Core.Capabilities.ICurrentUserContext)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(ContactCreatedEvent),
        nameof(ContactUpdatedEvent),
        nameof(ContactDeletedEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => [];
}

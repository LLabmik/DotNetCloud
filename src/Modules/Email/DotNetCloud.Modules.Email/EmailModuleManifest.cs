using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Email;

/// <summary>
/// Module manifest for the Email module.
/// Declares capabilities, published events, and subscribed events.
/// </summary>
public sealed class EmailModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.email";

    /// <inheritdoc />
    public string Name => "Email";

    /// <inheritdoc />
    public string Version => "1.1.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.IAuditLogger),
        nameof(Core.Capabilities.ISearchProvider)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(EmailAccountAddedEvent),
        nameof(EmailAccountRemovedEvent),
        nameof(EmailThreadCreatedEvent),
        nameof(EmailMessageReceivedEvent),
        nameof(EmailSentEvent),
        nameof(EmailRuleTriggeredEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => [];
}

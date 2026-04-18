using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.AI;

/// <summary>
/// Module manifest for the AI Assistant module.
/// Declares capabilities, published events, and subscribed events.
/// </summary>
public sealed class AiModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.ai";

    /// <inheritdoc />
    public string Name => "AI Assistant";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.ICurrentUserContext),
        nameof(Core.Capabilities.IAuditLogger),
        nameof(Core.Capabilities.IUserDirectory),
        nameof(Core.Capabilities.INotificationService),
        nameof(Core.Capabilities.ILlmProvider)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(ConversationCreatedEvent),
        nameof(ConversationMessageEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => Array.Empty<string>();
}

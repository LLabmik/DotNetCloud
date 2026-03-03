using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Example.Events;

namespace DotNetCloud.Modules.Example;

/// <summary>
/// Manifest for the Example module.
/// Declares identity, capabilities, and event contracts for the module system.
/// </summary>
public sealed class ExampleModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.example";

    /// <inheritdoc />
    public string Name => "Example";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.INotificationService),
        nameof(Core.Capabilities.IStorageProvider)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(NoteCreatedEvent),
        nameof(NoteDeletedEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => [];
}

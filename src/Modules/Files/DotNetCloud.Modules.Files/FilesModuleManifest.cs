using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Files.Events;

namespace DotNetCloud.Modules.Files;

/// <summary>
/// Manifest for the Files module.
/// Declares identity, capabilities, and event contracts for the module system.
/// </summary>
public sealed class FilesModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.files";

    /// <inheritdoc />
    public string Name => "Files";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.INotificationService),
        nameof(Core.Capabilities.IStorageProvider),
        nameof(Core.Capabilities.ITeamDirectory),
        nameof(Core.Capabilities.IGroupDirectory),
        nameof(Core.Capabilities.IUserDirectory),
        nameof(Core.Capabilities.ICurrentUserContext)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(FileUploadedEvent),
        nameof(FileDeletedEvent),
        nameof(FileMovedEvent),
        nameof(FileSharedEvent),
        nameof(FileRestoredEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => [];
}

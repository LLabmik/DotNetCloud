using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Photos;

/// <summary>
/// Manifest for the Photos module.
/// Declares identity, capabilities, and event contracts for the module system.
/// </summary>
public sealed class PhotosModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.photos";

    /// <inheritdoc />
    public string Name => "Photos";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.IStorageProvider),
        nameof(Core.Capabilities.IUserDirectory),
        nameof(Core.Capabilities.ICurrentUserContext),
        nameof(Core.Capabilities.INotificationService)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(PhotoUploadedEvent),
        nameof(PhotoDeletedEvent),
        nameof(AlbumCreatedEvent),
        nameof(AlbumSharedEvent),
        nameof(PhotoEditedEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        "FileUploadedEvent"
    };
}

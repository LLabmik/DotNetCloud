using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Video;

/// <summary>
/// Manifest for the Video module.
/// Declares identity, capabilities, and event contracts for the module system.
/// </summary>
public sealed class VideoModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.video";

    /// <inheritdoc />
    public string Name => "Video";

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
        nameof(VideoAddedEvent),
        nameof(VideoDeletedEvent),
        nameof(VideoWatchedEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        "FileUploadedEvent"
    };
}

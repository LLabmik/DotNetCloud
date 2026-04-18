using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Music;

/// <summary>
/// Manifest for the Music module.
/// Declares identity, capabilities, and event contracts for the module system.
/// </summary>
public sealed class MusicModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.music";

    /// <inheritdoc />
    public string Name => "Music";

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
        nameof(TrackPlayedEvent),
        nameof(PlaylistCreatedEvent),
        nameof(LibraryScanCompletedEvent),
        nameof(TrackScrobbledEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        "FileUploadedEvent"
    };
}

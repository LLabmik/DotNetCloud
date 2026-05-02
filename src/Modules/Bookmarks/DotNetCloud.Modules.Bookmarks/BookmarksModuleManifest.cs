using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Bookmarks;

/// <summary>
/// Module manifest for the Bookmarks module.
/// Declares capabilities, published events, and subscribed events.
/// </summary>
public sealed class BookmarksModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.bookmarks";

    /// <inheritdoc />
    public string Name => "Bookmarks";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.INotificationService),
        nameof(Core.Capabilities.ICurrentUserContext),
        nameof(Core.Capabilities.IAuditLogger)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(BookmarkCreatedEvent),
        nameof(BookmarkUpdatedEvent),
        nameof(BookmarkDeletedEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => [];
}

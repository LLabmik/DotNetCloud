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
    public string Version => "1.2.0";

    /// <inheritdoc />
    /// <remarks>
    /// Audit is the only core capability the module reaches for: search indexing goes through
    /// <c>SearchIndexRequestEvent</c> on the event bus, and bookmarks never touch user identity or
    /// notifications. Declaring capabilities the code does not use forces scopes a user cannot be
    /// asked to consent to meaningfully, and the store review rejects it.
    /// </remarks>
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
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

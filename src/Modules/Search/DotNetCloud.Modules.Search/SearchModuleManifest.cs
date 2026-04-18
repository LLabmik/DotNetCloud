using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Search;

/// <summary>
/// Manifest for the Search module.
/// Declares identity, capabilities, and event contracts for the module system.
/// </summary>
public sealed class SearchModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.search";

    /// <inheritdoc />
    public string Name => "Search";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.ISearchableModule),
        nameof(Core.Events.IEventBus)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(SearchIndexCompletedEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        nameof(SearchIndexRequestEvent)
    };
}

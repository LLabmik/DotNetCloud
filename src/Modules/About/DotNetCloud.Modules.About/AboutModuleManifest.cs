using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.About;

/// <summary>
/// Manifest for the About module. Declares identity for the module system.
/// No capabilities or events are required — this module only displays static content.
/// </summary>
public sealed class AboutModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.about";

    /// <inheritdoc />
    public string Name => "About";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => [];

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => [];

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => [];
}

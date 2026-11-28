using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.MyModule;

/// <summary>
/// The code-side view of <c>manifest.json</c>. <c>manifest.json</c> is the contract the core and the
/// packer read; this type carries the same identity and declarations into the running process so the
/// lifecycle service can answer <c>GetManifest</c> without parsing a file.
/// </summary>
/// <remarks>
/// Keep the two in step. The packer validates the JSON, and the installed runtime validates it again
/// on discovery, so a mismatch fails loudly rather than silently running with a different identity.
/// </remarks>
public sealed class MyModuleManifest : IModuleManifest
{
    /// <summary>The module id. Must be lowercase, reverse-DNS, and equal to the host assembly name.</summary>
    public const string ModuleId = "org.example.mymodule";

    /// <summary>The module version. Must equal the <c>version</c> in <c>manifest.json</c>.</summary>
    public const string ModuleVersion = "0.1.0";

    /// <inheritdoc />
    public string Id => ModuleId;

    /// <inheritdoc />
    public string Name => "MyModule";

    /// <inheritdoc />
    public string Version => ModuleVersion;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => [];

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => [];

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => [];
}

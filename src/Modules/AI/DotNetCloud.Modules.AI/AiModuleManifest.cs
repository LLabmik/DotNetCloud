using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.AI;

/// <summary>
/// Module manifest for the AI Assistant module.
/// Declares capabilities and the (currently empty) event surface.
/// </summary>
/// <remarks>
/// Audit logging and the module settings plane are the only core capabilities the module reaches for:
/// conversations live in the module's own schema, provider settings are read through <c>GetSetting</c>
/// (gated by <c>IModuleSettings</c>), and the module raises no cross-module events. Declaring
/// capabilities or events the code does not use forces consent scopes a user cannot be asked to
/// approve meaningfully, and the store review rejects it.
/// </remarks>
public sealed class AiModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.ai";

    /// <inheritdoc />
    public string Name => "AI Assistant";

    /// <inheritdoc />
    public string Version => "2.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.IAuditLogger),
        nameof(Core.Capabilities.IModuleSettings)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => [];

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => [];
}

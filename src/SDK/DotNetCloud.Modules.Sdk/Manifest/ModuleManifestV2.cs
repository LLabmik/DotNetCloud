using System.Text.Json.Serialization;

namespace DotNetCloud.Modules.Sdk.Manifest;

/// <summary>
/// A version 2 module manifest — the only manifest shape a sandboxed (non-required) module uses.
/// </summary>
/// <remarks>
/// <para>
/// The core parses manifests strictly (unknown fields are an error) because the manifest is what an
/// operator reviews before a module is granted capabilities or a database schema. This SDK model
/// mirrors that contract so a module author can validate a manifest locally, before packaging.
/// </para>
/// <para>
/// Required modules keep their version 1 manifests; they are shipped with the core and never go
/// through the sandbox.
/// </para>
/// </remarks>
public sealed record ModuleManifestV2
{
    /// <summary>Manifest schema version. Must be 2.</summary>
    [JsonPropertyName("manifestVersion")]
    public int ManifestVersion { get; init; } = 2;

    /// <summary>Reverse-DNS module identifier; must equal the install directory name.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Semantic version.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>Short description shown in the admin UI and the store.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Publisher identity.</summary>
    [JsonPropertyName("publisher")]
    public ModulePublisher? Publisher { get; init; }

    /// <summary>Module API contract version the module was built against.</summary>
    [JsonPropertyName("moduleApiVersion")]
    public int ModuleApiVersion { get; init; } = 1;

    /// <summary>Minimum core version this module runs on.</summary>
    [JsonPropertyName("minCoreVersion")]
    public string? MinCoreVersion { get; init; }

    /// <summary>Maximum core version this module runs on.</summary>
    [JsonPropertyName("maxCoreVersion")]
    public string? MaxCoreVersion { get; init; }

    /// <summary>
    /// <c>isolated</c> (the core provisions a scoped schema and principal) or <c>none</c>
    /// (the module persists only settings and its own data directory). Defaults to <c>none</c>.
    /// </summary>
    [JsonPropertyName("dataStore")]
    public string DataStore { get; init; } = ModuleDataStoreModes.None;

    /// <summary>
    /// Whether the module implements <c>EraseUserData</c>. Required whenever the module stores user
    /// data or asks for a consent scope.
    /// </summary>
    [JsonPropertyName("handlesUserErasure")]
    public bool HandlesUserErasure { get; init; }

    /// <summary>Capability interfaces the module needs (for example <c>INotificationService</c>).</summary>
    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>Per-user consent scopes (for example <c>files:read</c>).</summary>
    [JsonPropertyName("userConsentScopes")]
    public IReadOnlyList<string> UserConsentScopes { get; init; } = [];

    /// <summary>Event declarations.</summary>
    [JsonPropertyName("events")]
    public ModuleEvents? Events { get; init; }

    /// <summary>UI descriptor; <c>iframe</c> is the only supported type.</summary>
    [JsonPropertyName("ui")]
    public ModuleUi? Ui { get; init; }

    /// <summary>Requested resource limits. Requests are capped by admin/store policy.</summary>
    [JsonPropertyName("resources")]
    public ModuleResources? Resources { get; init; }

    /// <summary>Restart policy (<c>always</c>, <c>on-failure</c>, <c>never</c>, <c>backoff</c>).</summary>
    [JsonPropertyName("restartPolicy")]
    public string? RestartPolicy { get; init; }
}

/// <summary>Publisher identity.</summary>
/// <param name="Id">Publisher id (stable, lowercase).</param>
/// <param name="Name">Display name.</param>
/// <param name="Url">Optional homepage.</param>
public sealed record ModulePublisher(string Id, string Name, string? Url);

/// <summary>Event declarations.</summary>
/// <param name="Publishes">Event types the module raises; the core rejects anything else.</param>
/// <param name="Subscribes">Event types the module consumes.</param>
public sealed record ModuleEvents(IReadOnlyList<string> Publishes, IReadOnlyList<string> Subscribes)
{
    /// <summary>Gets an empty declaration.</summary>
    public static ModuleEvents Empty { get; } = new([], []);
}

/// <summary>Module UI descriptor.</summary>
/// <param name="Type">UI type; only <c>iframe</c> is accepted for non-required modules.</param>
/// <param name="EntryPath">Path the core proxies to (relative to the module root).</param>
/// <param name="NavItems">Navigation entries contributed to the core chrome.</param>
/// <param name="Widget">Optional home-page widget contributed by the module.</param>
public sealed record ModuleUi(
    string Type,
    string EntryPath,
    IReadOnlyList<ModuleNavItem> NavItems,
    ModuleWidget? Widget = null);

/// <summary>A home-page widget contributed by a module.</summary>
/// <param name="Path">Module-relative path the widget frame loads (for example <c>/widget/index.html</c>).</param>
public sealed record ModuleWidget(string Path);

/// <summary>A navigation entry contributed by a module.</summary>
/// <param name="Id">Stable id, unique within the module.</param>
/// <param name="Label">Display label.</param>
/// <param name="Icon">Material Icons ligature name (never an emoji).</param>
/// <param name="Path">Module-relative path to open.</param>
public sealed record ModuleNavItem(string Id, string Label, string Icon, string Path);

/// <summary>Requested resource limits (never entitlements).</summary>
/// <param name="MemoryLimitMb">Requested memory ceiling in MB.</param>
/// <param name="CpuPercent">Requested CPU share of one core.</param>
/// <param name="DataDirMb">Requested data-directory ceiling in MB.</param>
/// <param name="DatabaseMb">Requested database-schema ceiling in MB.</param>
/// <param name="ConnectionLimit">Requested database connection limit.</param>
public sealed record ModuleResources(
    int? MemoryLimitMb,
    int? CpuPercent,
    int? DataDirMb,
    int? DatabaseMb,
    int? ConnectionLimit);

/// <summary>The values <see cref="ModuleManifestV2.DataStore"/> accepts.</summary>
public static class ModuleDataStoreModes
{
    /// <summary>The core provisions a schema and principal that only this module can use.</summary>
    public const string Isolated = "isolated";

    /// <summary>The module has no database and persists only settings/data files.</summary>
    public const string None = "none";
}

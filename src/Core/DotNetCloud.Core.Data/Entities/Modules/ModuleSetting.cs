namespace DotNetCloud.Core.Data.Entities.Modules;

/// <summary>
/// Persisted key/value settings owned by a single module.
/// </summary>
/// <remarks>
/// <para>
/// This is the persistence plane for modules that declare <c>dataStore: "none"</c> and the
/// backing store for the <c>IModuleSettings</c> capability RPCs. Keys are namespaced by the
/// <em>authenticated</em> module identity: a module can never read or write another module's keys,
/// nor the admin-only <c>SystemSettings</c> table.
/// </para>
/// <para>
/// Values marked <see cref="IsSecret"/> are encrypted-at-rest candidates; they are never returned
/// by list operations and are masked in the admin UI.
/// </para>
/// </remarks>
public class ModuleSetting
{
    /// <summary>Owning module id. Part of the composite primary key.</summary>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Setting key. Part of the composite primary key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Serialized value (plain text or JSON).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Whether the value is sensitive and must be masked in listings.</summary>
    public bool IsSecret { get; set; }

    /// <summary>When the value last changed.</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>The user who last changed the value, when a user initiated the change.</summary>
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>Navigation to the owning module.</summary>
    public virtual InstalledModule? Module { get; set; }
}

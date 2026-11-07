namespace DotNetCloud.Core.Data.Entities.Modules;

/// <summary>
/// Provisioned database store for a sandboxed module: the module's own schema, the scoped
/// principal that owns it, and the connection string the module process receives.
/// </summary>
/// <remarks>
/// <para>
/// One row exists per module that declares <c>dataStore: "isolated"</c>. The connection string is
/// encrypted with DataProtection (purpose <c>DotNetCloud.Sandbox.ModuleDataStore</c>) and is never
/// returned to any surface other than the module's own launch environment.
/// </para>
/// <para>
/// Modules that declare <c>dataStore: "none"</c> have no row here; they persist through
/// <see cref="ModuleSetting"/>.
/// </para>
/// <para>
/// There is deliberately <b>no foreign key</b> to <see cref="InstalledModule"/>: uninstalling a
/// module with "keep data" deletes the installation row but retains this row with
/// <see cref="ModuleDataStoreStatus.Orphaned"/> so an administrator can see and drop the leftover
/// schema. A cascade would silently destroy the data the operator chose to keep.
/// </para>
/// </remarks>
public class ModuleDataStore
{
    /// <summary>Primary key and owner of the store.</summary>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Database engine the store lives in (<c>PostgreSQL</c> or <c>SqlServer</c>).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Schema owned by the module principal.</summary>
    public string SchemaName { get; set; } = string.Empty;

    /// <summary>Database role / contained user that owns the schema.</summary>
    public string PrincipalName { get; set; } = string.Empty;

    /// <summary>DataProtection-encrypted connection string for the module process, or null until provisioned.</summary>
    public string? EncryptedConnectionString { get; set; }

    /// <summary>Current provisioning state.</summary>
    public ModuleDataStoreStatus Status { get; set; } = ModuleDataStoreStatus.Pending;

    /// <summary>Sanitized administrator-facing failure reason. Must never contain secrets.</summary>
    public string? LastError { get; set; }

    /// <summary>When the store was first provisioned.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>When the row last changed.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}

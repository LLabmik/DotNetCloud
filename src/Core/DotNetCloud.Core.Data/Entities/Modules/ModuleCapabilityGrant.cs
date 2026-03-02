namespace DotNetCloud.Core.Data.Entities.Modules;

/// <summary>
/// Represents a capability grant to an installed module.
/// Tracks which capabilities (IUserDirectory, IStorageProvider, etc.) are granted to modules and when.
/// </summary>
/// <remarks>
/// <para><strong>Purpose:</strong></para>
/// <list type="bullet">
///   <item><description>Enforce capability-based security at the database level</description></item>
///   <item><description>Track when and by whom capabilities were granted to modules</description></item>
///   <item><description>Enable runtime capability validation (module cannot access capability without grant)</description></item>
///   <item><description>Support capability revocation and auditing</description></item>
///   <item><description>Provide UI for admins to review and manage module permissions</description></item>
/// </list>
/// <para><strong>Capability System Overview:</strong></para>
/// <para>DotNetCloud uses a capability-based security model where modules must explicitly request
/// and receive capabilities to access core services. Capabilities are organized by tier:</para>
/// <list type="bullet">
///   <item><description><strong>Public Tier:</strong> Always granted, no approval needed (IUserDirectory, IEventBus, etc.)</description></item>
///   <item><description><strong>Restricted Tier:</strong> Requires admin approval (IStorageProvider, IModuleSettings, etc.)</description></item>
///   <item><description><strong>Privileged Tier:</strong> Highly sensitive, requires explicit admin approval (IUserManager, IBackupProvider, etc.)</description></item>
///   <item><description><strong>Forbidden Tier:</strong> Never granted to modules (internal core use only)</description></item>
/// </list>
/// <para><strong>Grant Workflow:</strong></para>
/// <code>
/// // Module manifest declares required capabilities
/// public class FilesModuleManifest : IModuleManifest
/// {
///     public IReadOnlyCollection&lt;string&gt; RequiredCapabilities => new[]
///     {
///         "IUserDirectory",        // Public tier - auto-granted
///         "IStorageProvider",      // Restricted tier - needs approval
///         "ICurrentUserContext"    // Public tier - auto-granted
///     };
/// }
/// 
/// // Admin reviews and grants restricted capabilities
/// var grant = new ModuleCapabilityGrant
/// {
///     ModuleId = "dotnetcloud.files",
///     CapabilityName = "IStorageProvider",
///     GrantedAt = DateTime.UtcNow,
///     GrantedByUserId = adminUserId
/// };
/// </code>
/// <para><strong>Security Considerations:</strong></para>
/// <list type="bullet">
///   <item><description>Grants are immutable once created (create new grant if capability tier changes)</description></item>
///   <item><description>Module cannot start if required capabilities are not granted</description></item>
///   <item><description>All capability access is logged for security audits</description></item>
///   <item><description>Revoking a grant requires module restart to take effect</description></item>
/// </list>
/// </remarks>
public class ModuleCapabilityGrant
{
    /// <summary>
    /// Unique identifier for this capability grant.
    /// </summary>
    /// <remarks>
    /// Primary key. Auto-generated on insert.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    /// The module that is being granted this capability (e.g., "dotnetcloud.files").
    /// </summary>
    /// <remarks>
    /// <para>Foreign key to InstalledModule. Required field.</para>
    /// <para>Cascade delete enabled: When module is uninstalled, all grants are removed.</para>
    /// <para>Maximum length: 200 characters (matches InstalledModule.ModuleId).</para>
    /// </remarks>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the capability interface being granted (e.g., "IStorageProvider", "IUserDirectory").
    /// </summary>
    /// <remarks>
    /// <para>Maximum length: 200 characters.</para>
    /// <para><strong>Public Tier Capabilities</strong> (auto-granted, no record needed):</para>
    /// <list type="bullet">
    ///   <item><description><strong>IUserDirectory:</strong> Query user information (read-only)</description></item>
    ///   <item><description><strong>ICurrentUserContext:</strong> Get current caller context</description></item>
    ///   <item><description><strong>INotificationService:</strong> Send notifications to users</description></item>
    ///   <item><description><strong>IEventBus:</strong> Publish/subscribe to events</description></item>
    /// </list>
    /// <para><strong>Restricted Tier Capabilities</strong> (require admin approval, recorded here):</para>
    /// <list type="bullet">
    ///   <item><description><strong>IStorageProvider:</strong> File storage operations (read/write/delete)</description></item>
    ///   <item><description><strong>IModuleSettings:</strong> Module configuration storage</description></item>
    ///   <item><description><strong>ITeamDirectory:</strong> Team information access</description></item>
    /// </list>
    /// <para><strong>Privileged Tier Capabilities</strong> (highly sensitive, explicit approval required):</para>
    /// <list type="bullet">
    ///   <item><description><strong>IUserManager:</strong> Create/disable users (admin operations)</description></item>
    ///   <item><description><strong>IBackupProvider:</strong> System backup operations</description></item>
    /// </list>
    /// <para>Unique constraint: (ModuleId, CapabilityName) - one grant per capability per module.</para>
    /// </remarks>
    public string CapabilityName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this capability was granted to the module.
    /// </summary>
    /// <remarks>
    /// Auto-set on insert. Immutable. Used for audit trails.
    /// </remarks>
    public DateTime GrantedAt { get; set; }

    /// <summary>
    /// User ID of the administrator who granted this capability.
    /// Null for auto-granted public tier capabilities (system-granted).
    /// </summary>
    /// <remarks>
    /// <para>Optional foreign key to ApplicationUser.</para>
    /// <para>Null indicates system-granted capability (public tier or initial installation).</para>
    /// <para>Non-null indicates explicit admin approval (restricted/privileged tiers).</para>
    /// <para>Used for accountability and audit trails ("Who granted IUserManager to module X?").</para>
    /// <para>No cascade delete: If admin is deleted, grant remains (audit trail preserved).</para>
    /// </remarks>
    public Guid? GrantedByUserId { get; set; }

    // ==================== Navigation Properties ====================

    /// <summary>
    /// Navigation property to the module that received this capability grant.
    /// </summary>
    /// <remarks>
    /// EF Core relationship: Many ModuleCapabilityGrant -> One InstalledModule.
    /// Cascade delete enabled.
    /// </remarks>
    public virtual InstalledModule Module { get; set; } = null!;

    /// <summary>
    /// Navigation property to the admin user who granted this capability.
    /// Null if system-granted (public tier capability).
    /// </summary>
    /// <remarks>
    /// EF Core relationship: Many ModuleCapabilityGrant -> One ApplicationUser (optional).
    /// No cascade delete (preserve audit trail if admin is deleted).
    /// </remarks>
    public virtual ApplicationUser? GrantedByUser { get; set; }
}

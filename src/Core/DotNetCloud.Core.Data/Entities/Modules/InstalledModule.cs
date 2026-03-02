using System.ComponentModel.DataAnnotations;

namespace DotNetCloud.Core.Data.Entities.Modules;

/// <summary>
/// Represents a module installed in the DotNetCloud system.
/// Tracks module version, status, and installation metadata.
/// </summary>
/// <remarks>
/// <para><strong>Purpose:</strong></para>
/// <list type="bullet">
///   <item><description>Track which modules are installed in the system</description></item>
///   <item><description>Monitor module versions for update management</description></item>
///   <item><description>Track module status (Enabled, Disabled, UpdateAvailable, Failed)</description></item>
///   <item><description>Enable/disable modules at runtime without uninstalling</description></item>
///   <item><description>Support module update notifications and rollback</description></item>
/// </list>
/// <para><strong>Module Lifecycle States:</strong></para>
/// <list type="bullet">
///   <item><description><strong>Enabled:</strong> Module is active and processing requests</description></item>
///   <item><description><strong>Disabled:</strong> Module is installed but not running (manually disabled by admin)</description></item>
///   <item><description><strong>UpdateAvailable:</strong> Newer version detected, pending admin approval</description></item>
///   <item><description><strong>Failed:</strong> Module crashed or failed health checks, requires admin intervention</description></item>
///   <item><description><strong>Installing:</strong> Module installation in progress</description></item>
///   <item><description><strong>Uninstalling:</strong> Module removal in progress</description></item>
/// </list>
/// <para><strong>Usage Patterns:</strong></para>
/// <code>
/// // Installing a new module
/// var module = new InstalledModule
/// {
///     ModuleId = "dotnetcloud.files",
///     Version = "1.0.0",
///     Status = "Enabled",
///     InstalledAt = DateTime.UtcNow
/// };
/// 
/// // Update available notification
/// module.Status = "UpdateAvailable";
/// // Admin approves update, version changes to 1.1.0, status back to Enabled
/// 
/// // Disabling a module
/// module.Status = "Disabled";
/// // Process supervisor stops module, but keeps data intact
/// </code>
/// <para><strong>Module ID Convention:</strong></para>
/// <para>Module IDs follow reverse domain name convention:</para>
/// <list type="bullet">
///   <item><description><strong>dotnetcloud.files</strong> - Core Files module</description></item>
///   <item><description><strong>dotnetcloud.chat</strong> - Core Chat module</description></item>
///   <item><description><strong>dotnetcloud.calendar</strong> - Core Calendar module</description></item>
///   <item><description><strong>org.example.customapp</strong> - Third-party custom module</description></item>
/// </list>
/// </remarks>
public class InstalledModule
{
    /// <summary>
    /// Unique identifier for the module (e.g., "dotnetcloud.files", "dotnetcloud.chat").
    /// This is the primary key.
    /// </summary>
    /// <remarks>
    /// <para>Maximum length: 200 characters.</para>
    /// <para>Convention: Reverse domain name notation (e.g., dotnetcloud.modulename).</para>
    /// <para>Examples:</para>
    /// <list type="bullet">
    ///   <item><description><strong>dotnetcloud.files</strong> - File storage and sync</description></item>
    ///   <item><description><strong>dotnetcloud.chat</strong> - Real-time messaging</description></item>
    ///   <item><description><strong>dotnetcloud.calendar</strong> - Calendar and events</description></item>
    ///   <item><description><strong>dotnetcloud.deck</strong> - Project management</description></item>
    ///   <item><description><strong>dotnetcloud.ai</strong> - AI assistant</description></item>
    /// </list>
    /// <para>Immutable after installation.</para>
    /// </remarks>
    [Key]
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>
    /// Semantic version of the installed module (e.g., "1.2.3", "2.0.0-beta.1").
    /// </summary>
    /// <remarks>
    /// <para>Maximum length: 50 characters.</para>
    /// <para>Follows <see href="https://semver.org/">Semantic Versioning 2.0.0</see>:</para>
    /// <list type="bullet">
    ///   <item><description><strong>MAJOR.MINOR.PATCH</strong> (e.g., "1.2.3")</description></item>
    ///   <item><description><strong>Pre-release:</strong> "1.0.0-alpha", "2.0.0-beta.1"</description></item>
    ///   <item><description><strong>Build metadata:</strong> "1.0.0+20230101"</description></item>
    /// </list>
    /// <para>Updated when module is upgraded. Previous version not tracked (use audit logs for history).</para>
    /// </remarks>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the module (Enabled, Disabled, UpdateAvailable, Failed, etc.).
    /// </summary>
    /// <remarks>
    /// <para>Maximum length: 50 characters.</para>
    /// <para><strong>Valid Status Values:</strong></para>
    /// <list type="bullet">
    ///   <item><description><strong>Enabled:</strong> Module is running and processing requests</description></item>
    ///   <item><description><strong>Disabled:</strong> Module is stopped by admin (can be re-enabled)</description></item>
    ///   <item><description><strong>UpdateAvailable:</strong> Newer version detected, awaiting admin approval</description></item>
    ///   <item><description><strong>Failed:</strong> Module crashed or failed health checks (requires admin action)</description></item>
    ///   <item><description><strong>Installing:</strong> Module installation in progress (transient state)</description></item>
    ///   <item><description><strong>Uninstalling:</strong> Module removal in progress (transient state)</description></item>
    ///   <item><description><strong>Updating:</strong> Module update in progress (transient state)</description></item>
    /// </list>
    /// <para><strong>State Transitions:</strong></para>
    /// <para>Installing → Enabled → Disabled ↔ Enabled → Uninstalling</para>
    /// <para>Enabled → UpdateAvailable → Updating → Enabled</para>
    /// <para>Any state → Failed (on error, requires admin intervention)</para>
    /// </remarks>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the module was first installed.
    /// </summary>
    /// <remarks>
    /// Auto-set on insert. Immutable. Preserved across updates.
    /// </remarks>
    public DateTime InstalledAt { get; set; }

    /// <summary>
    /// Timestamp of the last update to this module record (version change, status change, etc.).
    /// </summary>
    /// <remarks>
    /// Auto-updated on any change to Version or Status.
    /// Used for audit trails and update tracking.
    /// </remarks>
    public DateTime UpdatedAt { get; set; }

    // ==================== Navigation Properties ====================

    /// <summary>
    /// Navigation property to the capability grants for this module.
    /// </summary>
    /// <remarks>
    /// <para>One-to-many relationship: One InstalledModule has many ModuleCapabilityGrant records.</para>
    /// <para>Cascade delete enabled: When module is uninstalled, all capability grants are removed.</para>
    /// <para>Used to track which capabilities (IUserDirectory, IStorageProvider, etc.) are granted to this module.</para>
    /// </remarks>
    public virtual ICollection<ModuleCapabilityGrant> CapabilityGrants { get; set; } = new List<ModuleCapabilityGrant>();
}

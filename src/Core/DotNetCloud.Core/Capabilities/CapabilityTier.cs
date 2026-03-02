namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Defines the approval sensitivity tier for a capability.
/// 
/// The tier determines how permissive the capability is and what approval process
/// is required before granting it to a module.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tier Hierarchy:</b>
/// 
/// Tiers are ordered by sensitivity: <c>Public &lt; Restricted &lt; Privileged &lt; Forbidden</c>
/// </para>
/// 
/// <para>
/// <b>Approval Process by Tier:</b>
/// 
/// <list type="table">
///   <listheader>
///     <term>Tier</term>
///     <description>Approval Process</description>
///     <description>Examples</description>
///   </listheader>
///   <item>
///     <term><see cref="Public"/></term>
///     <description>
///       Automatic. All modules receive these capabilities without any approval needed.
///       These are safe operations that don't expose sensitive data or grant elevated privileges.
///     </description>
///     <description>
///       <c>IUserDirectory</c> (read-only public user info),
///       <c>ICurrentUserContext</c> (identify current caller),
///       <c>INotificationService</c> (send notifications),
///       <c>IEventBus</c> (publish/subscribe to events)
///     </description>
///   </item>
///   <item>
///     <term><see cref="Restricted"/></term>
///     <description>
///       Manual review by administrator. Module source code and manifest are reviewed
///       to ensure the capability usage is legitimate and doesn't pose a risk.
///       Admin can approve or deny based on assessment.
///     </description>
///     <description>
///       <c>IStorageProvider</c> (access file storage),
///       <c>IModuleSettings</c> (read/write configuration),
///       <c>ITeamDirectory</c> (access team hierarchy)
///     </description>
///   </item>
///   <item>
///     <term><see cref="Privileged"/></term>
///     <description>
///       Manual security review required. Involves security audit and explicit approval
///       from authorized personnel. Reserved for operations that affect system integrity,
///       user accounts, or sensitive operations.
///     </description>
///     <description>
///       <c>IUserManager</c> (create/disable users),
///       <c>IBackupProvider</c> (backup and restore operations)
///     </description>
///   </item>
///   <item>
///     <term><see cref="Forbidden"/></term>
///     <description>
///       Never granted. These operations are fundamentally incompatible with the module
///       security model and are never exposed to any module. Core services may offer
///       safe subsets through specific capability interfaces.
///     </description>
///     <description>
///       Direct database access,
///       System-level file operations outside storage provider,
///       Process spawning,
///       Network bypass operations,
///       Module process termination
///     </description>
///   </item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Usage Pattern:</b>
/// 
/// <code>
/// // Module manifest declares what it needs
/// public class MyModuleManifest : IModuleManifest
/// {
///     public IReadOnlyCollection&lt;string&gt; RequiredCapabilities =&gt; new[]
///     {
///         nameof(IStorageProvider),      // Restricted tier
///         nameof(INotificationService)   // Public tier
///     };
/// }
/// 
/// // During initialization, capabilities are injected
/// public class MyModule : IModule
/// {
///     private readonly IStorageProvider? _storage;      // May be null if not granted
///     private readonly INotificationService _notify;   // Always non-null (public)
///     
///     public MyModule(IStorageProvider? storage, INotificationService notify)
///     {
///         _storage = storage;
///         _notify = notify;
///     }
///     
///     public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken ct)
///     {
///         // Gracefully handle missing restricted capability
///         if (_storage == null)
///         {
///             Console.WriteLine("Warning: Storage capability not granted. File operations disabled.");
///             return;
///         }
///         
///         // Use capability
///         await _storage.UploadAsync("file.txt", stream, ct);
///     }
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Granting Capabilities to Modules:</b>
/// 
/// Administrators use the admin dashboard or CLI to grant capabilities:
/// 
/// <code>
/// // CLI example
/// dotnetcloud admin grant-capability dotnetcloud.files IStorageProvider
/// 
/// // REST API example
/// POST /api/v1/core/admin/modules/dotnetcloud.files/capabilities/IStorageProvider/grant
/// </code>
/// </para>
/// </remarks>
/// <seealso cref="ICapabilityInterface"/>
/// <seealso cref="DotNetCloud.Core.Modules.IModuleManifest"/>
public enum CapabilityTier
{
    /// <summary>
    /// Public tier capability.
    /// 
    /// Automatically granted to all modules. These are safe operations that don't expose
    /// sensitive data or grant elevated privileges. Examples: <c>INotificationService</c>,
    /// <c>IUserDirectory</c>, <c>ICurrentUserContext</c>, <c>IEventBus</c>.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Restricted tier capability.
    /// 
    /// Requires explicit administrator approval after code review. These capabilities
    /// access user data or system resources. Examples: <c>IStorageProvider</c>,
    /// <c>IModuleSettings</c>, <c>ITeamDirectory</c>.
    /// </summary>
    Restricted = 1,

    /// <summary>
    /// Privileged tier capability.
    /// 
    /// Requires security review and explicit approval. These are highly sensitive operations
    /// that affect system integrity or user accounts. Examples: <c>IUserManager</c>,
    /// <c>IBackupProvider</c>.
    /// </summary>
    Privileged = 2,

    /// <summary>
    /// Forbidden tier.
    /// 
    /// Must never be granted to any module. These operations are incompatible with the
    /// module security model: direct database access, system file operations, process
    /// spawning, network bypass, etc.
    /// </summary>
    Forbidden = 3
}

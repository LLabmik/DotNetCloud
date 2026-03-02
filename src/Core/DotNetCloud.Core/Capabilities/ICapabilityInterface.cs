namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Marker interface that all capability interfaces must implement.
/// 
/// Capability interfaces define secure, fine-grained APIs that modules can request.
/// The DotNetCloud permission system ensures modules can only access capabilities
/// they've been explicitly granted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Design Pattern:</b>
/// 
/// Capability interfaces are organized into four tiers based on approval sensitivity:
/// 
/// <list type="bullet">
///   <item>
///     <description><b>Public:</b> Automatically granted to all modules. 
///     Examples: INotificationService, IUserDirectory, IEventBus.
///     </description>
///   </item>
///   <item>
///     <description><b>Restricted:</b> Requires manual administrator approval. 
///     Examples: IStorageProvider, IModuleSettings, ITeamDirectory.
///     </description>
///   </item>
///   <item>
///     <description><b>Privileged:</b> Requires security review for approval. 
///     Examples: IUserManager, IBackupProvider.
///     </description>
///   </item>
///   <item>
///     <description><b>Forbidden:</b> Never granted to modules. 
///     Examples: Direct database access, system file operations.
///     </description>
///   </item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Implementation Pattern:</b>
/// 
/// Modules request capabilities through dependency injection. If a capability is not
/// granted, the dependency is injected as <c>null</c>, enabling graceful degradation:
/// 
/// <code>
/// public class MyModule : IModule
/// {
///     private readonly IStorageProvider? _storage;      // May be null if not granted
///     private readonly INotificationService _notify;   // Always non-null (public)
///     
///     public MyModule(IStorageProvider? storage, INotificationService notify)
///     {
///         // storage may be null if capability not granted
///         // notify will never be null (public capability, always granted)
///         _storage = storage;
///         _notify = notify;
///     }
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Security Properties:</b>
/// 
/// - <b>Principle of Least Privilege:</b> Modules start with no capabilities
/// - <b>Explicit Grants:</b> Each capability must be explicitly granted by administrator
/// - <b>Auditability:</b> All capability grants are logged and auditable
/// - <b>Fail-Safe Defaults:</b> Missing capabilities default to null, not throwing exceptions
/// </para>
/// 
/// <para>
/// <b>Creating Custom Capabilities:</b>
/// 
/// 1. Define the interface inheriting from this marker:
/// <code>
/// public interface IMyCapability : ICapabilityInterface
/// {
///     Task DoSomethingAsync(CancellationToken cancellationToken);
/// }
/// </code>
/// 
/// 2. Implement the interface in core services
/// 3. Register with appropriate tier in DI container
/// 4. Declare in module manifest
/// </para>
/// </remarks>
/// <seealso cref="CapabilityTier"/>
/// <seealso cref="DotNetCloud.Core.Modules.IModuleManifest"/>
public interface ICapabilityInterface
{
}

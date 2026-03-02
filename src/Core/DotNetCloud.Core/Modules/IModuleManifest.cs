namespace DotNetCloud.Core.Modules;

/// <summary>
/// Defines metadata about a module's capabilities, events, and requirements.
/// 
/// The manifest is the contract that describes what a module provides to the system
/// and what it requires. It enables the system to validate modules, enforce security,
/// and coordinate initialization.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purpose:</b>
/// 
/// The manifest serves multiple functions:
/// 
/// <list type="bullet">
///   <item><description>Declares module identity and version</description></item>
///   <item><description>Specifies required capabilities (for security validation)</description></item>
///   <item><description>Lists published events (for subscription discovery)</description></item>
///   <item><description>Lists subscribed events (for event bus setup)</description></item>
///   <item><description>Enables system to make intelligent initialization decisions</description></item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Example Implementation:</b>
/// 
/// <code>
/// public class DocumentsModuleManifest : IModuleManifest
/// {
///     public string Id =&gt; "dotnetcloud.documents";
///     public string Name =&gt; "Documents";
///     public string Version =&gt; "1.0.0";
///     
///     public IReadOnlyCollection&lt;string&gt; RequiredCapabilities =&gt; new[]
///     {
///         nameof(IStorageProvider),      // Restricted: file storage
///         nameof(INotificationService)   // Public: send notifications
///     };
///     
///     public IReadOnlyCollection&lt;string&gt; PublishedEvents =&gt; new[]
///     {
///         nameof(DocumentCreatedEvent),
///         nameof(DocumentDeletedEvent),
///         nameof(DocumentSharedEvent)
///     };
///     
///     public IReadOnlyCollection&lt;string&gt; SubscribedEvents =&gt; new[]
///     {
///         nameof(UserDeletedEvent),  // Clean up docs when user deleted
///         nameof(TeamDeletedEvent)   // Clean up team docs
///     };
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Validation Rules:</b>
/// 
/// <list type="bullet">
///   <item><description>Id must be non-null, non-empty, lowercase, dot-separated (e.g., "dotnetcloud.files")</description></item>
///   <item><description>Name must be non-null, non-empty, user-friendly</description></item>
///   <item><description>Version must follow semantic versioning (e.g., "1.0.0")</description></item>
///   <item><description>RequiredCapabilities, PublishedEvents, SubscribedEvents must be non-null (may be empty)</description></item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Manifest Definition Approaches:</b>
/// 
/// <list type="number">
///   <item>
///     <description>
///       <b>Class-based (Recommended):</b> Implement IModuleManifest as a class
///       <code>
///       public class MyModuleManifest : IModuleManifest
///       {
///           public string Id =&gt; "my.module";
///           // ...
///       }
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Attribute-based (Future):</b> Decorate module class with manifest information
///       <code>
///       [ModuleManifest("my.module", "My Module", "1.0.0", 
///           RequiredCapabilities = new[] { nameof(IStorageProvider) })]
///       public class MyModule : IModule
///       {
///       }
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Configuration-based (Future):</b> Load from manifest.json file bundled with module
///     </description>
///   </item>
/// </list>
/// </para>
/// </remarks>
public interface IModuleManifest
{
    /// <summary>
    /// Gets the unique identifier for the module.
    /// 
    /// Should be in format "organization.modulename", e.g., "dotnetcloud.files", "dotnetcloud.chat".
    /// Used throughout the system to reference this module.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description>Must be non-null and non-empty</description></item>
    ///   <item><description>Must be lowercase with dots as separators</description></item>
    ///   <item><description>Must be unique across all installed modules</description></item>
    ///   <item><description>Used in capability grants, event routing, and logging</description></item>
    /// </list>
    /// </remarks>
    string Id { get; }

    /// <summary>
    /// Gets the human-readable name of the module.
    /// 
    /// Displayed in admin UI, logs, and user-facing screens.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description>Should be user-friendly (e.g., "Documents", "Chat", "Calendar")</description></item>
    ///   <item><description>Not used for identification, only display</description></item>
    /// </list>
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets the version of the module in semantic versioning format.
    /// 
    /// Format: "MAJOR.MINOR.PATCH" (e.g., "1.0.0", "2.1.3").
    /// Used for compatibility checks and update notifications.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description>Must follow semantic versioning</description></item>
    ///   <item><description>Used to detect upgrades and manage compatibility</description></item>
    ///   <item><description>Breaking changes should increment MAJOR version</description></item>
    /// </list>
    /// </remarks>
    string Version { get; }

    /// <summary>
    /// Gets the collection of capability names required by this module.
    /// 
    /// These capabilities must be available (granted) in the system for the module to
    /// initialize successfully. If a required capability is missing during initialization,
    /// the module should throw an exception in <see cref="IModule.InitializeAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Usage:</b>
    /// 
    /// <code>
    /// public IReadOnlyCollection&lt;string&gt; RequiredCapabilities =&gt; new[]
    /// {
    ///     nameof(IStorageProvider),      // Files module needs storage
    ///     nameof(INotificationService)   // Needs to send notifications
    /// };
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// <b>Best Practices:</b>
    /// 
    /// <list type="bullet">
    ///   <item><description>Use <c>nameof()</c> operator for type safety</description></item>
    ///   <item><description>Only list capabilities actually used by the module</description></item>
    ///   <item><description>Document why each capability is required</description></item>
    ///   <item><description>Consider optional capabilities (inject as null) for non-critical features</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    IReadOnlyCollection<string> RequiredCapabilities { get; }

    /// <summary>
    /// Gets the collection of event type names that this module publishes.
    /// 
    /// Other modules can subscribe to these events to react to significant things
    /// happening within this module. Events enable loose coupling and event-driven
    /// architectures.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Usage:</b>
    /// 
    /// <code>
    /// public IReadOnlyCollection&lt;string&gt; PublishedEvents =&gt; new[]
    /// {
    ///     nameof(DocumentCreatedEvent),
    ///     nameof(DocumentDeletedEvent),
    ///     nameof(DocumentSharedEvent)
    /// };
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// <b>Best Practices:</b>
    /// 
    /// <list type="bullet">
    ///   <item><description>Use <c>nameof()</c> operator for type safety</description></item>
    ///   <item><description>Only publish significant business events</description></item>
    ///   <item><description>Don't publish internal implementation details</description></item>
    ///   <item><description>Event names should be past tense (Created, Deleted, Updated)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    IReadOnlyCollection<string> PublishedEvents { get; }

    /// <summary>
    /// Gets the collection of event type names that this module subscribes to.
    /// 
    /// The module will receive notifications when these events are published by other
    /// modules. Subscriptions are registered during module initialization via the event bus.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Usage:</b>
    /// 
    /// <code>
    /// public IReadOnlyCollection&lt;string&gt; SubscribedEvents =&gt; new[]
    /// {
    ///     nameof(UserDeletedEvent),      // Clean up when user is deleted
    ///     nameof(TeamDeletedEvent)       // Clean up when team is deleted
    /// };
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// <b>Best Practices:</b>
    /// 
    /// <list type="bullet">
    ///   <item><description>Use <c>nameof()</c> operator for type safety</description></item>
    ///   <item><description>Subscribe to events during <see cref="IModule.InitializeAsync"/></description></item>
    ///   <item><description>Unsubscribe during <see cref="IModule.StopAsync"/></description></item>
    ///   <item><description>Handle subscription failures gracefully</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    IReadOnlyCollection<string> SubscribedEvents { get; }
}

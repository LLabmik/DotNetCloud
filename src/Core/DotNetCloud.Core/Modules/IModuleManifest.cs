namespace DotNetCloud.Core.Modules;

/// <summary>
/// Defines metadata about a module's capabilities, events, and requirements.
/// </summary>
public interface IModuleManifest
{
    /// <summary>
    /// Gets the unique identifier for the module.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the human-readable name of the module.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the version of the module in semantic versioning format (e.g., "1.0.0").
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the collection of capability names required by this module.
    /// These capabilities must be available in the current execution context.
    /// </summary>
    IReadOnlyCollection<string> RequiredCapabilities { get; }

    /// <summary>
    /// Gets the collection of event type names that this module publishes.
    /// Other modules can subscribe to these events.
    /// </summary>
    IReadOnlyCollection<string> PublishedEvents { get; }

    /// <summary>
    /// Gets the collection of event type names that this module subscribes to.
    /// The module will receive notifications when these events are published.
    /// </summary>
    IReadOnlyCollection<string> SubscribedEvents { get; }
}

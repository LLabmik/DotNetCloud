namespace DotNetCloud.Core.Modules.Supervisor;

/// <summary>
/// Defines the restart behavior when a module process exits unexpectedly.
/// </summary>
/// <remarks>
/// The process supervisor uses this policy to determine how to handle module crashes.
/// Each module can have its own restart policy configured via the module manifest
/// or administrator settings.
/// </remarks>
public enum RestartPolicy
{
    /// <summary>
    /// Restart the module process immediately after it exits.
    /// Suitable for critical modules that must always be running.
    /// </summary>
    Immediate,

    /// <summary>
    /// Restart the module process with exponential backoff delays.
    /// Delays increase with each consecutive failure: 1s, 2s, 4s, 8s, ... up to a maximum.
    /// Resets after a configurable period of successful operation.
    /// </summary>
    ExponentialBackoff,

    /// <summary>
    /// Do not restart the module automatically. Send an alert to administrators.
    /// Used for modules where automatic restart could cause data corruption or
    /// where manual investigation is required before restarting.
    /// </summary>
    AlertOnly
}

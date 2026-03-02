namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Defines the approval sensitivity tier for a capability.
/// </summary>
public enum CapabilityTier
{
    /// <summary>
    /// Capability is considered safe and can be granted automatically.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Capability requires explicit administrator approval.
    /// </summary>
    Restricted = 1,

    /// <summary>
    /// Capability grants highly sensitive operations and requires explicit approval.
    /// </summary>
    Privileged = 2,

    /// <summary>
    /// Capability must never be granted to modules.
    /// </summary>
    Forbidden = 3
}

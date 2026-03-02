namespace DotNetCloud.Core.Authorization;

/// <summary>
/// Defines the type of caller making a request to the system.
/// </summary>
public enum CallerType
{
    /// <summary>
    /// A regular authenticated user.
    /// </summary>
    User = 0,

    /// <summary>
    /// A system process or background job running with system privileges.
    /// </summary>
    System = 1,

    /// <summary>
    /// A loaded module making inter-module calls.
    /// </summary>
    Module = 2
}

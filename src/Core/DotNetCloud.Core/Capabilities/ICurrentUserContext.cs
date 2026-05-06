using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides access to the current caller context.
/// </summary>
public interface ICurrentUserContext : ICapabilityInterface
{
    /// <summary>
    /// Gets the <see cref="CallerContext"/> for the currently authenticated caller,
    /// or <see langword="null"/> if no authenticated caller is present.
    /// </summary>
    CallerContext? GetCurrentCaller();
}

using System.Security.Cryptography;
using System.Text;

namespace DotNetCloud.Core.Authorization;

/// <summary>
/// Derives a stable, non-empty <see cref="Guid"/> identity for a module acting in its own right.
/// </summary>
/// <remarks>
/// <see cref="CallerContext"/> requires a non-empty user id for both <see cref="CallerType.User"/>
/// and <see cref="CallerType.Module"/> callers, but a module has no user account. Deriving the id
/// from the module identifier keeps the value deterministic across the fleet (so audit rows for
/// the same module group together) without letting the module choose an arbitrary value.
/// </remarks>
public static class ModuleCallerIdentity
{
    /// <summary>
    /// Derives the caller id that represents <paramref name="moduleId"/> acting for itself.
    /// </summary>
    /// <param name="moduleId">The verified module identifier.</param>
    /// <returns>A deterministic, non-empty caller id.</returns>
    public static Guid ToUserId(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("dotnetcloud.module:" + moduleId));
        return new Guid(hash.AsSpan(0, 16));
    }
}

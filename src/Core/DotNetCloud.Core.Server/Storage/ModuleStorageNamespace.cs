using System.Text.RegularExpressions;
using DotNetCloud.Core.Server.Supervisor;

namespace DotNetCloud.Core.Server.Storage;

/// <summary>
/// Builds and validates the on-disk location of a module storage object (F5).
/// </summary>
/// <remarks>
/// <para>
/// A module never names an absolute path. It supplies a container and a namespace-relative object
/// path; the core stamps the module identity (from the verified module credential) and, for
/// user-owned objects, the acting user (from the verified user assertion) in front of them.
/// </para>
/// <para>
/// Layout: <c>&lt;storageRoot&gt;/&lt;moduleId&gt;/&lt;container&gt;/&lt;path&gt;</c> for module data and
/// <c>&lt;storageRoot&gt;/&lt;moduleId&gt;/users/&lt;userId&gt;/&lt;container&gt;/&lt;path&gt;</c> for user data.
/// Because the caller cannot supply the leading segments, addressing another module's or user's
/// object is not expressible; the final <see cref="PathContainment"/> check rejects anything that
/// still escapes through traversal segments.
/// </para>
/// </remarks>
internal static class ModuleStorageNamespace
{
    /// <summary>Fixed segment separating module-global data from per-user data.</summary>
    public const string UsersSegment = "users";

    /// <summary>Maximum length of the caller-supplied object path.</summary>
    public const int MaxRelativePathLength = 1024;

    /// <summary>Maximum length of a container name.</summary>
    public const int MaxContainerLength = 64;

    private static readonly Regex ModuleIdPattern = new(
        @"^[a-z0-9](?:[a-z0-9._-]*[a-z0-9])?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ContainerPattern = new(
        @"^[a-z0-9][a-z0-9._-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Composes the absolute path of a storage object inside the caller's namespace.
    /// </summary>
    /// <param name="storageRoot">Root directory that holds every module's storage.</param>
    /// <param name="moduleId">The authenticated module id (never taken from a request field).</param>
    /// <param name="container">The module's container name (e.g. <c>attachments</c>).</param>
    /// <param name="userId">
    /// The verified acting user for user-owned data; <see langword="null"/> for module-global data.
    /// </param>
    /// <param name="relativePath">Object path relative to the container; empty addresses the container itself.</param>
    /// <param name="absolutePath">The resolved, contained path when the call succeeds.</param>
    /// <param name="error">A client-safe validation error when the call fails.</param>
    /// <returns><see langword="true"/> when the path is valid and contained.</returns>
    public static bool TryBuild(
        string storageRoot,
        string moduleId,
        string container,
        string? userId,
        string relativePath,
        out string absolutePath,
        out string? error)
    {
        absolutePath = string.Empty;

        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            error = "The storage root is not configured.";
            return false;
        }

        if (!TryValidateModuleId(moduleId, out error)
            || !TryValidateContainer(container, out error)
            || !TryValidateUserId(userId, out var normalizedUserId, out error)
            || !TryValidateRelativePath(relativePath, out error))
        {
            return false;
        }

        var moduleRoot = Path.Combine(Path.GetFullPath(storageRoot), moduleId);
        var segments = new List<string>(6) { moduleRoot };

        if (normalizedUserId is not null)
        {
            segments.Add(UsersSegment);
            segments.Add(normalizedUserId);
        }

        segments.Add(container);

        if (relativePath.Length > 0)
        {
            segments.Add(relativePath);
        }

        var candidate = Path.Combine([.. segments]);

        if (!PathContainment.TryResolveWithin(moduleRoot, candidate, out absolutePath))
        {
            absolutePath = string.Empty;
            error = "The object path escapes the module storage namespace.";
            return false;
        }

        return true;
    }

    /// <summary>Validates a module id for use as a storage namespace segment.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="error">The validation error, when invalid.</param>
    /// <returns><see langword="true"/> when the id is a safe single path segment.</returns>
    public static bool TryValidateModuleId(string? moduleId, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(moduleId) || !ModuleIdPattern.IsMatch(moduleId) || moduleId.Length > 200)
        {
            error = "The module id is not a valid storage namespace.";
            return false;
        }

        return true;
    }

    private static bool TryValidateContainer(string? container, out string? error)
    {
        error = null;

        if (string.IsNullOrEmpty(container)
            || container.Length > MaxContainerLength
            || !ContainerPattern.IsMatch(container)
            || container is "." or "..")
        {
            error = $"The container must match {ContainerPattern} and be at most {MaxContainerLength} characters.";
            return false;
        }

        return true;
    }

    private static bool TryValidateUserId(string? userId, out string? normalizedUserId, out string? error)
    {
        normalizedUserId = null;
        error = null;

        if (userId is null)
        {
            return true;
        }

        if (!Guid.TryParse(userId, out var parsed) || parsed == Guid.Empty)
        {
            error = "The acting user id is not a valid user identifier.";
            return false;
        }

        // Always re-format: the namespace segment must not depend on the caller's spelling.
        normalizedUserId = parsed.ToString("D");
        return true;
    }

    private static bool TryValidateRelativePath(string? relativePath, out string? error)
    {
        error = null;

        var path = relativePath ?? string.Empty;
        if (path.Length == 0)
        {
            return true;
        }

        if (path.Length > MaxRelativePathLength)
        {
            error = $"The object path must be at most {MaxRelativePathLength} characters.";
            return false;
        }

        if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':'))
        {
            error = "The object path must be relative to the module namespace.";
            return false;
        }

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                error = "The object path must not contain empty, current or parent directory segments.";
                return false;
            }

            if (segment.Any(char.IsControl))
            {
                error = "The object path must not contain control characters.";
                return false;
            }
        }

        return true;
    }
}

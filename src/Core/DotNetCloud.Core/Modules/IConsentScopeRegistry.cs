namespace DotNetCloud.Core.Modules;

/// <summary>
/// The consent scopes a module declared in the manifest the supervisor loaded.
/// </summary>
/// <remarks>
/// A narrow, public view of the manifest registry: enough for a user-facing consent decision, without
/// exposing the whole manifest model. <see langword="null"/> means "no manifest is loaded for this
/// module" (not installed, not running, or not a sandboxed module), which a caller must treat as
/// "nothing can be approved" rather than "no scopes requested".
/// </remarks>
public interface IConsentScopeRegistry
{
    /// <summary>
    /// Gets the consent scopes a module declared, or <see langword="null"/> when no manifest is loaded.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The declared scopes, or <see langword="null"/>.</returns>
    IReadOnlyCollection<string>? GetUserConsentScopes(string moduleId);
}

namespace DotNetCloud.Modules.Sdk.Web;

/// <summary>
/// Raised when a module read needs a consent scope the acting user has not approved yet.
/// </summary>
/// <remarks>
/// A frame is a sandboxed document with no way to approve anything itself: the shell owns the consent
/// prompt. Swallowing this into an empty result leaves the UI permanently empty with no affordance to
/// fix it, so the caller catches this and calls
/// <see cref="ModuleFrameBridge.RequestConsentAsync"/> with <see cref="Scopes"/>.
/// </remarks>
public sealed class ModuleConsentRequiredException : ModuleApiException
{
    /// <summary>The module error code a host answers when the profile scope is missing.</summary>
    public const string ProfileConsentCode = "PROFILE_CONSENT_REQUIRED";

    /// <summary>The module error code a host answers when the files-read scope is missing.</summary>
    public const string FilesConsentCode = "FILES_CONSENT_REQUIRED";

    /// <summary>The gRPC trailer the core sets on a refused brokered call.</summary>
    public const string ConsentRequiredCode = "MODULE_CONSENT_REQUIRED";

    /// <summary>Initializes a new instance of the <see cref="ModuleConsentRequiredException"/> class.</summary>
    /// <param name="message">The host's explanation.</param>
    /// <param name="scopes">The scopes the shell must ask the user to approve.</param>
    public ModuleConsentRequiredException(string message, params string[] scopes)
        : base(message, 403, ConsentRequiredCode)
    {
        Scopes = scopes.Length == 0 ? ["profile"] : scopes;
    }

    /// <summary>Gets the consent scopes to request.</summary>
    public IReadOnlyList<string> Scopes { get; }
}

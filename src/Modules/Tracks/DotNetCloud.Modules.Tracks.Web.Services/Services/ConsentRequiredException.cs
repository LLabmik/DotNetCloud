namespace DotNetCloud.Modules.Tracks.Web.Services;

/// <summary>
/// Raised when a module read needs a consent scope the acting user has not approved yet.
/// </summary>
/// <remarks>
/// A frame is a sandboxed document with no way to approve anything itself: the shell owns the consent
/// prompt. Swallowing this into an empty result — which is what the organization listing used to do —
/// left the product picker permanently empty with no affordance to fix it, so the caller catches this
/// and calls <c>ModuleBridge.RequestConsentAsync</c> with <see cref="Scopes"/>.
/// </remarks>
public sealed class ConsentRequiredException : Exception
{
    /// <summary>The module error code the host answers when the profile scope is missing.</summary>
    public const string ProfileConsentCode = "PROFILE_CONSENT_REQUIRED";

    /// <summary>Initializes a new instance of the <see cref="ConsentRequiredException"/> class.</summary>
    /// <param name="message">The host's explanation.</param>
    /// <param name="scopes">The scopes the shell must ask the user to approve.</param>
    public ConsentRequiredException(string message, params string[] scopes)
        : base(message)
    {
        Scopes = scopes.Length == 0 ? ["profile"] : scopes;
    }

    /// <summary>Gets the consent scopes to request.</summary>
    public IReadOnlyList<string> Scopes { get; }
}

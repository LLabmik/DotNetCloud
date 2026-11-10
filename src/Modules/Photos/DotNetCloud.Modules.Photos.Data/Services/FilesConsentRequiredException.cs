using DotNetCloud.Core.Errors;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Raised when the user has not (yet) consented to the <c>files:read</c> scope the module needs to read
/// a photo's original through the core.
/// </summary>
/// <remarks>
/// This is not a failure of the module: the core refuses the call, and the answer the UI needs is a
/// <b>403 with a code it can act on</b>, so it can ask the shell to show the consent prompt. Folding it
/// into a generic "could not read the original" made the user see
/// <c>Failed to apply edits to thumbnails</c> with nothing to click and no hint that a consent request
/// was waiting.
/// </remarks>
public sealed class FilesConsentRequiredException : DotNetCloudException
{
    /// <summary>The error code the module UI matches to trigger its consent request.</summary>
    public const string Code = "FILES_CONSENT_REQUIRED";

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesConsentRequiredException"/> class.
    /// </summary>
    /// <param name="message">A message shown to the user.</param>
    public FilesConsentRequiredException(string message)
        : base(Code, message)
    {
    }
}

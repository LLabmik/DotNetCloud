using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Security;

/// <summary>
/// Service for validating uploaded files — extension whitelist, magic byte checking, filename sanitization.
/// </summary>
public interface IFileValidationService
{
    /// <summary>
    /// Validates an uploaded file against the specified allowed types.
    /// Checks extension, MIME type, magic bytes, and file size.
    /// </summary>
    /// <param name="file">The uploaded file to validate.</param>
    /// <param name="allowedTypes">The list of allowed file type definitions.</param>
    /// <param name="maxSize">Optional override for maximum file size in bytes.</param>
    /// <returns>A <see cref="FileValidationResult"/> indicating pass/fail.</returns>
    FileValidationResult Validate(IFormFile file, AllowedFileTypes.FileTypeDefinition[] allowedTypes, long? maxSize = null);

    /// <summary>
    /// Validates a file the module did not receive as an upload — for example one copied out of the
    /// user's own Files tree — against the same extension and size policy.
    /// </summary>
    /// <param name="fileName">The file's name (its extension decides the policy).</param>
    /// <param name="length">The content length in bytes.</param>
    /// <param name="allowedTypes">Allowed type definitions.</param>
    /// <param name="maxSize">Optional size override in bytes.</param>
    /// <returns>The validation result.</returns>
    /// <remarks>
    /// Magic-byte validation is not performed: there is no upload to inspect at the decision point,
    /// and the content already lives in the user's own storage under the same platform.
    /// </remarks>
    FileValidationResult Validate(
        string fileName,
        long length,
        AllowedFileTypes.FileTypeDefinition[] allowedTypes,
        long? maxSize = null);

    /// <summary>
    /// Sanitizes a filename by stripping path traversal characters, null bytes, and other dangerous patterns.
    /// </summary>
    /// <param name="fileName">The original filename.</param>
    /// <returns>A sanitized, safe filename.</returns>
    string SanitizeFileName(string fileName);
}

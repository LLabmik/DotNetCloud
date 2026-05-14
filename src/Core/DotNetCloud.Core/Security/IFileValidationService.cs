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
    /// Sanitizes a filename by stripping path traversal characters, null bytes, and other dangerous patterns.
    /// </summary>
    /// <param name="fileName">The original filename.</param>
    /// <returns>A sanitized, safe filename.</returns>
    string SanitizeFileName(string fileName);
}

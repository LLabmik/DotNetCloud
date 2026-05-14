using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Security;

/// <summary>
/// Validates uploaded files against extension whitelists, magic byte signatures, and size limits.
/// Also provides filename sanitization to prevent path traversal and other injection attacks.
/// </summary>
public sealed class FileValidationService : IFileValidationService
{
    /// <summary>
    /// Minimum number of bytes to read from a file for magic byte validation.
    /// </summary>
    private const int MagicByteReadSize = 16;

    /// <inheritdoc />
    public FileValidationResult Validate(
        IFormFile file,
        AllowedFileTypes.FileTypeDefinition[] allowedTypes,
        long? maxSize = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(allowedTypes);

        if (allowedTypes.Length == 0)
        {
            return FileValidationResult.Failure(
                "FILE_NO_ALLOWED_TYPES",
                "No file types are configured as allowed.");
        }

        if (file.Length == 0)
        {
            return FileValidationResult.Failure(
                "FILE_EMPTY",
                "The uploaded file is empty.");
        }

        // Check extension against allowed types
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return FileValidationResult.Failure(
                "FILE_NO_EXTENSION",
                "The uploaded file has no extension. Allowed extensions: " + GetAllowedExtensions(allowedTypes));
        }

        var matchedTypes = allowedTypes
            .Where(t => t.Extensions.Any(e =>
                e.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (matchedTypes.Length == 0)
        {
            return FileValidationResult.Failure(
                "FILE_EXTENSION_NOT_ALLOWED",
                $"File extension '{extension}' is not allowed. Allowed extensions: {GetAllowedExtensions(allowedTypes)}");
        }

        // Check MIME type if the matched type defines expectations
        foreach (var matched in matchedTypes)
        {
            if (matched.MimeTypes.Length > 0)
            {
                if (matched.MimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                {
                    break; // MIME type matched
                }
            }
            else
            {
                // No MIME restrictions for this type — accept any
                break;
            }
        }

        // Check file size (per-type max or override)
        var effectiveMaxSize = maxSize ?? matchedTypes.Max(t => t.MaxSize);
        if (effectiveMaxSize.HasValue && file.Length > effectiveMaxSize.Value)
        {
            var sizeMb = effectiveMaxSize.Value / (1024.0 * 1024.0);
            return FileValidationResult.Failure(
                "FILE_TOO_LARGE",
                $"File size exceeds the maximum allowed size of {sizeMb:F1} MB.");
        }

        // Check magic bytes if the matched type defines them
        foreach (var matched in matchedTypes)
        {
            if (matched.MagicBytes.Length > 0)
            {
                if (ValidateMagicBytes(file, matched.MagicBytes))
                {
                    return FileValidationResult.Success();
                }
            }
            else
            {
                // No magic byte validation for this type — accept based on extension/size
                return FileValidationResult.Success();
            }
        }

        // File matched extension but failed all magic byte checks
        return FileValidationResult.Failure(
            "FILE_CONTENT_MISMATCH",
            "The file content does not match its extension. The file may be renamed or corrupted.");
    }

    /// <inheritdoc />
    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "untitled";
        }

        // Remove path traversal characters and null bytes
        var sanitized = fileName
            .Replace("..", "", StringComparison.Ordinal)
            .Replace("/", "", StringComparison.Ordinal)
            .Replace("\\", "", StringComparison.Ordinal)
            .Replace("\0", "", StringComparison.Ordinal);

        // Remove any remaining dangerous characters
        sanitized = sanitized.Replace(":", "", StringComparison.Ordinal);
        sanitized = sanitized.Replace("*", "", StringComparison.Ordinal);
        sanitized = sanitized.Replace("?", "", StringComparison.Ordinal);
        sanitized = sanitized.Replace("\"", "", StringComparison.Ordinal);
        sanitized = sanitized.Replace("<", "", StringComparison.Ordinal);
        sanitized = sanitized.Replace(">", "", StringComparison.Ordinal);
        sanitized = sanitized.Replace("|", "", StringComparison.Ordinal);

        // Trim to reasonable length
        if (sanitized.Length > 255)
        {
            var ext = Path.GetExtension(sanitized);
            sanitized = sanitized[..(255 - ext.Length)] + ext;
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized;
    }

    private static string GetAllowedExtensions(AllowedFileTypes.FileTypeDefinition[] types)
    {
        return string.Join(", ", types
            .SelectMany(t => t.Extensions)
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static bool ValidateMagicBytes(IFormFile file, byte[][] expectedSignatures)
    {
        using var stream = file.OpenReadStream();
        var header = new byte[MagicByteReadSize];
        var bytesRead = stream.Read(header, 0, MagicByteReadSize);

        if (bytesRead < MagicByteReadSize)
        {
            // File is smaller than read buffer; resize the array
            Array.Resize(ref header, bytesRead);
        }

        // Reset stream position so the caller can re-read
        stream.Position = 0;

        return expectedSignatures.Any(sig =>
        {
            if (sig.Length > header.Length)
            {
                return false;
            }

            for (var i = 0; i < sig.Length; i++)
            {
                if (header[i] != sig[i])
                {
                    return false;
                }
            }

            return true;
        });
    }
}

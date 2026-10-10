using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Reads the EXIF capture timestamp (<c>DateTimeOriginal</c>, falling back to <c>DateTime</c>) from an
/// image, normalised to UTC. Mirrors the parsing in <c>ExifMetadataExtractor</c> so the Files and
/// Photos modules agree on the same value for the same file.
/// </summary>
public static class ImageCaptureTimeReader
{
    /// <summary>
    /// Reads the capture timestamp from an already-decoded image. Returns <c>null</c> when the image
    /// carries no usable EXIF date.
    /// </summary>
    /// <param name="image">A decoded image.</param>
    public static DateTime? Read(Image image)
    {
        var profile = image.Metadata.ExifProfile;
        if (profile is null)
            return null;

        string? dateString = null;
        if (profile.TryGetValue(ExifTag.DateTimeOriginal, out var dateOriginal))
            dateString = dateOriginal?.Value;

        if (string.IsNullOrWhiteSpace(dateString) && profile.TryGetValue(ExifTag.DateTime, out var dateTime))
            dateString = dateTime?.Value;

        return Parse(dateString);
    }

    /// <summary>
    /// Decodes the image from <paramref name="content"/> and reads its EXIF capture timestamp.
    /// Returns <c>null</c> when the content is not a decodable image or carries no EXIF date.
    /// </summary>
    /// <param name="content">A readable stream containing the image bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<DateTime?> TryReadAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync(content, cancellationToken);
        return Read(image);
    }

    /// <summary>
    /// Parses an EXIF date string ("yyyy:MM:dd HH:mm:ss", no time zone) as UTC.
    /// </summary>
    internal static DateTime? Parse(string? value) =>
        DateTime.TryParseExact(
            value,
            "yyyy:MM:dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var result)
            ? result
            : null;
}

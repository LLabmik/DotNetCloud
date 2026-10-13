namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Stages an outbound share payload on disk for the system share sheet.
/// </summary>
/// <remarks>
/// Every share gets its own cache directory. The FileProvider URI handed to the receiving app is
/// derived from the file path, so a single reused path (a fixed "share_image.jpg", say) means a
/// single reused URI — and receiving apps cache the bytes behind that URI, which makes them keep
/// showing the first image they were ever handed no matter what is shared afterwards. A unique
/// directory per share keeps the URI unique while the file itself keeps the source name, so the
/// receiver shows a meaningful file name and the right MIME type.
/// </remarks>
public static class ShareFileWriter
{
    /// <summary>How long a staged share is left on disk for a receiver that reads it late.</summary>
    private static readonly TimeSpan StaleShareAge = TimeSpan.FromHours(1);

    /// <summary>Stages <paramref name="data"/> in the app cache and returns the staged file path.</summary>
    /// <param name="data">Payload to share.</param>
    /// <param name="fileName">Name of the source file; drives the staged file's name and extension.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full path of the staged file.</returns>
    public static Task<string> WriteShareFileAsync(byte[] data, string? fileName, CancellationToken ct = default)
        => WriteShareFileAsync(data, fileName, FileSystem.CacheDirectory, ct);

    /// <summary>Stages <paramref name="data"/> under <paramref name="cacheRoot"/> and returns the file path.</summary>
    internal static async Task<string> WriteShareFileAsync(
        byte[] data, string? fileName, string cacheRoot, CancellationToken ct = default)
    {
        var shareRoot = Path.Combine(cacheRoot, "share");
        DeleteStaleShares(shareRoot);

        var directory = Path.Combine(shareRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, BuildSafeFileName(fileName));
        await File.WriteAllBytesAsync(path, data, ct).ConfigureAwait(false);
        return path;
    }

    /// <summary>Removes share directories that are too old to belong to a pending share.</summary>
    private static void DeleteStaleShares(string shareRoot)
    {
        try
        {
            if (!Directory.Exists(shareRoot))
                return;

            var cutoff = DateTime.UtcNow - StaleShareAge;
            foreach (var directory in Directory.EnumerateDirectories(shareRoot))
            {
                if (Directory.GetLastWriteTimeUtc(directory) < cutoff)
                    Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Cache maintenance must never block a share.
        }
        catch (UnauthorizedAccessException)
        {
            // Cache maintenance must never block a share.
        }
    }

    /// <summary>Turns a source file name into a bare, safe file name, keeping its extension.</summary>
    internal static string BuildSafeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        var extension = Path.GetExtension(name);
        var stem = Sanitize(Path.GetFileNameWithoutExtension(name));

        // "." and ".." are legal file names but would resolve outside the share directory.
        if (stem.Length == 0 || stem == "." || stem == "..")
            stem = "image";

        return stem + Sanitize(extension);
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var kept = value.Where(c => !invalid.Contains(c) && c != '\\' && c != '/').ToArray();
        return new string(kept).Trim();
    }
}

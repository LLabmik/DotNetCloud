namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Result returned by an <see cref="IFileScanner"/> scan operation.
/// </summary>
/// <param name="IsClean"><see langword="true"/> when no threat was detected.</param>
/// <param name="ThreatName">Name of the detected threat, or <see langword="null"/> when clean.</param>
/// <param name="ScannerName">Identifier of the scanner engine that produced this result.</param>
public record ScanResult(bool IsClean, string? ThreatName = null, string? ScannerName = null);

/// <summary>
/// Abstraction for a pluggable file-content scanner (e.g., ClamAV).
/// </summary>
public interface IFileScanner
{
    /// <summary>
    /// Scans the provided content stream for threats.
    /// </summary>
    /// <param name="content">File content to scan. The caller is responsible for disposing the stream.</param>
    /// <param name="fileName">Original file name, used for logging and scanner context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ScanResult"/> describing the outcome.</returns>
    Task<ScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Extracts a representative still frame from a video source file.
/// </summary>
public interface IVideoFrameExtractor
{
    /// <summary>
    /// Attempts to extract a frame from <paramref name="inputPath"/> and write it
    /// as an image file at <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="inputPath">Absolute path to the source video file.</param>
    /// <param name="outputPath">Absolute path where the extracted image should be written.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when extraction succeeds and output is created; otherwise <c>false</c>.</returns>
    Task<bool> TryExtractFrameAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default);
}

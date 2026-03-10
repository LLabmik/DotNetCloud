namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Renders the first page of a PDF document to an image file.
/// </summary>
public interface IPdfPageRenderer
{
    /// <summary>
    /// Attempts to render page 1 from <paramref name="inputPath"/> and write it
    /// to <paramref name="outputImagePath"/>.
    /// </summary>
    /// <param name="inputPath">Absolute path to the source PDF file.</param>
    /// <param name="outputImagePath">Absolute path where the rendered image should be written.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when rendering succeeds and output is created; otherwise <c>false</c>.</returns>
    Task<bool> TryRenderFirstPageAsync(string inputPath, string outputImagePath, CancellationToken cancellationToken = default);
}

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Uses the pdftoppm executable (Poppler) to render the first PDF page to JPEG.
/// </summary>
public sealed class PdftoppmPdfPageRenderer : IPdfPageRenderer
{
    private readonly string _pdftoppmPath;
    private readonly ILogger<PdftoppmPdfPageRenderer> _logger;

    /// <summary>
    /// Initializes the renderer with a configuration-backed pdftoppm executable path.
    /// </summary>
    /// <param name="configuration">Configuration source used to resolve renderer settings.</param>
    /// <param name="logger">Logger instance.</param>
    public PdftoppmPdfPageRenderer(IConfiguration configuration, ILogger<PdftoppmPdfPageRenderer> logger)
    {
        _pdftoppmPath = configuration["Files:Thumbnails:PdfToPpmPath"] ?? "pdftoppm";
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> TryRenderFirstPageAsync(string inputPath, string outputImagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var outputDirectory = Path.GetDirectoryName(outputImagePath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return false;
            }

            Directory.CreateDirectory(outputDirectory);

            var outputPrefix = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(outputImagePath));
            var generatedPath = outputPrefix + ".jpg";

            if (File.Exists(generatedPath))
            {
                File.Delete(generatedPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _pdftoppmPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-jpeg");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-singlefile");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add(outputPrefix);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogWarning("Unable to start pdftoppm process for thumbnail generation.");
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("pdftoppm exited with code {ExitCode} for input {InputPath}.", process.ExitCode, inputPath);
                return false;
            }

            if (!File.Exists(generatedPath) || new FileInfo(generatedPath).Length == 0)
            {
                return false;
            }

            if (!string.Equals(generatedPath, outputImagePath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(generatedPath, outputImagePath, overwrite: true);
            }

            return File.Exists(outputImagePath) && new FileInfo(outputImagePath).Length > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "pdftoppm rendering failed for input {InputPath}.", inputPath);
            return false;
        }
    }
}

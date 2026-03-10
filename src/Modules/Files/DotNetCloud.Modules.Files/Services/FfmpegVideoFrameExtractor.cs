using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Uses the ffmpeg executable to extract the first video frame as a JPEG image.
/// </summary>
public sealed class FfmpegVideoFrameExtractor : IVideoFrameExtractor
{
    private readonly string _ffmpegPath;
    private readonly ILogger<FfmpegVideoFrameExtractor> _logger;

    /// <summary>
    /// Initializes the extractor with configuration-backed ffmpeg path.
    /// </summary>
    /// <param name="configuration">Configuration source used to resolve ffmpeg settings.</param>
    /// <param name="logger">Logger instance.</param>
    public FfmpegVideoFrameExtractor(IConfiguration configuration, ILogger<FfmpegVideoFrameExtractor> logger)
    {
        _ffmpegPath = configuration["Files:Thumbnails:FfmpegPath"] ?? "ffmpeg";
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> TryExtractFrameAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-loglevel");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("-frames:v");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-q:v");
            startInfo.ArgumentList.Add("2");
            startInfo.ArgumentList.Add(outputPath);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogWarning("Unable to start ffmpeg process for thumbnail generation.");
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ffmpeg exited with code {ExitCode} for input {InputPath}.", process.ExitCode, inputPath);
                return false;
            }

            return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffmpeg frame extraction failed for input {InputPath}.", inputPath);
            return false;
        }
    }
}

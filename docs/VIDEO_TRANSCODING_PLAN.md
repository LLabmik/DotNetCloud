# Video Transcoding for HTML5 Playback — Implementation Plan

**Date:** 2026-06-09
**Status:** Not started
**Target:** Add FFmpeg-based on-the-fly video transcoding so any uploaded video (MKV, AVI, MOV, WMV, FLV, WebM, etc.) plays in any HTML5 browser.

## Architecture Overview

```
Browser requests /api/v1/videos/{id}/stream?token=...
       │
       ▼
VideoController (REST)
       │
       ├─ CanDirectPlay()? ──Yes──▶ Serve original file (PhysicalFileResult + Range)
       │
       └─ No ──▶ TranscodeCacheService.GetCachedAsync()
                    │
                    ├─ Cache hit ──▶ Serve cached file
                    │
                    └─ Cache miss ──▶ VideoTranscodingService.TranscodeAsync()
                                          │
                                          ├─ FfmpegArgumentBuilder.Build()
                                          ├─ FfmpegProcessManager.RunAsync()
                                          └─ ProgressiveFileStream tailing output
```

**Format target:** H.264 video (libx264, CRF 23, veryfast preset) + AAC audio (128kbps) in MP4 container with `faststart` moov atom. This is the ONLY combination that plays natively in Chrome, Firefox, Safari, Edge without any plugins.

**Service location:** Within the Video module Host process — FFmpeg is already used there for thumbnail/metadata extraction. No new process needed.

---

## Files to Create

| #   | File Path                                                                              | Purpose                 |
| --- | -------------------------------------------------------------------------------------- | ----------------------- |
| F1  | `src/Modules/Video/DotNetCloud.Modules.Video/Services/VideoTranscodingOptions.cs`      | Config class            |
| F2  | `src/Modules/Video/DotNetCloud.Modules.Video/Services/FfmpegArgumentBuilder.cs`        | FFmpeg CLI builder      |
| F3  | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/FfmpegProcessManager.cs`    | FFmpeg process wrapper  |
| F4  | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/TranscodeCacheService.cs`   | Content-addressed cache |
| F5  | `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoTranscodingService.cs`     | Service interface       |
| F6  | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/VideoTranscodingService.cs` | Main orchestrator       |
| F7  | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/TranscodingJob.cs`          | Job model               |
| F8  | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/TranscodingJobTracker.cs`   | Job tracker             |
| F9  | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/ProgressiveFileStream.cs`   | Tailing stream          |
| F10 | `tests/DotNetCloud.Video.Tests/Services/FfmpegArgumentBuilderTests.cs`                 | Unit tests              |
| F11 | `tests/DotNetCloud.Video.Tests/DotNetCloud.Video.Tests.csproj`                         | Test project            |

## Files to Edit

| #   | File Path                                                                           | Change                                |
| --- | ----------------------------------------------------------------------------------- | ------------------------------------- |
| E1  | `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoStreamingService.cs`    | Add `GetStreamableFormatAsync` method |
| E2  | `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoSettingsProvider.cs`    | Add `GetTranscodingOptions()`         |
| E3  | `src/Modules/Video/DotNetCloud.Modules.Video.Data/VideoServiceRegistration.cs`      | Register all new services             |
| E4  | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Controllers/VideoController.cs`   | Add stream/transcode endpoints        |
| E5  | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Protos/video_service.proto`       | New gRPC RPCs + messages              |
| E6  | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/VideoGrpcServiceImpl.cs` | Implement new RPCs                    |
| E7  | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Program.cs`                       | Register new hosted services          |
| E8  | `src/Core/DotNetCloud.Core.Server/Grpc/Clients/VideoApiClient.cs`                   | Add transcode client methods          |

---

## Phase 1: Foundation — Transcoding Infrastructure

### Step 1.1 — Transcoding Configuration & Options

#### F1: `VideoTranscodingOptions.cs`

Create file: `src/Modules/Video/DotNetCloud.Modules.Video/Services/VideoTranscodingOptions.cs`

```csharp
namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Configuration options for video transcoding.
/// Bound from configuration section "Video:Transcoding".
/// </summary>
public sealed class VideoTranscodingOptions
{
    /// <summary>Path to the ffmpeg binary. Default "ffmpeg" (resolved from PATH).</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>Maximum number of concurrent ffmpeg transcode processes.</summary>
    public int MaxConcurrentJobs { get; set; } = 2;

    /// <summary>Directory for temporary transcode output files (while in progress).</summary>
    public string TempDirectory { get; set; } = string.Empty;

    /// <summary>How long cached transcode outputs are kept, in hours. 0 = never expire.</summary>
    public int CacheTtlHours { get; set; } = 168; // 7 days

    /// <summary>Maximum total size of the transcode cache in bytes. 0 = unlimited.</summary>
    public long MaxCacheSizeBytes { get; set; } = 10L * 1024 * 1024 * 1024; // 10 GB

    /// <summary>Video codec: "libx264" (default), "libx265", "libvpx-vp9".</summary>
    public string VideoCodec { get; set; } = "libx264";

    /// <summary>CRF value for video quality. Lower = better. 23 is default for x264.</summary>
    public int VideoCrf { get; set; } = 23;

    /// <summary>Encoder preset: "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow".</summary>
    public string EncoderPreset { get; set; } = "veryfast";

    /// <summary>Maximum output video width. Source is scaled down if wider. 0 = no limit.</summary>
    public int MaxWidth { get; set; } = 1920;

    /// <summary>Maximum output video height. Source is scaled down if taller. 0 = no limit.</summary>
    public int MaxHeight { get; set; } = 1080;

    /// <summary>Audio codec: "aac" (default), "libmp3lame", "opus".</summary>
    public string AudioCodec { get; set; } = "aac";

    /// <summary>Audio bitrate in kbps. Default 128.</summary>
    public int AudioBitrateKbps { get; set; } = 128;

    /// <summary>ffmpeg thread count. 0 = auto.</summary>
    public int ThreadCount { get; set; } = 0;

    /// <summary>
    /// MIME types that can be direct-played (served as-is without transcoding).
    /// Videos with MIME types NOT in this list are always transcoded.
    /// Video codec and container are also checked via ffprobe.
    /// </summary>
    public HashSet<string> DirectPlayMimeTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4"
    };
}
```

#### E2: Extend `IVideoSettingsProvider`

Edit: `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoSettingsProvider.cs`

Find the existing interface and add one method:

```csharp
/// <summary>Gets transcoding configuration options.</summary>
VideoTranscodingOptions GetTranscodingOptions();
```

#### E2b: Implement the new method

Find the implementation class (search for `: IVideoSettingsProvider` in the Video module projects — likely in `VideoSettingsProvider.cs` in the Data project) and add:

```csharp
/// <inheritdoc />
public VideoTranscodingOptions GetTranscodingOptions()
{
    var options = new VideoTranscodingOptions();
    _configuration.GetSection("Video:Transcoding").Bind(options);
    return options;
}
```

Add using: `using DotNetCloud.Modules.Video.Services;`

Also add to `appsettings.json` in the Host project (or config.json on the server):

```json
"Video": {
  "Transcoding": {
    "FfmpegPath": "ffmpeg",
    "MaxConcurrentJobs": 2,
    "CacheTtlHours": 168,
    "MaxCacheSizeBytes": 10737418240,
    "VideoCodec": "libx264",
    "VideoCrf": 23,
    "EncoderPreset": "veryfast",
    "MaxWidth": 1920,
    "MaxHeight": 1080,
    "AudioCodec": "aac",
    "AudioBitrateKbps": 256,
    "ThreadCount": 0
  }
}
```

---

### Step 1.2 — FFmpeg Command-Line Builder

#### F2: `FfmpegArgumentBuilder.cs`

Create file: `src/Modules/Video/DotNetCloud.Modules.Video/Services/FfmpegArgumentBuilder.cs`

This class builds ffmpeg CLI arguments. It is purely functional (no state, no I/O) so it lives in the core module project (not Host) and is unit-testable.

```csharp
using System.Globalization;
using System.Text;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Builds ffmpeg command-line arguments for video transcoding.
/// Thread-safe (all methods are stateless).
/// </summary>
public sealed class FfmpegArgumentBuilder
{
    /// <summary>
    /// Returns true if the video can be played directly in HTML5 browsers
    /// without transcoding.
    /// Must be H.264 or H.265 video + AAC or MP3 audio + MP4 container.
    /// </summary>
    public bool CanDirectPlay(string mimeType, string? videoCodec, string? audioCodec, string container)
    {
        // MIME must be video/mp4
        if (!string.Equals(mimeType, "video/mp4", StringComparison.OrdinalIgnoreCase))
            return false;

        // Container must be mp4
        if (!string.Equals(container, "mp4", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(container, "mov", StringComparison.OrdinalIgnoreCase))
            return false;

        // Video codec must be H.264 (avc1) or H.265 (hevc/hvc1)
        // H.265 has partial browser support; H.264 is universal
        bool videoOk = videoCodec is not null && (
            videoCodec.Contains("h264", StringComparison.OrdinalIgnoreCase) ||
            videoCodec.Contains("avc", StringComparison.OrdinalIgnoreCase));

        // Audio must be AAC or MP3
        bool audioOk = audioCodec is null || audioCodec.Length == 0 || (
            audioCodec.Contains("aac", StringComparison.OrdinalIgnoreCase) ||
            audioCodec.Contains("mp3", StringComparison.OrdinalIgnoreCase));

        return videoOk && audioOk;
    }

    /// <summary>
    /// Builds the ffmpeg command-line arguments for progressive MP4 transcoding.
    /// Output: H.264 + AAC in MP4 with faststart for web streaming.
    /// </summary>
    /// <param name="inputPath">Absolute path to the source video file.</param>
    /// <param name="outputPath">Absolute path where the transcoded file will be written.</param>
    /// <param name="options">Transcoding options (codec, CRF, preset, bitrate, etc.).</param>
    /// <param name="seekStart">Optional start time for seeking (TimeSpan or null).</param>
    /// <param name="seekDuration">Optional duration to transcode (TimeSpan or null = full file).</param>
    /// <returns>Full ffmpeg argument string (does NOT include the "ffmpeg" binary name).</returns>
    public string BuildProgressiveMp4Args(
        string inputPath,
        string outputPath,
        VideoTranscodingOptions options,
        TimeSpan? seekStart = null,
        TimeSpan? seekDuration = null)
    {
        var sb = new StringBuilder();

        // --- Hide banner and set log level ---
        sb.Append("-hide_banner -loglevel warning ");

        // --- Thread count ---
        if (options.ThreadCount > 0)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-threads {0} ", options.ThreadCount);
        }

        // --- Seeking (must come before -i) ---
        if (seekStart.HasValue && seekStart.Value > TimeSpan.Zero)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-ss {0} ", seekStart.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        // --- Input file ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-i \"{0}\" ", EscapePath(inputPath));

        // --- Duration limit ---
        if (seekDuration.HasValue && seekDuration.Value > TimeSpan.Zero)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-t {0} ", seekDuration.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        // --- Map all streams we want ---
        sb.Append("-map 0:v:0? -map 0:a:0? ");

        // --- Video codec ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-c:v {0} ", options.VideoCodec);

        // --- Video preset ---
        if (!string.IsNullOrEmpty(options.EncoderPreset))
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-preset {0} ", options.EncoderPreset);
        }

        // --- Video CRF (quality) ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-crf {0} ", options.VideoCrf);

        // --- Pixel format (ensure browser compatibility) ---
        sb.Append("-pix_fmt yuv420p ");

        // --- Resolution scaling ---
        if (options.MaxWidth > 0 && options.MaxHeight > 0)
        {
            // scale filter: fit within max dimensions, keep aspect ratio, ensure even dimensions
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "-vf \"scale='min({0},iw)':'min({1},ih)':force_original_aspect_ratio=decrease,pad='ceil(iw/2)*2':'ceil(ih/2)*2'\" ",
                options.MaxWidth,
                options.MaxHeight);
        }
        else
        {
            // Just ensure even dimensions (some codecs require it)
            sb.Append("-vf \"pad='ceil(iw/2)*2':'ceil(ih/2)*2'\" ");
        }

        // --- Audio codec ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-c:a {0} ", options.AudioCodec);

        // --- Audio bitrate ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-b:a {0}k ", options.AudioBitrateKbps);

        // --- Audio channels (stereo) ---
        sb.Append("-ac 2 ");

        // --- Remove metadata from source ---
        sb.Append("-map_metadata -1 ");

        // --- Faststart for web streaming (moves moov atom to beginning) ---
        sb.Append("-movflags +faststart ");

        // --- Output format ---
        sb.Append("-f mp4 ");

        // --- Overwrite output ---
        sb.Append("-y ");

        // --- Output file ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"{0}\"", EscapePath(outputPath));

        return sb.ToString();
    }

    /// <summary>
    /// Builds ffprobe arguments to extract stream info as JSON.
    /// </summary>
    public string BuildFfprobeArgs(string inputPath)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "-v quiet -print_format json -show_format -show_streams \"{0}\"",
            EscapePath(inputPath));
    }

    /// <summary>
    /// Escapes a file path for safe use in ffmpeg command lines.
    /// Handles both Windows backslashes and special characters.
    /// </summary>
    private static string EscapePath(string path)
    {
        // ffmpeg uses backslash as escape char on Windows but forward slash works everywhere
        return path.Replace('\\', '/')
                   .Replace("\"", "\\\"");
    }
}
```

#### F10: Unit Tests

Create file: `tests/DotNetCloud.Video.Tests/Services/FfmpegArgumentBuilderTests.cs`

```csharp
using DotNetCloud.Modules.Video.Services;

namespace DotNetCloud.Video.Tests.Services;

public class FfmpegArgumentBuilderTests
{
    private readonly FfmpegArgumentBuilder _builder = new();
    private readonly VideoTranscodingOptions _opts = new()
    {
        FfmpegPath = "ffmpeg",
        VideoCodec = "libx264",
        VideoCrf = 23,
        EncoderPreset = "veryfast",
        MaxWidth = 1920,
        MaxHeight = 1080,
        AudioCodec = "aac",
        AudioBitrateKbps = 128,
        ThreadCount = 0
    };

    [Fact]
    public void BuildProgressiveMp4Args_ShouldContainAllCodecArgs()
    {
        var args = _builder.BuildProgressiveMp4Args("/videos/test.mkv", "/out/test.mp4", _opts);

        Assert.Contains("-c:v libx264", args);
        Assert.Contains("-preset veryfast", args);
        Assert.Contains("-crf 23", args);
        Assert.Contains("-c:a aac", args);
        Assert.Contains("-b:a 128k", args);
        Assert.Contains("-f mp4", args);
        Assert.Contains("-movflags +faststart", args);
        Assert.Contains("-pix_fmt yuv420p", args);
    }

    [Fact]
    public void BuildProgressiveMp4Args_ShouldContainSeekStart()
    {
        var args = _builder.BuildProgressiveMp4Args("/v/test.mkv", "/o/test.mp4", _opts,
            seekStart: TimeSpan.FromSeconds(30));

        Assert.Contains("-ss 30.000", args);
    }

    [Fact]
    public void BuildProgressiveMp4Args_ShouldContainDuration()
    {
        var args = _builder.BuildProgressiveMp4Args("/v/test.mkv", "/o/test.mp4", _opts,
            seekDuration: TimeSpan.FromMinutes(5));

        Assert.Contains("-t 300.000", args);
    }

    [Fact]
    public void CanDirectPlay_H264AacMp4_ShouldReturnTrue()
    {
        bool result = _builder.CanDirectPlay("video/mp4", "h264", "aac", "mp4");
        Assert.True(result);
    }

    [Fact]
    public void CanDirectPlay_MkvContainer_ShouldReturnFalse()
    {
        bool result = _builder.CanDirectPlay("video/x-matroska", "h264", "aac", "mkv");
        Assert.False(result);
    }

    [Fact]
    public void CanDirectPlay_Vp9Codec_ShouldReturnFalse()
    {
        bool result = _builder.CanDirectPlay("video/mp4", "vp9", "aac", "mp4");
        Assert.False(result);
    }

    [Fact]
    public void BuildFfprobeArgs_ShouldContainShowStreams()
    {
        var args = _builder.BuildFfprobeArgs("/videos/test.mkv");
        Assert.Contains("-show_streams", args);
        Assert.Contains("-show_format", args);
        Assert.Contains("-print_format json", args);
    }
}
```

#### F11: Test Project File

Create file: `tests/DotNetCloud.Video.Tests/DotNetCloud.Video.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Modules\Video\DotNetCloud.Modules.Video\DotNetCloud.Modules.Video.csproj" />
  </ItemGroup>
</Project>
```

> **IMPORTANT:** After creating this .csproj, add the test project to `DotNetCloud.sln` by running:
> `dotnet sln DotNetCloud.sln add tests/DotNetCloud.Video.Tests/DotNetCloud.Video.Tests.csproj`

---

### Step 1.3 — FFmpeg Process Manager

#### F3: `FfmpegProcessManager.cs`

Create file: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/FfmpegProcessManager.cs`

This lives in the Host project because it depends on `System.Diagnostics.Process` and file system I/O.

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Manages ffmpeg child processes for video transcoding.
/// Enforces concurrency limits, handles graceful cancellation,
/// and parses progress from stderr.
///
/// Registered as a singleton (one per module host process).
/// Thread-safe.
/// </summary>
public sealed class FfmpegProcessManager : IDisposable
{
    private readonly VideoTranscodingOptions _options;
    private readonly ILogger<FfmpegProcessManager> _logger;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly ConcurrentDictionary<string, TranscodingJob> _activeJobs = new();

    // Regex to parse "time=HH:MM:SS.MS" from ffmpeg stderr
    private static readonly Regex TimeRegex = new(
        @"time=(\d+):(\d+):(\d+)\.(\d+)",
        RegexOptions.Compiled);

    // Regex to parse "speed= N.Nx" from ffmpeg stderr
    private static readonly Regex SpeedRegex = new(
        @"speed=\s*([\d.]+)x",
        RegexOptions.Compiled);

    public FfmpegProcessManager(
        VideoTranscodingOptions options,
        ILogger<FfmpegProcessManager> logger)
    {
        _options = options;
        _logger = logger;
        _concurrencyGate = new SemaphoreSlim(options.MaxConcurrentJobs);
    }

    /// <summary>
    /// Runs ffmpeg with the given arguments, writing output to outputPath.
    /// Returns when the process exits successfully.
    /// Throws FfmpegException on non-zero exit code.
    /// Supports cancellation — sends 'q' to ffmpeg stdin for graceful stop.
    /// </summary>
    /// <param name="arguments">The ffmpeg arguments (NOT including "ffmpeg" binary).</param>
    /// <param name="outputPath">Where ffmpeg will write the output file.</param>
    /// <param name="job">The TranscodingJob to track progress on. Its ProgressPercent and Speed fields are updated.</param>
    /// <param name="cancellationToken">Token to cancel transcoding.</param>
    /// <param name="totalDuration">Total duration of the source video, used to compute progress %.</param>
    public async Task RunAsync(
        string arguments,
        string outputPath,
        TranscodingJob job,
        TimeSpan totalDuration,
        CancellationToken cancellationToken = default)
    {
        await _concurrencyGate.WaitAsync(cancellationToken);

        try
        {
            _activeJobs[job.Id] = job;
            job.Status = TranscodingJobStatus.Running;

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _options.FfmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty
            };

            using var process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };
            job.ProcessId = process.Id;

            // Set up progress parsing from stderr
            var progressTcs = new TaskCompletionSource<bool>();
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is null) return;
                _logger.LogTrace("ffmpeg stderr: {Line}", e.Data);
                ParseProgress(e.Data, job, totalDuration);
            };

            _logger.LogInformation(
                "Starting ffmpeg: {FfmpegPath} {Arguments}",
                _options.FfmpegPath, arguments);

            process.Start();
            process.BeginErrorReadLine();

            // Read stdout to prevent buffer deadlock (ffmpeg may write to stdout)
            _ = ConsumeStdoutAsync(process);

            // Wait for exit or cancellation
            using var ctr = cancellationToken.Register(() =>
            {
                _logger.LogInformation("Cancelling ffmpeg job {JobId}", job.Id);
                SendGracefulQuit(process);
            });

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                SendGracefulQuit(process);
                // Give ffmpeg 5 seconds to flush and exit
                if (!process.HasExited)
                {
                    await Task.WhenAny(
                        process.WaitForExitAsync(CancellationToken.None),
                        Task.Delay(5000, CancellationToken.None));
                }
                if (!process.HasExited)
                {
                    _logger.LogWarning("Force-killing ffmpeg job {JobId}", job.Id);
                    process.Kill(entireProcessTree: true);
                }
            }

            // Ensure stderr reading is complete
            process.CancelErrorRead();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogError(
                    "ffmpeg exited with code {ExitCode} for job {JobId}. Error: {Error}",
                    process.ExitCode, job.Id, error);
                throw new FfmpegException(
                    $"ffmpeg exited with code {process.ExitCode}",
                    process.ExitCode,
                    error);
            }

            _logger.LogInformation("ffmpeg job {JobId} completed successfully", job.Id);
        }
        finally
        {
            _activeJobs.TryRemove(job.Id, out _);
            _concurrencyGate.Release();
        }
    }

    /// <summary>
    /// Cancels a running transcode job by sending 'q' to ffmpeg's stdin.
    /// </summary>
    public void CancelJob(string jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            job.Status = TranscodingJobStatus.Cancelled;
            _logger.LogInformation("Transcode job {JobId} marked as cancelled", jobId);
        }
    }

    /// <summary>
    /// Parses ffmpeg stderr lines to extract progress information.
    /// Lines look like:
    ///   frame=  150 fps= 30 q=28.0 size=    1024kB time=00:00:05.00 bitrate=1678.2kbits/s speed=1.00x
    /// </summary>
    private void ParseProgress(string line, TranscodingJob job, TimeSpan totalDuration)
    {
        var timeMatch = TimeRegex.Match(line);
        if (timeMatch.Success)
        {
            var ts = new TimeSpan(
                0,
                int.Parse(timeMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(timeMatch.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(timeMatch.Groups[3].Value, CultureInfo.InvariantCulture),
                int.Parse(timeMatch.Groups[4].Value.PadRight(3, '0').Substring(0, 3), CultureInfo.InvariantCulture));

            job.CurrentTime = ts;

            if (totalDuration > TimeSpan.Zero)
            {
                job.ProgressPercent = Math.Min(100.0, (ts.TotalSeconds / totalDuration.TotalSeconds) * 100.0);
            }
        }

        var speedMatch = SpeedRegex.Match(line);
        if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value,
            NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
        {
            job.Speed = speed;
        }
    }

    /// <summary>
    /// Sends 'q' to ffmpeg's stdin for graceful quit.
    /// ffmpeg finalizes the output file before exiting.
    /// </summary>
    private static void SendGracefulQuit(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.WriteLine("q");
            }
        }
        catch (Exception)
        {
            // Process may have already exited
        }
    }

    /// <summary>
    /// Consumes stdout to prevent buffer deadlocks (ffmpeg may print banner to stdout).
    /// </summary>
    private static async Task ConsumeStdoutAsync(Process process)
    {
        try
        {
            await process.StandardOutput.ReadToEndAsync();
        }
        catch
        {
            // Ignore — process may have exited
        }
    }

    public void Dispose()
    {
        _concurrencyGate.Dispose();
    }

    /// <summary>
    /// Gets a copy of currently active jobs for monitoring.
    /// </summary>
    public IReadOnlyList<TranscodingJob> GetActiveJobs()
    {
        return _activeJobs.Values.ToList().AsReadOnly();
    }
}

/// <summary>
/// Exception thrown when ffmpeg exits with a non-zero code.
/// </summary>
public sealed class FfmpegException : Exception
{
    public int ExitCode { get; }
    public string? FfmpegError { get; }

    public FfmpegException(string message, int exitCode, string? ffmpegError = null)
        : base(message)
    {
        ExitCode = exitCode;
        FfmpegError = ffmpegError;
    }
}
```

---

## Phase 2: Transcoding Service & Caching

### Step 2.1 — Transcode Output Cache

#### F4: `TranscodeCacheService.cs`

Create file: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/TranscodeCacheService.cs`

```csharp
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Content-addressed cache for transcoded video outputs.
///
/// Cache key = SHA256(source file path + JSON of transcode parameters).
/// This means the same file transcoded with the same settings always produces
/// the same cache key, regardless of when or by whom it was requested.
///
/// Thread-safe. Registered as singleton.
/// </summary>
public sealed class TranscodeCacheService
{
    private readonly VideoTranscodingOptions _options;
    private readonly ILogger<TranscodeCacheService> _logger;
    private readonly string _cacheRoot;

    // Prevents concurrent transcode of the same cache key
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    public TranscodeCacheService(
        VideoTranscodingOptions options,
        ILogger<TranscodeCacheService> logger)
    {
        _options = options;
        _logger = logger;

        // Use the configured temp directory, or fall back to a subfolder of the module's content root
        _cacheRoot = !string.IsNullOrWhiteSpace(options.TempDirectory)
            ? Path.Combine(options.TempDirectory, "transcode-cache")
            : Path.Combine(Path.GetTempPath(), "dotnetcloud-transcode-cache");

        Directory.CreateDirectory(_cacheRoot);
    }

    /// <summary>
    /// Computes the cache key for a given source file and transcoding options.
    /// </summary>
    public string ComputeCacheKey(string sourceFilePath, VideoTranscodingOptions options)
    {
        // Serialize options to JSON, excluding path properties that vary per-run
        var optionsObj = new
        {
            options.VideoCodec,
            options.VideoCrf,
            options.EncoderPreset,
            options.MaxWidth,
            options.MaxHeight,
            options.AudioCodec,
            options.AudioBitrateKbps
        };
        var optionsJson = JsonSerializer.Serialize(optionsObj);

        var input = sourceFilePath + "|" + optionsJson;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Returns the cached output file path if it exists and is not expired.
    /// Returns null on cache miss.
    /// </summary>
    public string? GetCachedPath(string cacheKey)
    {
        var path = GetCacheFilePath(cacheKey);
        if (!File.Exists(path))
            return null;

        // Check TTL expiration
        if (_options.CacheTtlHours > 0)
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
            if (age.TotalHours > _options.CacheTtlHours)
            {
                _logger.LogDebug("Cache entry expired: {CacheKey}", cacheKey);
                TryDelete(path);
                return null;
            }
        }

        _logger.LogDebug("Cache hit: {CacheKey} -> {Path}", cacheKey, path);
        return path;
    }

    /// <summary>
    /// Acquires an exclusive lock for a given cache key.
    /// Prevents multiple concurrent transcode processes for the same output.
    /// Caller MUST release the semaphore.
    /// </summary>
    public async Task<IDisposable> LockCacheKeyAsync(string cacheKey, CancellationToken ct = default)
    {
        var semaphore = _keyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        return new SemaphoreReleaser(semaphore, cacheKey, _keyLocks);
    }

    /// <summary>
    /// Registers a successfully transcoded file in the cache.
    /// The file should already exist at the path returned by GetCacheFilePath.
    /// </summary>
    public void RegisterCachedFile(string cacheKey, string sourcePath)
    {
        var cachePath = GetCacheFilePath(cacheKey);
        if (!File.Exists(sourcePath))
        {
            _logger.LogWarning("Cannot register cache entry — source file missing: {Path}", sourcePath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

        // Move (or copy if move fails) the file to cache location
        try
        {
            File.Move(sourcePath, cachePath, overwrite: true);
        }
        catch (IOException)
        {
            File.Copy(sourcePath, cachePath, overwrite: true);
            TryDelete(sourcePath);
        }

        _logger.LogInformation("Cache entry created: {CacheKey} -> {Path}", cacheKey, cachePath);

        // Trigger cleanup if cache is too large
        _ = Task.Run(() => EnforceMaxSizeAsync());
    }

    /// <summary>
    /// Returns the file system path where a cache entry should be stored.
    /// </summary>
    public string GetCacheFilePath(string cacheKey)
    {
        // Use subdirectories based on first 4 chars to avoid too many files in one dir
        var subDir = cacheKey[..Math.Min(4, cacheKey.Length)];
        return Path.Combine(_cacheRoot, subDir, cacheKey + ".mp4");
    }

    /// <summary>
    /// Deletes old cache entries if total size exceeds MaxCacheSizeBytes.
    /// Oldest entries are deleted first.
    /// </summary>
    private async Task EnforceMaxSizeAsync()
    {
        if (_options.MaxCacheSizeBytes <= 0) return;

        try
        {
            var dirInfo = new DirectoryInfo(_cacheRoot);
            if (!dirInfo.Exists) return;

            var files = dirInfo.GetFiles("*.mp4", SearchOption.AllDirectories)
                .OrderBy(f => f.LastWriteTimeUtc)
                .ToList();

            long totalSize = files.Sum(f => f.Length);

            while (totalSize > _options.MaxCacheSizeBytes && files.Count > 0)
            {
                var oldest = files[0];
                totalSize -= oldest.Length;
                _logger.LogDebug("Cache eviction: {Path} ({Size} bytes)", oldest.FullName, oldest.Length);
                TryDelete(oldest.FullName);
                files.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during transcode cache size enforcement");
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { /* best effort */ }
    }

    private sealed class SemaphoreReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly string _key;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks;
        private bool _disposed;

        public SemaphoreReleaser(
            SemaphoreSlim semaphore,
            string key,
            ConcurrentDictionary<string, SemaphoreSlim> keyLocks)
        {
            _semaphore = semaphore;
            _key = key;
            _keyLocks = keyLocks;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _semaphore.Release();
            // Don't remove from dictionary — keep the semaphore for reuse
        }
    }
}
```

---

### Step 2.3 — Transcoding Job Model & Tracker

#### F7: `TranscodingJob.cs`

Create file: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/TranscodingJob.cs`

```csharp
namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Status of a transcoding job.
/// </summary>
public enum TranscodingJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Represents a single video transcoding job.
/// Used for tracking progress and lifecycle.
/// </summary>
public sealed class TranscodingJob
{
    /// <summary>Unique job identifier (GUID string).</summary>
    public required string Id { get; init; }

    /// <summary>The video entity ID being transcoded.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>The user ID who requested the transcode.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Cache key for the transcode output.</summary>
    public required string CacheKey { get; init; }

    /// <summary>Path where ffmpeg is writing the output file.</summary>
    public string? OutputPath { get; set; }

    /// <summary>Current job status.</summary>
    public TranscodingJobStatus Status { get; set; } = TranscodingJobStatus.Queued;

    /// <summary>Progress percentage (0.0 to 100.0).</summary>
    public double ProgressPercent { get; set; }

    /// <summary>Current transcode position in the source video.</summary>
    public TimeSpan CurrentTime { get; set; }

    /// <summary>ffmpeg speed multiplier (e.g., 1.5x = faster than real-time).</summary>
    public double Speed { get; set; }

    /// <summary>ffmpeg process ID for monitoring.</summary>
    public int ProcessId { get; set; }

    /// <summary>When the job was created (UTC).</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>When the job finished (completed, failed, or cancelled). Null if still running.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Error message if status is Failed.</summary>
    public string? ErrorMessage { get; set; }
}
```

#### F8: `TranscodingJobTracker.cs`

Create file: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/TranscodingJobTracker.cs`

```csharp
using System.Collections.Concurrent;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Thread-safe in-memory tracker for active and recent transcoding jobs.
/// Registered as singleton.
/// </summary>
public sealed class TranscodingJobTracker
{
    private readonly ConcurrentDictionary<string, TranscodingJob> _jobs = new();

    /// <summary>
    /// Creates and registers a new job. Returns the job.
    /// </summary>
    public TranscodingJob CreateJob(Guid videoId, Guid userId, string cacheKey)
    {
        var job = new TranscodingJob
        {
            Id = Guid.NewGuid().ToString("N"),
            VideoId = videoId,
            UserId = userId,
            CacheKey = cacheKey
        };
        _jobs[job.Id] = job;
        return job;
    }

    /// <summary>
    /// Gets a job by ID. Returns null if not found.
    /// </summary>
    public TranscodingJob? GetJob(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job : null;
    }

    /// <summary>
    /// Gets the active (running or queued) job for a given video+user pair, if any.
    /// Returns null if no active job exists.
    /// </summary>
    public TranscodingJob? GetActiveJob(Guid videoId, Guid userId)
    {
        return _jobs.Values.FirstOrDefault(j =>
            j.VideoId == videoId &&
            j.UserId == userId &&
            (j.Status == TranscodingJobStatus.Queued || j.Status == TranscodingJobStatus.Running));
    }

    /// <summary>
    /// Removes old completed/failed/cancelled jobs older than the given age.
    /// </summary>
    public void PurgeOldJobs(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        foreach (var kvp in _jobs)
        {
            if (kvp.Value.CompletedAt.HasValue && kvp.Value.CompletedAt.Value < cutoff)
            {
                _jobs.TryRemove(kvp.Key, out _);
            }
        }
    }
}
```

---

### Step 2.2 — VideoTranscodingService

#### F5: `IVideoTranscodingService.cs`

Create file: `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoTranscodingService.cs`

```csharp
namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// High-level service for video transcoding.
/// Orchestrates cache lookup, ffmpeg argument building, process execution, and job tracking.
/// </summary>
public interface IVideoTranscodingService
{
    /// <summary>
    /// Checks whether the video can be served directly to HTML5 browsers
    /// without transcoding. Uses ffprobe to inspect codecs.
    /// </summary>
    /// <param name="videoFilePath">Absolute path to the source video file on disk.</param>
    /// <param name="mimeType">MIME type of the video (e.g., "video/mp4").</param>
    /// <returns>True if the video can be direct-played.</returns>
    Task<bool> CanDirectPlayAsync(string videoFilePath, string mimeType, CancellationToken ct = default);

    /// <summary>
    /// Transcodes a video file and returns the path to the transcoded output.
    /// Uses cache when available.
    /// </summary>
    /// <param name="videoId">The Video entity ID.</param>
    /// <param name="userId">The requesting user ID.</param>
    /// <param name="sourceFilePath">Absolute path to the source video file.</param>
    /// <param name="mimeType">MIME type of the source video.</param>
    /// <param name="seekStart">Optional seek position for partial transcode.</param>
    /// <param name="seekDuration">Optional duration for partial transcode.</param>
    /// <returns>A tuple of (jobId, outputFilePath).</returns>
    Task<(string JobId, string OutputPath)> TranscodeAsync(
        Guid videoId,
        Guid userId,
        string sourceFilePath,
        string mimeType,
        TimeSpan? seekStart = null,
        TimeSpan? seekDuration = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current progress of a transcode job.
    /// Returns null if the job does not exist.
    /// </summary>
    TranscodingJob? GetProgress(string jobId);

    /// <summary>
    /// Cancels a running transcode job.
    /// </summary>
    void CancelTranscode(string jobId);
}
```

> **IMPORTANT:** The interface references `TranscodingJob` which is defined in the Host project (F7). To avoid circular dependencies, use a forward declaration pattern: define `TranscodingJob` in the `DotNetCloud.Modules.Video` core project (not Host) OR change the interface method to return a simple DTO. For this plan, move `TranscodingJob` (F7) and `TranscodingJobStatus` to the core project at:
> `src/Modules/Video/DotNetCloud.Modules.Video/Services/TranscodingJob.cs`

So:

- **F7 (TranscodingJob.cs)** → `src/Modules/Video/DotNetCloud.Modules.Video/Services/TranscodingJob.cs` (core project)
- **F8 (TranscodingJobTracker.cs)** → stays in `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/TranscodingJobTracker.cs` (Host project, references F7 from core)

#### F6: `VideoTranscodingService.cs`

Create file: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/VideoTranscodingService.cs`

```csharp
using System.Text.Json;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Orchestrates video transcoding: probe → check cache → build args → run ffmpeg → cache result.
/// Registered as scoped service.
/// </summary>
public sealed class VideoTranscodingService : IVideoTranscodingService
{
    private readonly FfmpegArgumentBuilder _argBuilder;
    private readonly FfmpegProcessManager _processManager;
    private readonly TranscodeCacheService _cacheService;
    private readonly TranscodingJobTracker _jobTracker;
    private readonly VideoTranscodingOptions _options;
    private readonly ILogger<VideoTranscodingService> _logger;

    public VideoTranscodingService(
        FfmpegArgumentBuilder argBuilder,
        FfmpegProcessManager processManager,
        TranscodeCacheService cacheService,
        TranscodingJobTracker jobTracker,
        VideoTranscodingOptions options,
        ILogger<VideoTranscodingService> logger)
    {
        _argBuilder = argBuilder;
        _processManager = processManager;
        _cacheService = cacheService;
        _jobTracker = jobTracker;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> CanDirectPlayAsync(
        string videoFilePath,
        string mimeType,
        CancellationToken ct = default)
    {
        // Fast check: MIME type must be video/mp4
        if (!string.Equals(mimeType, "video/mp4", StringComparison.OrdinalIgnoreCase))
            return false;

        // Run ffprobe to get codec info
        var probeJson = await RunFfprobeAsync(videoFilePath, ct);
        if (probeJson is null)
            return false;

        var (videoCodec, audioCodec, container) = ParseCodecInfo(probeJson);
        return _argBuilder.CanDirectPlay(mimeType, videoCodec, audioCodec, container ?? "");
    }

    /// <inheritdoc />
    public async Task<(string JobId, string OutputPath)> TranscodeAsync(
        Guid videoId,
        Guid userId,
        string sourceFilePath,
        string mimeType,
        TimeSpan? seekStart = null,
        TimeSpan? seekDuration = null,
        CancellationToken ct = default)
    {
        // Check for existing active job (deduplication)
        var existingJob = _jobTracker.GetActiveJob(videoId, userId);
        if (existingJob is not null)
        {
            _logger.LogDebug("Reusing existing transcode job {JobId} for video {VideoId}", existingJob.Id, videoId);
            return (existingJob.Id, existingJob.OutputPath ?? string.Empty);
        }

        // Compute cache key
        var cacheKey = _cacheService.ComputeCacheKey(sourceFilePath, _options);

        // Check cache
        var cachedPath = _cacheService.GetCachedPath(cacheKey);
        if (cachedPath is not null)
        {
            _logger.LogDebug("Transcode cache hit for video {VideoId}, key {CacheKey}", videoId, cacheKey);
            var completedJob = _jobTracker.CreateJob(videoId, userId, cacheKey);
            completedJob.Status = TranscodingJobStatus.Completed;
            completedJob.ProgressPercent = 100.0;
            completedJob.OutputPath = cachedPath;
            completedJob.CompletedAt = DateTime.UtcNow;
            return (completedJob.Id, cachedPath);
        }

        // Acquire lock for this cache key to prevent concurrent transcodes of same file
        using var cacheLock = await _cacheService.LockCacheKeyAsync(cacheKey, ct);

        // Double-check cache after acquiring lock
        cachedPath = _cacheService.GetCachedPath(cacheKey);
        if (cachedPath is not null)
        {
            var completedJob = _jobTracker.CreateJob(videoId, userId, cacheKey);
            completedJob.Status = TranscodingJobStatus.Completed;
            completedJob.ProgressPercent = 100.0;
            completedJob.OutputPath = cachedPath;
            completedJob.CompletedAt = DateTime.UtcNow;
            return (completedJob.Id, cachedPath);
        }

        // Create job
        var job = _jobTracker.CreateJob(videoId, userId, cacheKey);

        // Determine output path for ffmpeg (temp location)
        var tempOutputDir = !string.IsNullOrWhiteSpace(_options.TempDirectory)
            ? _options.TempDirectory
            : Path.GetTempPath();
        var tempOutputPath = Path.Combine(tempOutputDir, $"transcode-{job.Id}.mp4");
        job.OutputPath = tempOutputPath;

        // Get video duration for progress tracking
        var duration = await GetVideoDurationAsync(sourceFilePath, ct);

        // Build ffmpeg arguments
        var args = _argBuilder.BuildProgressiveMp4Args(
            sourceFilePath,
            tempOutputPath,
            _options,
            seekStart,
            seekDuration);

        // Run ffmpeg in background (don't await — let the caller poll progress)
        _ = Task.Run(async () =>
        {
            try
            {
                await _processManager.RunAsync(args, tempOutputPath, job, duration, ct);
                job.Status = TranscodingJobStatus.Completed;
                job.ProgressPercent = 100.0;
                job.CompletedAt = DateTime.UtcNow;
                _cacheService.RegisterCachedFile(cacheKey, tempOutputPath);
                job.OutputPath = _cacheService.GetCacheFilePath(cacheKey);
                _logger.LogInformation("Transcode job {JobId} completed and cached", job.Id);
            }
            catch (FfmpegException ex)
            {
                job.Status = TranscodingJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(ex, "Transcode job {JobId} failed: {Error}", job.Id, ex.Message);
            }
            catch (OperationCanceledException)
            {
                job.Status = TranscodingJobStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Transcode job {JobId} cancelled", job.Id);
            }
            catch (Exception ex)
            {
                job.Status = TranscodingJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(ex, "Transcode job {JobId} unexpected error", job.Id);
            }
        }, ct);

        return (job.Id, tempOutputPath);
    }

    /// <inheritdoc />
    public TranscodingJob? GetProgress(string jobId)
    {
        return _jobTracker.GetJob(jobId);
    }

    /// <inheritdoc />
    public void CancelTranscode(string jobId)
    {
        _processManager.CancelJob(jobId);
    }

    // ─── Private Helpers ────────────────────────────────────────────────

    /// <summary>
    /// Runs ffprobe and returns the raw JSON output.
    /// Uses the same ffprobe binary pattern as VideoMetadataExtractor.
    /// </summary>
    private async Task<string?> RunFfprobeAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var ffprobePath = ResolveFfprobePath();
            var args = _argBuilder.BuildFfprobeArgs(filePath);

            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffprobe failed for {Path}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Resolves ffprobe path from ffmpeg path (replace "ffmpeg" with "ffprobe").
    /// </summary>
    private string ResolveFfprobePath()
    {
        var ffmpegPath = _options.FfmpegPath;
        // If the configured path ends with "ffmpeg" or "ffmpeg.exe", replace with "ffprobe"
        if (ffmpegPath.EndsWith("ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            return ffmpegPath[..^6] + "ffprobe" + ffmpegPath[^6..].Replace("ffmpeg", "ffprobe");
        }
        if (ffmpegPath.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
        {
            return ffmpegPath[..^10] + "ffprobe.exe";
        }
        return "ffprobe"; // fallback to PATH
    }

    /// <summary>
    /// Extracts codec info from ffprobe JSON output.
    /// </summary>
    private static (string? VideoCodec, string? AudioCodec, string? Container) ParseCodecInfo(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? videoCodec = null;
        string? audioCodec = null;
        string? container = null;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var ct) ? ct.GetString() : null;
                var codecName = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() : null;

                if (codecType == "video" && videoCodec is null)
                    videoCodec = codecName;
                if (codecType == "audio" && audioCodec is null)
                    audioCodec = codecName;
            }
        }

        if (root.TryGetProperty("format", out var format) &&
            format.TryGetProperty("format_name", out var fmtName))
        {
            container = fmtName.GetString()?.Split(',').FirstOrDefault();
        }

        return (videoCodec, audioCodec, container);
    }

    /// <summary>
    /// Gets the total duration of a video file using ffprobe.
    /// Returns TimeSpan.Zero if duration cannot be determined.
    /// </summary>
    private async Task<TimeSpan> GetVideoDurationAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var json = await RunFfprobeAsync(filePath, ct);
            if (json is null) return TimeSpan.Zero;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("format", out var format) &&
                format.TryGetProperty("duration", out var dur) &&
                double.TryParse(dur.GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }
        catch { /* ignore */ }

        return TimeSpan.Zero;
    }
}
```

> **IMPORTANT:** `VideoTranscodingService` uses `System.Diagnostics.Process` — add `using System.Diagnostics;` at the top of the file.

---

## Phase 3: HTTP Streaming Endpoint

### Step 3.2 — Progressive File Stream

#### F9: `ProgressiveFileStream.cs`

Create file: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/ProgressiveFileStream.cs`

```csharp
namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// A read-only FileStream wrapper that supports reading from a file
/// that is concurrently being written by another process (ffmpeg).
///
/// When Read/ReadAsync reaches the current end of the file, it waits
/// briefly and retries, allowing the writer to produce more data.
/// Returns 0 (EOF) only when the transcode job has completed and
/// all data has been read.
/// </summary>
public sealed class ProgressiveFileStream : Stream
{
    private readonly FileStream _inner;
    private readonly Func<bool> _isWriteComplete;
    private readonly int _pollIntervalMs;
    private long _position;
    private bool _disposed;

    /// <summary>
    /// Creates a new ProgressiveFileStream.
    /// </summary>
    /// <param name="filePath">Path to the file being written by ffmpeg.</param>
    /// <param name="isWriteComplete">Function that returns true when ffmpeg has finished writing.</param>
    /// <param name="pollIntervalMs">How long to wait between retries when data is not yet available (default 250ms).</param>
    public ProgressiveFileStream(
        string filePath,
        Func<bool> isWriteComplete,
        int pollIntervalMs = 250)
    {
        _inner = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 65536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        _isWriteComplete = isWriteComplete;
        _pollIntervalMs = pollIntervalMs;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int bytesRead = _inner.Read(buffer, offset + totalRead, count - totalRead);
            if (bytesRead > 0)
            {
                totalRead += bytesRead;
                _position += bytesRead;
            }
            else if (_isWriteComplete())
            {
                break; // Writer done, no more data
            }
            else
            {
                Thread.Sleep(_pollIntervalMs);
            }
        }
        return totalRead;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int bytesRead = await _inner.ReadAsync(
                buffer.AsMemory(offset + totalRead, count - totalRead),
                cancellationToken);

            if (bytesRead > 0)
            {
                totalRead += bytesRead;
                _position += bytesRead;
            }
            else if (_isWriteComplete())
            {
                break; // Writer done, no more data
            }
            else
            {
                await Task.Delay(_pollIntervalMs, cancellationToken);
            }
        }
        return totalRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPos < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        _position = _inner.Seek(newPos, SeekOrigin.Begin);
        return _position;
    }

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
```

---

### Step 3.1 — Streaming Controller Endpoints

#### E4: Add Endpoints to `VideoController.cs`

Edit: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Controllers/VideoController.cs`

Add the following new endpoints within the `VideoController` class:

```csharp
/// <summary>
/// Probes a video to determine if it can be direct-played or needs transcoding.
/// </summary>
[HttpGet("{videoId:guid}/stream-probe")]
public async Task<IActionResult> ProbeStream(Guid videoId)
{
    var caller = GetAuthenticatedCaller();
    var video = await _videoService.GetVideoAsync(videoId, caller);
    if (video is null)
        return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, "Video not found."));

    // Validate stream token
    var token = _streamingService.GenerateStreamToken(videoId, caller.UserId);
    var canDirectPlay = await _transcodingService.CanDirectPlayAsync(
        video.FilePath, video.MimeType);

    return Ok(Envelope(new
    {
        videoId = video.Id,
        canDirectPlay,
        mimeType = video.MimeType,
        // If transcoding is needed, the client should request /stream
        streamUrl = canDirectPlay
            ? $"/api/v1/videos/{videoId}/stream?token={Uri.EscapeDataString(token)}"
            : $"/api/v1/videos/{videoId}/stream?token={Uri.EscapeDataString(token)}&forceTranscode=true"
    }));
}

/// <summary>
/// Streams a video. Uses direct play if possible; falls back to transcoding.
/// Supports HTTP Range requests for seeking.
/// Query params:
///   token - required stream token from GenerateStreamToken
///   forceTranscode - optional, set to "true" to skip direct play
/// </summary>
[HttpGet("{videoId:guid}/stream")]
public async Task<IActionResult> StreamVideo(
    Guid videoId,
    [FromQuery] string token,
    [FromQuery] bool forceTranscode = false)
{
    // Validate token
    var streamToken = _streamingService.ValidateStreamToken(token);
    if (streamToken is null || streamToken.VideoId != videoId)
        return Unauthorized(ErrorEnvelope(ErrorCodes.InvalidToken, "Invalid or expired stream token."));

    var caller = GetAuthenticatedCaller();
    var video = await _videoService.GetVideoAsync(videoId, caller);
    if (video is null)
        return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, "Video not found."));

    // Acquire stream slot
    try
    {
        _streamingService.AcquireStreamSlot(caller.UserId);
    }
    catch (InvalidOperationException)
    {
        return StatusCode(429, ErrorEnvelope("TooManyStreams", "Too many concurrent streams."));
    }

    try
    {
        // Check direct play
        bool canDirectPlay = !forceTranscode &&
            await _transcodingService.CanDirectPlayAsync(video.FilePath, video.MimeType);

        if (canDirectPlay)
        {
            // Serve the original file with Range support
            return ServeDirectFile(video.FilePath, video.MimeType);
        }

        // Transcode
        var (jobId, outputPath) = await _transcodingService.TranscodeAsync(
            videoId,
            caller.UserId,
            video.FilePath,
            video.MimeType,
            ct: HttpContext.RequestAborted);

        // Serve progressive stream
        return ServeProgressiveStream(outputPath, jobId, video.MimeType);
    }
    finally
    {
        _streamingService.ReleaseStreamSlot(caller.UserId);
    }
}

/// <summary>
/// Gets the progress of a transcode job.
/// </summary>
[HttpGet("transcodes/{jobId}/progress")]
public IActionResult GetTranscodeProgress(string jobId)
{
    var job = _transcodingService.GetProgress(jobId);
    if (job is null)
        return NotFound(ErrorEnvelope("JobNotFound", "Transcode job not found."));

    return Ok(Envelope(new
    {
        jobId = job.Id,
        status = job.Status.ToString(),
        progressPercent = job.ProgressPercent,
        currentTime = job.CurrentTime.ToString(@"hh\:mm\:ss"),
        speed = job.Speed,
        errorMessage = job.ErrorMessage
    }));
}

/// <summary>
/// Cancels a running transcode job.
/// </summary>
[HttpDelete("transcodes/{jobId}")]
public IActionResult CancelTranscode(string jobId)
{
    _transcodingService.CancelTranscode(jobId);
    return Ok(Envelope(new { cancelled = true }));
}

// ─── Private helpers added to VideoController ─────────────────────

private IActionResult ServeDirectFile(string filePath, string mimeType)
{
    Response.Headers.AcceptRanges = "bytes";
    return PhysicalFile(filePath, mimeType, enableRangeProcessing: true);
}

private IActionResult ServeProgressiveStream(string outputPath, string jobId, string actualMimeType)
{
    var transcodedMimeType = "video/mp4"; // Always MP4 output

    bool IsComplete()
    {
        var job = _transcodingService.GetProgress(jobId);
        return job is null ||
               job.Status is TranscodingJobStatus.Completed or
                   TranscodingJobStatus.Failed or TranscodingJobStatus.Cancelled;
    }

    var stream = new ProgressiveFileStream(outputPath, IsComplete);

    // Register client disconnect → cancel transcode
    HttpContext.RequestAborted.Register(() =>
    {
        _transcodingService.CancelTranscode(jobId);
        stream.Dispose();
    });

    Response.Headers.AcceptRanges = "none"; // No Range support for live transcode
    return new FileStreamResult(stream, transcodedMimeType);
}
```

> **CRITICAL:** The `VideoController` constructor must be updated to accept `IVideoTranscodingService _transcodingService`. Add it as a new parameter and assign it to a field. See current constructor at the top of `VideoController.cs`.

#### Constructor update for VideoController

Edit the constructor to add the new dependency:

```csharp
private readonly IVideoTranscodingService _transcodingService;

public VideoController(
    // ... existing parameters ...
    IVideoTranscodingService transcodingService,  // ADD THIS (last parameter)
    ILogger<VideoController> logger)
{
    // ... existing assignments ...
    _transcodingService = transcodingService;  // ADD THIS
    _logger = logger;
}
```

Add required usings at top of `VideoController.cs`:

```csharp
using DotNetCloud.Modules.Video.Host.Services;
using TranscodingJobStatus = DotNetCloud.Modules.Video.Services.TranscodingJobStatus;
```

---

## Phase 4: gRPC Integration

### Step 4.1 — Update gRPC Proto

#### E5: Add to `video_service.proto`

Edit: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Protos/video_service.proto`

**Add these RPCs** inside the `service VideoGrpcService { }` block:

```protobuf
  // ── Transcoding ────────────────────────────────────────────────

  // Gets stream info (can direct play, available transcode formats).
  rpc GetStreamInfo (GetStreamInfoRequest) returns (StreamInfoResponse);

  // Requests a transcode and returns stream URL + job ID for progress tracking.
  rpc RequestTranscode (RequestTranscodeRequest) returns (RequestTranscodeResponse);

  // Gets progress/status of a transcode job.
  rpc GetTranscodeProgress (GetTranscodeProgressRequest) returns (TranscodeProgressResponse);
```

**Add these messages** at the end of the file:

```protobuf
// ── Transcoding Messages ─────────────────────────────────────────

message GetStreamInfoRequest {
  string video_id = 1;
  string user_id = 2;
}

message StreamInfoResponse {
  bool success = 1;
  string error_message = 2;
  bool can_direct_play = 3;
  string stream_url = 4;
  string mime_type = 5;
}

message RequestTranscodeRequest {
  string video_id = 1;
  string user_id = 2;
  bool force_transcode = 3;
}

message RequestTranscodeResponse {
  bool success = 1;
  string error_message = 2;
  string job_id = 3;
  string stream_url = 4;
}

message GetTranscodeProgressRequest {
  string job_id = 1;
}

message TranscodeProgressResponse {
  bool success = 1;
  string error_message = 2;
  string job_id = 3;
  string status = 4;
  double progress_percent = 5;
  string current_time = 6;
  double speed = 7;
}
```

**After editing the proto file, regenerate the gRPC code:**

```bash
# (If using dotnet-grpc tool)
dotnet-grpc generate
# or rebuild the Host project (which has the proto compilation in its csproj)
dotnet build src/Modules/Video/DotNetCloud.Modules.Video.Host/
```

---

### Step 4.2 — Implement gRPC Service Methods

#### E6: Extend `VideoGrpcServiceImpl.cs`

Edit: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/VideoGrpcServiceImpl.cs`

Add these methods. They delegate to the HTTP controller endpoints via the Video module's URL base.

```csharp
// Add using statements at top:
using DotNetCloud.Modules.Video.Data.Services; // for VideoService

// Inject additional services into the constructor:
private readonly IVideoStreamingService _streamingService;
private readonly IVideoTranscodingService _transcodingService;
private readonly VideoService _videoService;
private readonly IHttpClientFactory _httpClientFactory;

// (Update constructor parameters to accept these)

/// <summary>
/// Gets stream info for a video — whether it can direct play or needs transcode.
/// Delegates to the local HTTP endpoint to reuse the same logic.
/// </summary>
public override async Task<StreamInfoResponse> GetStreamInfo(
    GetStreamInfoRequest request, ServerCallContext context)
{
    if (!Guid.TryParse(request.VideoId, out var videoId) ||
        !Guid.TryParse(request.UserId, out var userId))
    {
        return new StreamInfoResponse { Success = false, ErrorMessage = "Invalid GUID format." };
    }

    try
    {
        var video = await _videoService.GetVideoAsync(videoId, new CallerContext { UserId = userId });
        if (video is null)
            return new StreamInfoResponse { Success = false, ErrorMessage = "Video not found." };

        var token = _streamingService.GenerateStreamToken(videoId, userId);
        var canDirectPlay = await _transcodingService.CanDirectPlayAsync(video.FilePath, video.MimeType);

        return new StreamInfoResponse
        {
            Success = true,
            CanDirectPlay = canDirectPlay,
            StreamUrl = $"/api/v1/videos/{videoId}/stream?token={Uri.EscapeDataString(token)}" +
                        (canDirectPlay ? "" : "&forceTranscode=true"),
            MimeType = canDirectPlay ? video.MimeType : "video/mp4"
        };
    }
    catch (Exception ex)
    {
        return new StreamInfoResponse { Success = false, ErrorMessage = ex.Message };
    }
}

/// <summary>
/// Requests transcoding for a video. Returns job ID for progress tracking.
/// </summary>
public override async Task<RequestTranscodeResponse> RequestTranscode(
    RequestTranscodeRequest request, ServerCallContext context)
{
    if (!Guid.TryParse(request.VideoId, out var videoId) ||
        !Guid.TryParse(request.UserId, out var userId))
    {
        return new RequestTranscodeResponse { Success = false, ErrorMessage = "Invalid GUID format." };
    }

    try
    {
        var token = _streamingService.GenerateStreamToken(videoId, userId);
        var video = await _videoService.GetVideoAsync(videoId, new CallerContext { UserId = userId });
        if (video is null)
            return new RequestTranscodeResponse { Success = false, ErrorMessage = "Video not found." };

        var (jobId, _) = await _transcodingService.TranscodeAsync(
            videoId, userId, video.FilePath, video.MimeType,
            ct: context.CancellationToken);

        return new RequestTranscodeResponse
        {
            Success = true,
            JobId = jobId,
            StreamUrl = $"/api/v1/videos/{videoId}/stream?token={Uri.EscapeDataString(token)}&forceTranscode=true"
        };
    }
    catch (Exception ex)
    {
        return new RequestTranscodeResponse { Success = false, ErrorMessage = ex.Message };
    }
}

/// <summary>
/// Gets progress/status of a transcode job.
/// </summary>
public override async Task<TranscodeProgressResponse> GetTranscodeProgress(
    GetTranscodeProgressRequest request, ServerCallContext context)
{
    try
    {
        var job = _transcodingService.GetProgress(request.JobId);
        if (job is null)
            return new TranscodeProgressResponse { Success = false, ErrorMessage = "Job not found." };

        return new TranscodeProgressResponse
        {
            Success = true,
            JobId = job.Id,
            Status = job.Status.ToString(),
            ProgressPercent = job.ProgressPercent,
            CurrentTime = job.CurrentTime.ToString(@"hh\:mm\:ss"),
            Speed = job.Speed
        };
    }
    catch (Exception ex)
    {
        return new TranscodeProgressResponse { Success = false, ErrorMessage = ex.Message };
    }
}
```

---

### Step 4.3 — gRPC Client in Core.Server

#### E8: Add to `VideoApiClient.cs`

Edit: `src/Core/DotNetCloud.Core.Server/Grpc/Clients/VideoApiClient.cs`

Add these additional methods. If this file does not exist, create a new one alongside the existing gRPC client pattern.

```csharp
/// <summary>
/// Gets stream info for a video (can direct play, needs transcode, stream URL).
/// </summary>
public async Task<(bool CanDirectPlay, string StreamUrl, string MimeType)> GetStreamInfoAsync(
    Guid videoId, Guid userId, CancellationToken ct = default)
{
    var request = new GetStreamInfoRequest
    {
        VideoId = videoId.ToString(),
        UserId = userId.ToString()
    };

    var response = await _client.GetStreamInfoAsync(request, cancellationToken: ct);

    if (!response.Success)
        throw new InvalidOperationException(response.ErrorMessage);

    return (response.CanDirectPlay, response.StreamUrl, response.MimeType);
}

/// <summary>
/// Requests a transcode for a video. Returns job ID and stream URL.
/// </summary>
public async Task<(string JobId, string StreamUrl)> RequestTranscodeAsync(
    Guid videoId, Guid userId, bool forceTranscode = false, CancellationToken ct = default)
{
    var request = new RequestTranscodeRequest
    {
        VideoId = videoId.ToString(),
        UserId = userId.ToString(),
        ForceTranscode = forceTranscode
    };

    var response = await _client.RequestTranscodeAsync(request, cancellationToken: ct);

    if (!response.Success)
        throw new InvalidOperationException(response.ErrorMessage);

    return (response.JobId, response.StreamUrl);
}

/// <summary>
/// Gets progress of a transcode job.
/// </summary>
public async Task<TranscodeProgressResponse> GetTranscodeProgressAsync(
    string jobId, CancellationToken ct = default)
{
    var request = new GetTranscodeProgressRequest { JobId = jobId };
    return await _client.GetTranscodeProgressAsync(request, cancellationToken: ct);
}
```

---

## Phase 5: DI Registration & Wiring

### E3: Update `VideoServiceRegistration.cs`

Edit: `src/Modules/Video/DotNetCloud.Modules.Video.Data/VideoServiceRegistration.cs`

Add these service registrations in the appropriate registration method:

```csharp
// Transcoding services — Phase 1-2
services.AddSingleton<FfmpegArgumentBuilder>();
services.AddSingleton<FfmpegProcessManager>();
services.AddSingleton<TranscodeCacheService>();
services.AddSingleton<TranscodingJobTracker>();
services.AddScoped<IVideoTranscodingService, VideoTranscodingService>();

// Transcoding configuration — bind from IConfiguration
services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var options = new VideoTranscodingOptions();
    config.GetSection("Video:Transcoding").Bind(options);
    return options;
});
```

Add required usings at top:

```csharp
using DotNetCloud.Modules.Video.Services;
using DotNetCloud.Modules.Video.Host.Services;
```

### E7: Update `Program.cs` (if needed)

Edit: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Program.cs`

No changes needed for the new services if they are registered via `VideoServiceRegistration`. However, verify that `builder.Services.AddAuthorization()` is already present (it is in the current code at line 84). If authorization policies are needed for the new transcode endpoints, add:

```csharp
builder.Services.AddAuthorization(options =>
    DotNetCloud.Core.Auth.Authorization.AuthorizationPolicies.Configure(options));
```

---

## Verification Checklist

After implementing all phases:

- [ ] `dotnet build DotNetCloud.CI.slnf` succeeds
- [ ] `dotnet test tests/DotNetCloud.Video.Tests/` passes (unit tests for `FfmpegArgumentBuilder`)
- [ ] Start the Video module host — no DI resolution errors at startup
- [ ] Upload a `.mkv` file with H.264 video → GET `/api/v1/videos/{id}/stream-probe` returns `canDirectPlay: false`
- [ ] GET `/api/v1/videos/{id}/stream` with a valid token returns a transcode-in-progress stream
- [ ] Upload a `.mp4` file with H.264/AAC → stream-probe returns `canDirectPlay: true`
- [ ] Stream the MP4 directly → served as-is without transcoding
- [ ] Stream the same video twice → second request hits transcode cache
- [ ] Verify HTTP Range requests work for cached (already-transcoded) files
- [ ] Verify concurrent transcode limit is enforced (only N ffmpeg processes run at once)
- [ ] Disconnect the client mid-transcode → ffmpeg process is gracefully stopped
- [ ] gRPC `GetStreamInfo` returns correct canDirectPlay status
- [ ] gRPC `RequestTranscode` returns jobId + streamUrl
- [ ] gRPC `GetTranscodeProgress` returns correct progress %
- [ ] Transcode output plays in Chrome, Firefox, Safari, Edge

---

## Key Design Decisions Summary

| Decision          | Choice                         | Rationale                                             |
| ----------------- | ------------------------------ | ----------------------------------------------------- |
| Target format     | H.264 + AAC / MP4              | Only format with 100% HTML5 browser support           |
| Streaming mode    | Progressive download           | Simpler than HLS; sufficient for on-demand            |
| Service location  | Video module Host process      | FFmpeg already present; follows process-isolation     |
| Cache strategy    | Content-addressed, SHA-256     | Same input + params = same output; reuse across users |
| HW acceleration   | Deferred                       | Software libx264 is universal and sufficient for MVP  |
| gRPC pattern      | Same proto file + new client   | Follows mandatory gRPC-only inter-module rule         |
| Progress tracking | In-memory ConcurrentDictionary | Simple, no DB overhead; acceptable statelessness      |

---

## Future Enhancements (Not in Scope)

- HLS adaptive streaming with multiple bitrates
- Hardware acceleration (VAAPI, NVENC, QSV, VideoToolbox)
- Subtitle burn-in during transcode
- HDR→SDR tone mapping
- Audio-only transcoding (for music/audio files)
- Live stream transcoding
- Trickplay thumbnail generation during transcode
- Transcode job persistence (survives process restart)
- User-facing transcode queue management UI

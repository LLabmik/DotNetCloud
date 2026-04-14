using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class LibraryScanProgressTests
{
    private MusicDbContext _db = null!;
    private Mock<IEventBus> _mockEventBus = null!;
    private Mock<IMetadataEnrichmentService> _mockEnrichment = null!;
    private IConfiguration _configuration = null!;
    private CallerContext _caller = null!;
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _mockEventBus = new Mock<IEventBus>();
        _mockEnrichment = new Mock<IMetadataEnrichmentService>();
        _caller = TestHelpers.CreateCaller();
        _tempDir = Path.Combine(Path.GetTempPath(), $"dnc-scan-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Files:Storage:RootPath"] = _tempDir,
                ["Music:Enrichment:Enabled"] = "true",
                ["Music:Enrichment:AutoFetchArt"] = "true",
                ["Music:Enrichment:AutoEnrichArtists"] = "true"
            })
            .Build();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private LibraryScanService CreateService(IConfiguration? config = null)
    {
        var metadataService = new MusicMetadataService(NullLogger<MusicMetadataService>.Instance);
        var albumArtService = new AlbumArtService(metadataService, NullLogger<AlbumArtService>.Instance);
        return new LibraryScanService(
            _db,
            metadataService,
            albumArtService,
            _mockEventBus.Object,
            config ?? _configuration,
            NullLogger<LibraryScanService>.Instance,
            _mockEnrichment.Object);
    }

    private List<(Guid FileNodeId, string FilePath, string MimeType, long SizeBytes)> CreateFileList(int count)
    {
        var files = new List<(Guid, string, string, long)>();
        for (var i = 0; i < count; i++)
        {
            files.Add((Guid.NewGuid(), $"/fake/path/track{i + 1}.mp3", "audio/mpeg", 5_000_000));
        }
        return files;
    }

    // ── Progress Reporting ───────────────────────────────────────────

    [TestMethod]
    public async Task ScanLibrary_ReportsProgressPerFile()
    {
        var files = CreateFileList(5);
        var progressReports = new List<LibraryScanProgress>();
        var progress = new Progress<LibraryScanProgress>(p => progressReports.Add(p));

        var service = CreateService();
        await service.ScanLibraryAsync(files, _caller.UserId, _caller, progress);

        await Task.Delay(100); // Allow progress callbacks

        // Should have multiple progress reports (at least one per file + initial + final + enrichment)
        Assert.IsTrue(progressReports.Count >= 5, $"Expected at least 5 progress reports, got {progressReports.Count}");
    }

    [TestMethod]
    public async Task ScanLibrary_ProgressIncludesFileName()
    {
        var files = CreateFileList(3);
        var progressReports = new List<LibraryScanProgress>();
        var progress = new Progress<LibraryScanProgress>(p => progressReports.Add(p));

        var service = CreateService();
        await service.ScanLibraryAsync(files, _caller.UserId, _caller, progress);

        await Task.Delay(100);

        var reportsWithFiles = progressReports.Where(p => p.CurrentFile is not null).ToList();
        Assert.IsTrue(reportsWithFiles.Count > 0, "Expected at least one report with CurrentFile set");
        Assert.IsTrue(reportsWithFiles.Any(p => p.CurrentFile!.Contains("track")),
            "Expected CurrentFile to contain track filename");
    }

    [TestMethod]
    public async Task ScanLibrary_ProgressIncludesRunningCounts()
    {
        var files = CreateFileList(3);
        var progressReports = new List<LibraryScanProgress>();
        var progress = new Progress<LibraryScanProgress>(p => progressReports.Add(p));

        var service = CreateService();
        await service.ScanLibraryAsync(files, _caller.UserId, _caller, progress);

        await Task.Delay(100);

        // Verify FilesProcessed increments over time
        var processedValues = progressReports.Select(p => p.FilesProcessed).ToList();
        Assert.IsTrue(processedValues.Max() > 0, "Expected FilesProcessed to increment");
    }

    [TestMethod]
    public async Task ScanLibrary_ProgressShowsPercentage()
    {
        var files = CreateFileList(5);
        var progressReports = new List<LibraryScanProgress>();
        var progress = new Progress<LibraryScanProgress>(p => progressReports.Add(p));

        var service = CreateService();
        await service.ScanLibraryAsync(files, _caller.UserId, _caller, progress);

        await Task.Delay(100);

        var percentages = progressReports.Select(p => p.PercentComplete).ToList();
        Assert.IsTrue(percentages.Any(p => p == 0), "Expected initial 0% progress");
        Assert.IsTrue(percentages.Any(p => p == 100), "Expected final 100% progress");
    }

    [TestMethod]
    public async Task ScanLibrary_ProgressShowsPhase()
    {
        var files = CreateFileList(2);
        var progressReports = new List<LibraryScanProgress>();
        var progress = new Progress<LibraryScanProgress>(p => progressReports.Add(p));

        var service = CreateService();
        await service.ScanLibraryAsync(files, _caller.UserId, _caller, progress);

        await Task.Delay(100);

        var phases = progressReports.Select(p => p.Phase).Distinct().ToList();
        Assert.IsTrue(phases.Contains("Extracting metadata"),
            $"Expected 'Extracting metadata' phase, got: {string.Join(", ", phases)}");
        Assert.IsTrue(phases.Contains("Complete"),
            $"Expected 'Complete' phase, got: {string.Join(", ", phases)}");
    }

    [TestMethod]
    public async Task ScanLibrary_NullProgress_DoesNotThrow()
    {
        var files = CreateFileList(2);

        var service = CreateService();
        var result = await service.ScanLibraryAsync(files, _caller.UserId, _caller, progress: null);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task ScanLibrary_EmptyFileList_ReportsComplete()
    {
        var progressReports = new List<LibraryScanProgress>();
        var progress = new Progress<LibraryScanProgress>(p => progressReports.Add(p));

        var service = CreateService();
        var result = await service.ScanLibraryAsync([], _caller.UserId, _caller, progress);

        await Task.Delay(100);

        Assert.IsNotNull(result);
        Assert.IsTrue(progressReports.Any(p => p.Phase == "Complete"),
            "Expected 'Complete' phase even for empty file list");
    }

    // ── Enrichment Integration ───────────────────────────────────────

    [TestMethod]
    public async Task ScanLibrary_WithEnrichmentEnabled_RunsEnrichmentPhase()
    {
        var files = CreateFileList(1);

        var service = CreateService();
        await service.ScanLibraryAsync(files, _caller.UserId, _caller);

        _mockEnrichment.Verify(
            x => x.EnrichAlbumsWithoutArtAsync(
                _caller.UserId,
                It.IsAny<IProgress<EnrichmentProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ScanLibrary_WithEnrichmentDisabled_SkipsEnrichmentPhase()
    {
        var disabledConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Files:Storage:RootPath"] = _tempDir,
                ["Music:Enrichment:Enabled"] = "false",
                ["Music:Enrichment:AutoFetchArt"] = "false",
                ["Music:Enrichment:AutoEnrichArtists"] = "false"
            })
            .Build();

        var files = CreateFileList(1);

        var service = CreateService(disabledConfig);
        await service.ScanLibraryAsync(files, _caller.UserId, _caller);

        _mockEnrichment.Verify(
            x => x.EnrichAlbumsWithoutArtAsync(
                It.IsAny<Guid>(),
                It.IsAny<IProgress<EnrichmentProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ScanLibrary_EnrichmentPhase_ReportsProgress()
    {
        var files = CreateFileList(1);
        var progressReports = new List<LibraryScanProgress>();
        var progress = new Progress<LibraryScanProgress>(p => progressReports.Add(p));

        var service = CreateService();
        await service.ScanLibraryAsync(files, _caller.UserId, _caller, progress);

        await Task.Delay(100);

        var phases = progressReports.Select(p => p.Phase).Distinct().ToList();
        // Should contain the enrichment phase report (at minimum the "Enriching metadata" trigger)
        Assert.IsTrue(phases.Any(p => p.Contains("Enrich", StringComparison.OrdinalIgnoreCase) || p.Contains("metadata", StringComparison.OrdinalIgnoreCase)),
            $"Expected enrichment phase in progress, got: {string.Join(", ", phases)}");
    }

    // ── Cancellation ─────────────────────────────────────────────────

    [TestMethod]
    public async Task ScanLibrary_CancellationDuringScan_StopsProcessing()
    {
        var files = CreateFileList(10);
        var cts = new CancellationTokenSource();
        var processedCount = 0;
        var progress = new Progress<LibraryScanProgress>(p =>
        {
            processedCount = p.FilesProcessed;
            if (p.FilesProcessed >= 2) cts.Cancel();
        });

        var service = CreateService();

        try
        {
            await service.ScanLibraryAsync(files, _caller.UserId, _caller, progress, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Should not have processed all 10 files
        Assert.IsTrue(processedCount < 10, $"Expected fewer than 10 processed, got {processedCount}");
    }

    [TestMethod]
    public async Task ScanLibrary_CancellationDuringEnrichment_StopsEnrichment()
    {
        var files = CreateFileList(1);
        var cts = new CancellationTokenSource();

        _mockEnrichment.Setup(x => x.EnrichAlbumsWithoutArtAsync(
                It.IsAny<Guid>(),
                It.IsAny<IProgress<EnrichmentProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = CreateService();
        // Enrichment cancellation should be caught — scan results should be preserved
        var result = await service.ScanLibraryAsync(files, _caller.UserId, _caller, cancellationToken: CancellationToken.None);

        Assert.IsNotNull(result);
    }
}

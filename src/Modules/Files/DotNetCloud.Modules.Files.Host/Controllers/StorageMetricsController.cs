using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for storage usage and deduplication metrics.
/// </summary>
[Route("api/v1/files/storage")]
public class StorageMetricsController : FilesControllerBase
{
    private readonly IStorageMetricsService _metricsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageMetricsController"/> class.
    /// </summary>
    public StorageMetricsController(IStorageMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    /// <summary>
    /// Returns storage usage metrics including physical vs. logical size and deduplication savings.
    /// </summary>
    [HttpGet("metrics")]
    public Task<IActionResult> GetMetricsAsync() => ExecuteAsync(async () =>
    {
        var metrics = await _metricsService.GetDeduplicationMetricsAsync();
        return Ok(Envelope(metrics));
    });
}

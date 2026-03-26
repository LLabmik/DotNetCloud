using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for bulk file operations.
/// </summary>
[Route("api/v1/files/bulk")]
public class BulkController : FilesControllerBase
{
    private readonly IFileService _fileService;
    private readonly ITrashService _trashService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkController"/> class.
    /// </summary>
    public BulkController(IFileService fileService, ITrashService trashService)
    {
        _fileService = fileService;
        _trashService = trashService;
    }

    /// <summary>
    /// Moves multiple files/folders to a target parent.
    /// </summary>
    [HttpPost("move")]
    public Task<IActionResult> BulkMoveAsync([FromBody] BulkOperationDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var caller = ToCaller(userId);
        var results = new List<BulkItemResultDto>();

        foreach (var nodeId in dto.NodeIds)
        {
            try
            {
                await _fileService.MoveAsync(nodeId, new MoveNodeDto { TargetParentId = dto.TargetParentId }, caller);
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = true });
            }
            catch (Exception ex)
            {
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = false, Error = ex.Message });
            }
        }

        return Ok(Envelope(ToBulkResult(dto.NodeIds.Count, results)));
    });

    /// <summary>
    /// Copies multiple files/folders to a target parent.
    /// </summary>
    [HttpPost("copy")]
    public Task<IActionResult> BulkCopyAsync([FromBody] BulkOperationDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var caller = ToCaller(userId);
        var results = new List<BulkItemResultDto>();

        foreach (var nodeId in dto.NodeIds)
        {
            try
            {
                await _fileService.CopyAsync(nodeId, dto.TargetParentId, caller);
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = true });
            }
            catch (Exception ex)
            {
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = false, Error = ex.Message });
            }
        }

        return Ok(Envelope(ToBulkResult(dto.NodeIds.Count, results)));
    });

    /// <summary>
    /// Soft-deletes multiple files/folders.
    /// </summary>
    [HttpPost("delete")]
    public Task<IActionResult> BulkDeleteAsync([FromBody] BulkOperationDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var caller = ToCaller(userId);
        var results = new List<BulkItemResultDto>();

        foreach (var nodeId in dto.NodeIds)
        {
            try
            {
                await _fileService.DeleteAsync(nodeId, caller);
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = true });
            }
            catch (Exception ex)
            {
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = false, Error = ex.Message });
            }
        }

        return Ok(Envelope(ToBulkResult(dto.NodeIds.Count, results)));
    });

    /// <summary>
    /// Permanently deletes multiple trashed files/folders.
    /// </summary>
    [HttpPost("permanent-delete")]
    public Task<IActionResult> BulkPermanentDeleteAsync([FromBody] BulkOperationDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var caller = ToCaller(userId);
        var results = new List<BulkItemResultDto>();

        foreach (var nodeId in dto.NodeIds)
        {
            try
            {
                await _trashService.PermanentDeleteAsync(nodeId, caller);
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = true });
            }
            catch (Exception ex)
            {
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = false, Error = ex.Message });
            }
        }

        return Ok(Envelope(ToBulkResult(dto.NodeIds.Count, results)));
    });

    private static BulkResultDto ToBulkResult(int totalCount, List<BulkItemResultDto> results) => new()
    {
        TotalCount = totalCount,
        SuccessCount = results.Count(r => r.Success),
        FailureCount = results.Count(r => !r.Success),
        Results = results
    };
}

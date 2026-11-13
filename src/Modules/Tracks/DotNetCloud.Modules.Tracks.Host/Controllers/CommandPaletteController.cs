using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for the command palette's cross-entity search.
/// </summary>
/// <remarks>
/// The palette queries the module's own database, which the sandboxed UI cannot reach, so the search
/// runs here and the frame only renders the grouped result.
/// </remarks>
[Route("api/v1")]
public class CommandPaletteController : TracksControllerBase
{
    private readonly ICommandPaletteService _palette;
    private readonly ILogger<CommandPaletteController> _logger;

    /// <summary>Initializes a new instance of the <see cref="CommandPaletteController"/> class.</summary>
    /// <param name="palette">The palette search service.</param>
    /// <param name="logger">Logger.</param>
    public CommandPaletteController(ICommandPaletteService palette, ILogger<CommandPaletteController> logger)
    {
        _palette = palette;
        _logger = logger;
    }

    /// <summary>Searches products, work items, sprints and views across an organization.</summary>
    /// <param name="orgId">Organization to search in.</param>
    /// <param name="q">The search text.</param>
    /// <param name="productId">Optional product to prioritise in the results.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("organizations/{orgId:guid}/palette")]
    public async Task<IActionResult> SearchAsync(
        Guid orgId,
        [FromQuery] string? q,
        [FromQuery] Guid? productId = null,
        CancellationToken ct = default)
    {
        GetAuthenticatedCaller();

        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Envelope(new CommandPaletteResult()));
        }

        try
        {
            var result = await _palette.SearchAsync(orgId, q.Trim(), productId, ct);
            return Ok(Envelope(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command palette search failed for organization {OrgId}", orgId);
            return Ok(Envelope(new CommandPaletteResult()));
        }
    }
}

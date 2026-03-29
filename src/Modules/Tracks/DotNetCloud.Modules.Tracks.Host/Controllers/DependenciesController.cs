using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for card dependencies.
/// </summary>
[Route("api/v1/cards/{cardId:guid}/dependencies")]
public class DependenciesController : TracksControllerBase
{
    private readonly DependencyService _dependencyService;
    private readonly ILogger<DependenciesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DependenciesController"/> class.
    /// </summary>
    public DependenciesController(DependencyService dependencyService, ILogger<DependenciesController> logger)
    {
        _dependencyService = dependencyService;
        _logger = logger;
    }

    /// <summary>Lists dependencies for a card.</summary>
    [HttpGet]
    public async Task<IActionResult> ListDependenciesAsync(Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var dependencies = await _dependencyService.GetDependenciesAsync(cardId, caller);
            return Ok(Envelope(dependencies));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    /// <summary>Adds a dependency on another card.</summary>
    [HttpPost]
    public async Task<IActionResult> AddDependencyAsync(Guid cardId, [FromBody] AddDependencyRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var dependency = await _dependencyService.AddDependencyAsync(
                cardId, request.DependsOnCardId, request.Type, caller);
            return Created($"/api/v1/cards/{cardId}/dependencies", Envelope(dependency));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.CardNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.DependencyCycleDetected))
                return Conflict(ErrorEnvelope(ErrorCodes.DependencyCycleDetected, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Removes a dependency.</summary>
    [HttpDelete("{dependsOnCardId:guid}")]
    public async Task<IActionResult> RemoveDependencyAsync(Guid cardId, Guid dependsOnCardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _dependencyService.RemoveDependencyAsync(cardId, dependsOnCardId, caller);
            return Ok(Envelope(new { removed = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }
}

/// <summary>Request body for adding a dependency.</summary>
public sealed record AddDependencyRequest
{
    /// <summary>The card ID that this card depends on.</summary>
    public required Guid DependsOnCardId { get; init; }

    /// <summary>The dependency type.</summary>
    public required CardDependencyType Type { get; init; }
}

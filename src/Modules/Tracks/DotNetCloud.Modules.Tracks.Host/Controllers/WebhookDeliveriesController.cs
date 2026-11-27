using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for webhook delivery logs.
/// </summary>
[Route("api/v1/webhooks/{subscriptionId:guid}/deliveries")]
public class WebhookDeliveriesController : TracksControllerBase
{
    private readonly WebhookDeliveryService _deliveryService;
    private readonly ILogger<WebhookDeliveriesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookDeliveriesController"/> class.
    /// </summary>
    public WebhookDeliveriesController(WebhookDeliveryService deliveryService, ILogger<WebhookDeliveriesController> logger)
    {
        _deliveryService = deliveryService;
        _logger = logger;
    }

    /// <summary>Lists deliveries for a webhook subscription (paginated).</summary>
    [HttpGet]
    public async Task<IActionResult> GetDeliveriesAsync(
        Guid subscriptionId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        try
        {
            var deliveries = await _deliveryService.GetDeliveriesAsync(subscriptionId, skip, take, ct);
            return Ok(Envelope(deliveries, new { skip, take, total = deliveries.Count }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list deliveries for webhook {SubscriptionId}", subscriptionId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }
}

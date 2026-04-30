using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for webhook subscription management.
/// </summary>
[Route("api/v1/products/{productId:guid}/webhooks")]
public class WebhooksController : TracksControllerBase
{
    private readonly WebhookService _webhookService;
    private readonly WebhookDeliveryService _deliveryService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        WebhookService webhookService,
        WebhookDeliveryService deliveryService,
        ILogger<WebhooksController> logger)
    {
        _webhookService = webhookService;
        _deliveryService = deliveryService;
        _logger = logger;
    }

    /// <summary>Lists all webhook subscriptions for a product.</summary>
    [HttpGet]
    public async Task<IActionResult> ListSubscriptionsAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var subscriptions = await _webhookService.GetSubscriptionsAsync(productId, ct);
            return Ok(Envelope(subscriptions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list webhooks for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Creates a new webhook subscription.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSubscriptionAsync(Guid productId, [FromBody] CreateWebhookDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Url) || !Uri.TryCreate(dto.Url, UriKind.Absolute, out _))
                return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, "A valid URL is required."));

            if (dto.EventTypes is null || dto.EventTypes.Count == 0)
                return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, "At least one event type is required."));

            var subscription = await _webhookService.CreateSubscriptionAsync(
                productId, caller.UserId, dto.Url, dto.EventTypes, ct);

            return Created($"/api/v1/products/{productId}/webhooks/{subscription.Id}", Envelope(subscription));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create webhook for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Updates an existing webhook subscription.</summary>
    [HttpPut("{subscriptionId:guid}")]
    public async Task<IActionResult> UpdateSubscriptionAsync(Guid productId, Guid subscriptionId, [FromBody] UpdateWebhookDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var subscription = await _webhookService.UpdateSubscriptionAsync(
                subscriptionId, dto.Url, dto.EventTypes, dto.IsActive, ct);

            if (subscription is null)
                return NotFound(ErrorEnvelope(ErrorCodes.NotFound, "Webhook subscription not found."));

            return Ok(Envelope(subscription));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update webhook {SubscriptionId}", subscriptionId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Deletes a webhook subscription.</summary>
    [HttpDelete("{subscriptionId:guid}")]
    public async Task<IActionResult> DeleteSubscriptionAsync(Guid productId, Guid subscriptionId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var deleted = await _webhookService.DeleteSubscriptionAsync(subscriptionId, ct);
            if (!deleted)
                return NotFound(ErrorEnvelope(ErrorCodes.NotFound, "Webhook subscription not found."));

            return Ok(Envelope(new { deleted = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete webhook {SubscriptionId}", subscriptionId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Sends a test ping event to the specified webhook subscription.</summary>
    [HttpPost("{subscriptionId:guid}/test")]
    public async Task<IActionResult> TestSubscriptionAsync(Guid productId, Guid subscriptionId, CancellationToken ct)
    {
        try
        {
            var subscription = await _webhookService.GetSubscriptionAsync(subscriptionId, ct);
            if (subscription is null)
                return NotFound(ErrorEnvelope(ErrorCodes.NotFound, "Webhook subscription not found."));

            var pingPayload = new
            {
                EventType = "ping",
                Timestamp = DateTime.UtcNow,
                SubscriptionId = subscriptionId,
                ProductId = productId
            };

            var delivery = await _deliveryService.DeliverAsync(subscription, "ping", pingPayload, ct);

            return Ok(Envelope(new
            {
                success = delivery.ResponseStatusCode is >= 200 and < 300,
                statusCode = delivery.ResponseStatusCode,
                durationMs = delivery.DurationMs,
                error = delivery.ErrorMessage
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test webhook {SubscriptionId}", subscriptionId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }
}

/// <summary>
/// DTO for creating a webhook subscription.
/// </summary>
public sealed class CreateWebhookDto
{
    public required string Url { get; set; }
    public required List<string> EventTypes { get; set; }
}

/// <summary>
/// DTO for updating a webhook subscription.
/// </summary>
public sealed class UpdateWebhookDto
{
    public required string Url { get; set; }
    public required List<string> EventTypes { get; set; }
    public bool IsActive { get; set; } = true;
}

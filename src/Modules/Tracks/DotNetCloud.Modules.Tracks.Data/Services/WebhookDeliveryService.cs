using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Executes HTTP POST deliveries to webhook subscription URLs.
/// Handles HMAC signing, request dispatch, and delivery recording.
/// </summary>
public sealed class WebhookDeliveryService
{
    private readonly TracksDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookDeliveryService"/> class.
    /// </summary>
    public WebhookDeliveryService(
        TracksDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDeliveryService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Delivers an event payload to a webhook subscription and records the delivery.
    /// </summary>
    public async Task<WebhookDelivery> DeliverAsync(
        WebhookSubscription subscription,
        string eventType,
        object payload,
        CancellationToken ct)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var signature = WebhookService.ComputeSignature(payloadJson, subscription.Secret);

        var delivery = new WebhookDelivery
        {
            SubscriptionId = subscription.Id,
            EventType = eventType,
            PayloadJson = payloadJson,
            CreatedAt = DateTime.UtcNow
        };

        var sw = Stopwatch.StartNew();

        try
        {
            using var client = _httpClientFactory.CreateClient("Webhooks");
            client.Timeout = TimeSpan.FromSeconds(30);

            var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.Add("X-DotNetCloud-Event", eventType);
            content.Headers.Add("X-DotNetCloud-Delivery", delivery.Id.ToString());
            content.Headers.Add("X-DotNetCloud-Signature", $"sha256={signature}");

            var response = await client.PostAsync(subscription.Url, content, ct);
            sw.Stop();

            delivery.ResponseStatusCode = (int)response.StatusCode;
            delivery.DurationMs = sw.ElapsedMilliseconds;
            delivery.DeliveredAt = DateTime.UtcNow;

            if (!response.IsSuccessStatusCode)
            {
                delivery.ResponseBody = await response.Content.ReadAsStringAsync(ct);
                delivery.ErrorMessage = $"HTTP {(int)response.StatusCode}: {delivery.ResponseBody?.Truncate(500)}";
                _logger.LogWarning("Webhook delivery {DeliveryId} failed: HTTP {StatusCode} for {Url}",
                    delivery.Id, (int)response.StatusCode, subscription.Url);
            }
            else
            {
                _logger.LogDebug("Webhook delivery {DeliveryId} succeeded: HTTP {StatusCode} in {Duration}ms",
                    delivery.Id, (int)response.StatusCode, sw.ElapsedMilliseconds);
            }

            // Update subscription timestamps
            subscription.LastDeliveryAt = delivery.DeliveredAt;
            subscription.UpdatedAt = DateTime.UtcNow;

            _db.WebhookDeliveries.Add(delivery);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            sw.Stop();

            delivery.DurationMs = sw.ElapsedMilliseconds;
            delivery.ErrorMessage = ex.Message.Truncate(2000);
            delivery.ResponseStatusCode = null;

            subscription.FailedDeliveryCount++;
            subscription.UpdatedAt = DateTime.UtcNow;

            _db.WebhookDeliveries.Add(delivery);
            await _db.SaveChangesAsync(ct);

            _logger.LogError(ex, "Webhook delivery {DeliveryId} failed with exception for {Url}",
                delivery.Id, subscription.Url);
        }

        return delivery;
    }

    /// <summary>
    /// Gets paginated deliveries for a subscription.
    /// </summary>
    public async Task<List<WebhookDelivery>> GetDeliveriesAsync(Guid subscriptionId, int skip, int take, CancellationToken ct)
    {
        return await _db.WebhookDeliveries
            .AsNoTracking()
            .Where(d => d.SubscriptionId == subscriptionId)
            .OrderByDescending(d => d.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Gets pending failed deliveries that are due for retry.
    /// </summary>
    public async Task<List<WebhookDelivery>> GetPendingRetriesAsync(CancellationToken ct)
    {
        // Retry intervals: 1min, 5min, 15min, 1h, 6h, 24h, 24h (max 7 retries)
        var retryIntervalsMinutes = new[] { 1, 5, 15, 60, 360, 1440, 1440 };
        var now = DateTime.UtcNow;

        var allFailed = await _db.WebhookDeliveries
            .Include(d => d.Subscription)
            .Where(d => d.ResponseStatusCode != 200 && d.ResponseStatusCode != 201 && d.ResponseStatusCode != 204
                     && d.ErrorMessage != null
                     && d.RetryCount < 7)
            .ToListAsync(ct);

        return allFailed
            .Where(d =>
            {
                if (d.RetryCount >= retryIntervalsMinutes.Length)
                    return false;
                var nextRetryAt = d.CreatedAt.AddMinutes(retryIntervalsMinutes[d.RetryCount]);
                return now >= nextRetryAt;
            })
            .ToList();
    }

    /// <summary>
    /// Retries a previously failed delivery.
    /// </summary>
    public async Task<WebhookDelivery> RetryDeliveryAsync(
        WebhookDelivery delivery,
        WebhookSubscription subscription,
        CancellationToken ct)
    {
        // Re-serialize the original payload
        var signature = WebhookService.ComputeSignature(delivery.PayloadJson, subscription.Secret);

        delivery.RetryCount++;
        var sw = Stopwatch.StartNew();

        try
        {
            using var client = _httpClientFactory.CreateClient("Webhooks");
            client.Timeout = TimeSpan.FromSeconds(30);

            var content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.Add("X-DotNetCloud-Event", delivery.EventType);
            content.Headers.Add("X-DotNetCloud-Delivery", delivery.Id.ToString());
            content.Headers.Add("X-DotNetCloud-Signature", $"sha256={signature}");
            content.Headers.Add("X-DotNetCloud-Retry", delivery.RetryCount.ToString());

            var response = await client.PostAsync(subscription.Url, content, ct);
            sw.Stop();

            delivery.ResponseStatusCode = (int)response.StatusCode;
            delivery.DurationMs = sw.ElapsedMilliseconds;
            delivery.DeliveredAt = DateTime.UtcNow;
            delivery.ErrorMessage = response.IsSuccessStatusCode ? null : $"Retry {delivery.RetryCount}: HTTP {(int)response.StatusCode}";

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            delivery.ErrorMessage = $"Retry {delivery.RetryCount}: {ex.Message.Truncate(500)}";
            await _db.SaveChangesAsync(ct);
        }

        return delivery;
    }
}

/// <summary>
/// Extension methods for string truncation.
/// </summary>
internal static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Manages webhook subscriptions: CRUD, dispatch, and secret generation.
/// </summary>
public sealed class WebhookService
{
    private readonly TracksDbContext _db;
    private readonly ILogger<WebhookService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookService"/> class.
    /// </summary>
    public WebhookService(TracksDbContext db, ILogger<WebhookService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new webhook subscription.
    /// </summary>
    public async Task<WebhookSubscription> CreateSubscriptionAsync(
        Guid productId,
        Guid createdByUserId,
        string url,
        List<string> eventTypes,
        CancellationToken ct)
    {
        var subscription = new WebhookSubscription
        {
            ProductId = productId,
            Url = url,
            Secret = GenerateSecret(),
            EventsJson = JsonSerializer.Serialize(eventTypes, JsonOptions),
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.WebhookSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Webhook subscription {SubscriptionId} created for product {ProductId} — URL: {Url}",
            subscription.Id, productId, url);

        return subscription;
    }

    /// <summary>
    /// Lists all subscriptions for a product.
    /// </summary>
    public async Task<List<WebhookSubscription>> GetSubscriptionsAsync(Guid productId, CancellationToken ct)
    {
        return await _db.WebhookSubscriptions
            .AsNoTracking()
            .Where(w => w.ProductId == productId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Gets a single subscription by ID.
    /// </summary>
    public async Task<WebhookSubscription?> GetSubscriptionAsync(Guid id, CancellationToken ct)
    {
        return await _db.WebhookSubscriptions.FindAsync([id], ct);
    }

    /// <summary>
    /// Updates a webhook subscription.
    /// </summary>
    public async Task<WebhookSubscription?> UpdateSubscriptionAsync(
        Guid id,
        string url,
        List<string> eventTypes,
        bool isActive,
        CancellationToken ct)
    {
        var subscription = await _db.WebhookSubscriptions.FindAsync([id], ct);
        if (subscription is null) return null;

        subscription.Url = url;
        subscription.EventsJson = JsonSerializer.Serialize(eventTypes, JsonOptions);
        subscription.IsActive = isActive;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return subscription;
    }

    /// <summary>
    /// Deletes a webhook subscription and all its deliveries.
    /// </summary>
    public async Task<bool> DeleteSubscriptionAsync(Guid id, CancellationToken ct)
    {
        var subscription = await _db.WebhookSubscriptions.FindAsync([id], ct);
        if (subscription is null) return false;

        _db.WebhookSubscriptions.Remove(subscription);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Finds all active subscriptions that match the given event type.
    /// </summary>
    public async Task<List<WebhookSubscription>> GetMatchingSubscriptionsAsync(string eventType, CancellationToken ct)
    {
        var allActive = await _db.WebhookSubscriptions
            .AsNoTracking()
            .Where(w => w.IsActive)
            .ToListAsync(ct);

        return allActive
            .Where(w =>
            {
                try
                {
                    var events = JsonSerializer.Deserialize<List<string>>(w.EventsJson, JsonOptions);
                    return events is not null && events.Contains(eventType, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            })
            .ToList();
    }

    /// <summary>
    /// Generates a cryptographically secure random secret for HMAC signing.
    /// </summary>
    public static string GenerateSecret()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Computes the HMAC-SHA256 signature of a payload with a secret.
    /// </summary>
    public static string ComputeSignature(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexStringLower(hash);
    }
}

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A webhook subscription for a product — configures an HTTP callback URL that
/// receives event payloads for selected event types.
/// </summary>
public sealed class WebhookSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public required string Url { get; set; }
    /// <summary>HMAC secret used to sign outgoing webhook payloads.</summary>
    public required string Secret { get; set; }
    /// <summary>JSON array of event type strings this subscription listens for.</summary>
    public required string EventsJson { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public DateTime? LastDeliveryAt { get; set; }
    public int FailedDeliveryCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}

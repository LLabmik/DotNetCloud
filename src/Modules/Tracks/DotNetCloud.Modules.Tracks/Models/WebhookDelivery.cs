namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Records a single HTTP delivery attempt for a webhook subscription.
/// Tracks timing, response status, and any errors for audit/debugging.
/// </summary>
public sealed class WebhookDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public required string EventType { get; set; }
    /// <summary>JSON payload sent to the webhook URL.</summary>
    public required string PayloadJson { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public long DurationMs { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public WebhookSubscription? Subscription { get; set; }
}

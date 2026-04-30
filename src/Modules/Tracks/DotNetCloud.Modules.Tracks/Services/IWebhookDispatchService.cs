namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Dispatches domain events to configured webhook subscriptions.
/// Implemented in the Data layer to access scoped DbContext services.
/// </summary>
public interface IWebhookDispatchService
{
    /// <summary>
    /// Dispatches a domain event to all matching active webhook subscriptions.
    /// </summary>
    Task DispatchAsync(string eventType, object eventPayload, CancellationToken ct);
}

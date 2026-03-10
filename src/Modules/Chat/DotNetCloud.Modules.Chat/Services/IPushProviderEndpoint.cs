namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Provider-specific push endpoint contract used by the router to dispatch by provider.
/// </summary>
internal interface IPushProviderEndpoint : IPushNotificationService
{
    /// <summary>
    /// Gets the provider type served by this endpoint.
    /// </summary>
    PushProvider Provider { get; }
}

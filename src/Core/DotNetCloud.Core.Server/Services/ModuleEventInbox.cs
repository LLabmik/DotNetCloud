using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// The core's inbox for platform events raised by process-isolated modules.
/// </summary>
/// <remarks>
/// <para>
/// A module host runs its own in-process event bus that nothing else can see, so a platform event
/// published there only ever reached handlers inside that module process. This inbox is the core end
/// of the supported route: the module states what happened, the core publishes it on its own bus and
/// its notification pipeline (in-app record, real-time push, email/push channels) decides delivery.
/// </para>
/// <para>
/// The registry is a <b>whitelist</b>. Only types registered here can be published by a module; an
/// unregistered type is refused rather than deserialised, so a module cannot inject an arbitrary
/// event (or spoof a user-facing event that the core would act on) by naming it on the wire.
/// </para>
/// <para>
/// The caller context is always the authenticated module, never one supplied in the payload. Events
/// that carry a module id of their own are stamped with the verified id so a module cannot attribute
/// work to another module.
/// </para>
/// </remarks>
internal sealed class ModuleEventInbox
{
    /// <summary>
    /// Upper bound on one accepted payload. Platform events are small metadata records; the cap
    /// stops a module from using this rpc as a bulk data channel.
    /// </summary>
    internal const int MaxPayloadBytes = 64 * 1024;

    private readonly Dictionary<string, Func<string, CallerContext, string, CancellationToken, Task>> _routes
        = new(StringComparer.Ordinal);

    private readonly IEventBus _eventBus;
    private readonly ILogger<ModuleEventInbox> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleEventInbox"/> class and registers the
    /// platform event types a module may publish.
    /// </summary>
    /// <param name="eventBus">The core's in-process event bus.</param>
    /// <param name="logger">The logger for this inbox.</param>
    public ModuleEventInbox(IEventBus eventBus, ILogger<ModuleEventInbox> logger)
    {
        _eventBus = eventBus;
        _logger = logger;

        // Notification-producing platform events. Each entry opts a core-owned event type in; the
        // core's NotificationProducer already subscribes to both, so publishing here is all a
        // module needs to do for in-app + real-time + push delivery.
        Register<ReminderTriggeredEvent>(
            (evt, verifiedModuleId) => evt with { SourceModuleId = verifiedModuleId });

        Register<ShareExpiringEvent>();
    }

    /// <summary>
    /// Gets the event type names a module is allowed to publish.
    /// </summary>
    public IReadOnlyCollection<string> PublishableEventTypes => _routes.Keys;

    /// <summary>
    /// Publishes a module-supplied platform event on the core's event bus.
    /// </summary>
    /// <param name="eventType">The wire event type name.</param>
    /// <param name="payloadJson">The JSON payload.</param>
    /// <param name="caller">The authenticated caller context derived by the transport.</param>
    /// <param name="verifiedModuleId">The module id authenticated on the transport.</param>
    /// <param name="cancellationToken">A token to cancel the publish.</param>
    /// <returns><see langword="true"/> when the event was accepted; otherwise false with a reason.</returns>
    public async Task<(bool Success, string? Error)> TryPublishAsync(
        string eventType,
        string payloadJson,
        CallerContext caller,
        string verifiedModuleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (string.IsNullOrWhiteSpace(eventType) || !_routes.TryGetValue(eventType, out var route))
        {
            _logger.LogWarning(
                "PublishEvent: module {ModuleId} attempted to publish unregistered event type '{EventType}'",
                verifiedModuleId, eventType);
            return (false, $"Event type '{eventType}' is not module-publishable.");
        }

        // Only a module acting for itself may publish a platform event. A user-scoped assertion (or
        // any other caller type) must not be able to trigger platform notifications.
        if (caller.Type != CallerType.Module)
        {
            _logger.LogWarning(
                "PublishEvent: {EventType} from module {ModuleId} was attempted with caller type {CallerType}",
                eventType, verifiedModuleId, caller.Type);
            return (false, "Platform events may only be published by a module acting for itself.");
        }

        if (payloadJson.Length > MaxPayloadBytes)
        {
            _logger.LogWarning(
                "PublishEvent: payload for {EventType} from module {ModuleId} exceeds {MaxBytes} bytes",
                eventType, verifiedModuleId, MaxPayloadBytes);
            return (false, $"Payload exceeds {MaxPayloadBytes} bytes.");
        }

        try
        {
            await route(payloadJson, caller, verifiedModuleId, cancellationToken).ConfigureAwait(false);
            return (true, null);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "PublishEvent: malformed payload for {EventType} from module {ModuleId}",
                eventType, verifiedModuleId);
            return (false, "Payload is not a valid event of the declared type.");
        }
    }

    /// <summary>
    /// Registers a module-publishable event type.
    /// </summary>
    /// <typeparam name="TEvent">The platform event type.</typeparam>
    /// <param name="normalize">
    /// Optional rewrite applied to the deserialised event before publishing, used to replace
    /// caller-supplied attribution with the verified module identity.
    /// </param>
    private void Register<TEvent>(Func<TEvent, string, TEvent>? normalize = null)
        where TEvent : class, IEvent
    {
        _routes[PlatformEventContract.TypeName<TEvent>()] =
            async (payloadJson, caller, verifiedModuleId, cancellationToken) =>
            {
                var @event = JsonSerializer.Deserialize<TEvent>(
                    payloadJson, PlatformEventContract.JsonOptions)
                    ?? throw new JsonException($"Payload deserialised to null for {typeof(TEvent).Name}.");

                if (normalize is not null)
                    @event = normalize(@event, verifiedModuleId);

                await _eventBus.PublishAsync(@event, caller, cancellationToken).ConfigureAwait(false);
            };
    }
}

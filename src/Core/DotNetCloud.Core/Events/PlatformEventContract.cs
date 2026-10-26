using System.Text.Json;

namespace DotNetCloud.Core.Events;

/// <summary>
/// The wire contract shared by modules that publish platform events and the core that consumes them.
/// </summary>
/// <remarks>
/// Both ends must agree on the event type name and the JSON shape. They are defined once here instead
/// of as literals on each side, so a rename cannot silently break delivery by making the core reject
/// an event its own type system produced.
/// </remarks>
public static class PlatformEventContract
{
    /// <summary>
    /// Gets the serializer options used for both sides of the platform-event transport.
    /// </summary>
    /// <remarks>
    /// Property names are matched case-insensitively so a payload written by a client using the web
    /// defaults (camelCase) is still accepted. Nothing here relaxes the <c>required</c> member
    /// enforcement of the event records.
    /// </remarks>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the wire name of a platform event type.
    /// </summary>
    /// <typeparam name="TEvent">The platform event type.</typeparam>
    /// <returns>The simple CLR type name, which is the contract key.</returns>
    public static string TypeName<TEvent>() where TEvent : class, IEvent => typeof(TEvent).Name;

    /// <summary>
    /// Gets the wire name of a platform event type.
    /// </summary>
    /// <param name="eventType">The platform event type.</param>
    /// <returns>The simple CLR type name, which is the contract key.</returns>
    public static string TypeName(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return eventType.Name;
    }
}

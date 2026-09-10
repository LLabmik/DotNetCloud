using System.Text.Json.Serialization;

namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Canonical 4-state user presence used by presence dots and the real-time presence pipeline.
/// </summary>
/// <remarks>
/// Serialized as its name string (<c>"Online"</c>, <c>"Away"</c>, <c>"DoNotDisturb"</c>,
/// <c>"Offline"</c>) over JSON/SignalR so the same representation flows to Blazor (in-process),
/// native CoreHub clients, and <see cref="PresenceDto.Status"/>. This is the single source of
/// truth for the presence-dot palette:
/// <list type="bullet">
///   <item><description><b>Online</b> — connected and actively interacting (green).</description></item>
///   <item><description><b>Away</b> — connected but no real interaction for the idle threshold (yellow).</description></item>
///   <item><description><b>DoNotDisturb</b> — connected with do-not-disturb enabled (red).</description></item>
///   <item><description><b>Offline</b> — no active connection (gray).</description></item>
/// </list>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<PresenceState>))]
public enum PresenceState
{
    /// <summary>Connected and actively interacting within the idle window.</summary>
    Online,

    /// <summary>Connected, but no real interaction for at least the idle threshold.</summary>
    Away,

    /// <summary>Connected with the user's chat do-not-disturb toggle enabled.</summary>
    DoNotDisturb,

    /// <summary>No active connection (offline).</summary>
    Offline
}

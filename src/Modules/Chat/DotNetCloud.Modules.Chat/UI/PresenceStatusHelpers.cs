using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Shared 4-state presence display helpers used across chat UI presence indicators
/// (DM sidebar dots, DM thread header, member list rows).
/// </summary>
/// <remarks>
/// Status strings are the canonical presence names ("Online", "Away", "DoNotDisturb",
/// "Offline") produced by <see cref="ToStatusString(PresenceState)"/> and carried on
/// <c>PresenceDto.Status</c> / <c>ChannelViewModel.PresenceStatus</c> / member status.
/// Anything unrecognized (null, empty, whitespace, legacy values) renders as Offline.
/// </remarks>
public static class PresenceStatusHelpers
{
    /// <summary>
    /// Maps a presence status string to the CSS class used to color the presence dot.
    /// </summary>
    /// <param name="status">The canonical presence status string, or <c>null</c>.</param>
    /// <returns>A <c>presence-*</c> CSS class; unrecognized values map to <c>presence-offline</c>.</returns>
    public static string GetCssClass(string? status) => status switch
    {
        "Online" => "presence-online",
        "Away" => "presence-away",
        "DoNotDisturb" => "presence-dnd",
        "Offline" => "presence-offline",
        _ => "presence-offline"
    };

    /// <summary>
    /// Maps a presence status string to its friendly display label.
    /// </summary>
    /// <param name="status">The canonical presence status string, or <c>null</c>.</param>
    /// <returns>A human-readable label; unrecognized values map to <c>Offline</c>.</returns>
    public static string GetLabel(string? status) => status switch
    {
        "Online" => "Online",
        "Away" => "Idle",
        "DoNotDisturb" => "Do Not Disturb",
        "Offline" => "Offline",
        _ => "Offline"
    };

    /// <summary>
    /// Converts a <see cref="PresenceState"/> to its canonical wire status string.
    /// </summary>
    /// <param name="state">The presence state.</param>
    /// <returns>The enum <see cref="PresenceState"/> name, e.g. <c>"DoNotDisturb"</c>.</returns>
    public static string ToStatusString(PresenceState state) => state.ToString();
}

namespace DotNetCloud.Core.Modules.Ui;

/// <summary>
/// The <c>postMessage</c> contract between the core shell and a module UI running in its
/// sandboxed iframe.
/// </summary>
/// <remarks>
/// <para>
/// Every sandboxed frame without <c>allow-same-origin</c> reports <c>event.origin === "null"</c>,
/// so the origin header is worthless for authentication here. Both sides therefore authenticate
/// the peer by <b>identity</b>: the host requires <c>event.source === iframe.contentWindow</c> for a
/// specific frame, and the bridge requires <c>event.source === window.parent</c>. The message type
/// must additionally be in the expected direction's list — an unknown type is dropped silently.
/// </para>
/// <para>
/// This type is the single source of truth for the names. <c>dncModuleBridge.js</c> in
/// <c>DotNetCloud.Modules.Sdk</c> must declare the same strings; a test fails the build if they
/// drift.
/// </para>
/// </remarks>
public static class ModuleAppProtocol
{
    /// <summary>Host → frame: handshake carrying theme, locale, title and the frame's role.</summary>
    public const string Init = "dnc:init";

    /// <summary>Frame → host: the module UI is mounted and ready for the init payload.</summary>
    public const string Ready = "dnc:ready";

    /// <summary>Frame → host: request navigation to a module-relative path.</summary>
    public const string Navigate = "dnc:navigate";

    /// <summary>Frame → host: report the rendered document height in CSS pixels.</summary>
    public const string Resize = "dnc:resize";

    /// <summary>Frame → host: request a document title change.</summary>
    public const string Title = "dnc:title";

    /// <summary>Frame → host: open an <c>https:</c> URL in a new tab.</summary>
    public const string OpenExternal = "dnc:openExternal";

    /// <summary>Frame → host: ask the core to render a consent prompt for a scope.</summary>
    public const string RequestConsent = "dnc:requestConsent";

    /// <summary>Frame → host: show a short toast.</summary>
    public const string Notify = "dnc:notify";

    /// <summary>Host → frame: the theme or locale changed.</summary>
    public const string Theme = "dnc:theme";

    /// <summary>Host → frame: the user granted (or declined) a scope.</summary>
    public const string ConsentResult = "dnc:consentResult";

    /// <summary>Message types a frame is allowed to send to the host.</summary>
    public static IReadOnlyList<string> FromFrame { get; } =
    [
        Ready,
        Navigate,
        Resize,
        Title,
        OpenExternal,
        RequestConsent,
        Notify,
    ];

    /// <summary>Message types the host is allowed to send to a frame.</summary>
    public static IReadOnlyList<string> ToFrame { get; } =
    [
        Init,
        Theme,
        ConsentResult,
    ];

    /// <summary>Maximum accepted size of a single serialized message, in bytes.</summary>
    public const int MaxMessageBytes = 8 * 1024;

    /// <summary>Maximum accepted document height report, in CSS pixels.</summary>
    public const int MaxFrameHeight = 20_000;

    /// <summary>Minimum accepted document height report, in CSS pixels.</summary>
    public const int MinFrameHeight = 200;

    /// <summary>Maximum accepted title length.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Maximum accepted toast body length.</summary>
    public const int MaxNotificationLength = 500;

    /// <summary>How many <c>dnc:notify</c> messages a frame may send per minute.</summary>
    public const int MaxNotificationsPerMinute = 5;

    /// <summary>Determines whether a frame-originated message type is allowed.</summary>
    /// <param name="type">The message type from the frame payload.</param>
    /// <returns><see langword="true"/> when the host may act on it.</returns>
    public static bool IsFromFrameMessage(string? type) =>
        !string.IsNullOrEmpty(type) && FromFrame.Contains(type, StringComparer.Ordinal);

    /// <summary>Determines whether a host-originated message type is allowed.</summary>
    /// <param name="type">The message type.</param>
    /// <returns><see langword="true"/> when a frame should accept it.</returns>
    public static bool IsToFrameMessage(string? type) =>
        !string.IsNullOrEmpty(type) && ToFrame.Contains(type, StringComparer.Ordinal);

    /// <summary>
    /// Normalizes a frame-reported height into the accepted range.
    /// </summary>
    /// <param name="height">The reported height.</param>
    /// <returns>A height clamped to <see cref="MinFrameHeight"/>..<see cref="MaxFrameHeight"/>.</returns>
    public static int ClampHeight(double height)
    {
        if (double.IsNaN(height) || double.IsInfinity(height))
        {
            return MinFrameHeight;
        }

        return (int)Math.Clamp(height, MinFrameHeight, MaxFrameHeight);
    }
}

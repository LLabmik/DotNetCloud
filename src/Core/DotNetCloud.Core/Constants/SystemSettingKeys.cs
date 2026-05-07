namespace DotNetCloud.Core.Constants;

/// <summary>
/// Contains well-known system setting keys used across the platform.
/// Each setting is stored as a key-value pair scoped by module (see the SystemSetting entity
/// in the Data project for the store implementation).
/// </summary>
public static class SystemSettingKeys
{
    /// <summary>
    /// The module identifier for core DotNetCloud platform settings.
    /// </summary>
    public const string CoreModule = "dotnetcloud.core";

    // ──────────────────────────────────────────────
    //  Closed System Mode Settings
    // ──────────────────────────────────────────────

    /// <summary>
    /// Setting key for closed system mode. When <c>"true"</c>, self-registration
    /// is disabled and only administrators can create accounts.
    /// </summary>
    /// <remarks>
    /// <b>Module:</b> <see cref="CoreModule"/><br/>
    /// <b>Type:</b> <see cref="bool"/> serialized as <c>"true"</c> or <c>"false"</c><br/>
    /// <b>Default:</b> <c>"false"</c> (open registration allowed)<br/>
    /// <b>Effect:</b> When enabled, <c>/auth/register</c> displays a disabled message,
    /// and admin-created users are forced to change their password on first login.
    /// </remarks>
    public const string ClosedSystemEnabled = "ClosedSystemEnabled";

    /// <summary>
    /// Default value for <see cref="ClosedSystemEnabled"/> when the setting is not present.
    /// </summary>
    public const string ClosedSystemEnabledDefault = "false";
}

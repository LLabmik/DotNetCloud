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

    // ──────────────────────────────────────────────
    //  Demo Mode Settings
    // ──────────────────────────────────────────────

    /// <summary>
    /// Setting key for demo/trial mode. When <c>"true"</c>, self-registered accounts
    /// are created as trial accounts with 750 MB storage, no email sending, and
    /// auto-deletion after 5 days. Admin-created accounts are exempt.
    /// </summary>
    /// <remarks>
    /// <b>Module:</b> <see cref="CoreModule"/><br/>
    /// <b>Type:</b> <see cref="bool"/> serialized as <c>"true"</c> or <c>"false"</c><br/>
    /// <b>Default:</b> <c>"false"</c> (demo mode disabled)<br/>
    /// <b>Mutual exclusion:</b> Cannot be enabled simultaneously with
    /// <see cref="ClosedSystemEnabled"/>.
    /// </remarks>
    public const string DemoModeEnabled = "DemoModeEnabled";

    /// <summary>
    /// Default value for <see cref="DemoModeEnabled"/> when the setting is not present.
    /// </summary>
    public const string DemoModeEnabledDefault = "false";

    // ──────────────────────────────────────────────
    //  Admin MFA Settings
    // ──────────────────────────────────────────────

    /// <summary>
    /// Setting key for requiring MFA on admin accounts. When <c>"true"</c>, all users
    /// with the Administrator role are required to set up multi-factor authentication
    /// (TOTP) before they can access the system. Users who haven't set up MFA yet
    /// are redirected to <c>/auth/mfa-setup</c> after login.
    /// </summary>
    /// <remarks>
    /// <b>Module:</b> <see cref="CoreModule"/><br/>
    /// <b>Type:</b> <see cref="bool"/> serialized as <c>"true"</c> or <c>"false"</c><br/>
    /// <b>Default:</b> <c>"false"</c> (MFA not required for admins)<br/>
    /// <b>Effect:</b> When enabled, existing and future admin users are prompted to
    /// set up TOTP on next login. Set during initial <c>dotnetcloud setup</c> when the
    /// user answers Yes to the TOTP MFA prompt.
    /// </remarks>
    public const string AdminMfaRequired = "AdminMfaRequired";

    /// <summary>
    /// Default value for <see cref="AdminMfaRequired"/> when the setting is not present.
    /// </summary>
    public const string AdminMfaRequiredDefault = "false";
}

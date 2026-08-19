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

    // ──────────────────────────────────────────────
    //  Retention / Disposal Settings (SOC 2 C2 / P6)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Setting key for the audit log retention window in days. Rows older than this
    /// window are purged daily by <c>AuditLogPurgeHostedService</c>.
    /// </summary>
    /// <remarks>
    /// <b>Module:</b> <see cref="CoreModule"/><br/>
    /// <b>Type:</b> <c>int</c> serialized as a decimal string<br/>
    /// <b>Default:</b> <c>"365"</c><br/>
    /// <b>Effect:</b> Controls how long the persisted audit trail (SOC 2 CC4) is kept.
    /// </remarks>
    public const string AuditLogRetentionDays = "AuditLogRetentionDays";

    /// <summary>
    /// Default value for <see cref="AuditLogRetentionDays"/>.
    /// </summary>
    public const string AuditLogRetentionDaysDefault = "365";

    /// <summary>
    /// Setting key for the soft-deleted user-data retention window in days. Soft-deleted
    /// records (trash) older than this window are candidates for permanent purge.
    /// </summary>
    /// <remarks>
    /// <b>Module:</b> <see cref="CoreModule"/><br/>
    /// <b>Type:</b> <c>int</c> serialized as a decimal string<br/>
    /// <b>Default:</b> <c>"30"</c><br/>
    /// <b>Effect:</b> Controls how long soft-deleted user data is kept before permanent
    /// disposal (SOC 2 C2 / P6).
    /// </remarks>
    public const string TrashRetentionDays = "TrashRetentionDays";

    /// <summary>
    /// Default value for <see cref="TrashRetentionDays"/>.
    /// </summary>
    public const string TrashRetentionDaysDefault = "30";

    // ──────────────────────────────────────────────
    //  OpenIddict Key Rotation (SOC 2 CC6 / C1)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Setting key for the OpenIddict key-rotation pending-restart flag. When present,
    /// keys were rotated after the last server start and a restart is required to activate
    /// them. The value stores the rotation timestamp (<c>DateTime.UtcNow</c>, ISO 8601).
    /// Cleared automatically on the next server start.
    /// </summary>
    /// <remarks>
    /// <b>Module:</b> <see cref="CoreModule"/><br/>
    /// <b>Type:</b> <c>string</c> ISO-8601 UTC timestamp<br/>
    /// <b>Effect:</b> The admin UI shows a "restart to activate keys" banner while set.
    /// </remarks>
    public const string OidcKeysPendingRestart = "OidcKeysPendingRestart";
}

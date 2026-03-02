namespace DotNetCloud.Core.Data.Entities.Modules;

/// <summary>
/// Represents a device registered by a user for accessing the DotNetCloud platform.
/// Tracks device information, push notification tokens, and last activity.
/// </summary>
/// <remarks>
/// <para><strong>Purpose:</strong></para>
/// <list type="bullet">
///   <item><description>Track which devices a user has registered (desktop, mobile, tablet, etc.)</description></item>
///   <item><description>Store push notification tokens for mobile/desktop notifications</description></item>
///   <item><description>Monitor device activity for security and presence tracking</description></item>
///   <item><description>Enable device-specific settings and configurations</description></item>
///   <item><description>Support device management features (revoke, rename, etc.)</description></item>
/// </list>
/// <para><strong>Usage Patterns:</strong></para>
/// <list type="number">
///   <item><description><strong>Desktop Client Registration:</strong> When sync client starts, registers device with name like "Windows Laptop"</description></item>
///   <item><description><strong>Mobile App Registration:</strong> Mobile app registers with push token for notifications</description></item>
///   <item><description><strong>Activity Tracking:</strong> LastSeenAt updated periodically to show online/offline status</description></item>
///   <item><description><strong>Security Monitoring:</strong> List all devices for user review, revoke suspicious devices</description></item>
/// </list>
/// <para><strong>Example Scenarios:</strong></para>
/// <code>
/// // Desktop sync client registration
/// var device = new UserDevice
/// {
///     UserId = currentUser.Id,
///     Name = "Windows Desktop - Office",
///     DeviceType = "Desktop",
///     LastSeenAt = DateTime.UtcNow
/// };
/// 
/// // Mobile app with push notifications
/// var mobileDevice = new UserDevice
/// {
///     UserId = currentUser.Id,
///     Name = "Android Phone - Pixel 7",
///     DeviceType = "Mobile",
///     PushToken = "fcm_token_abc123xyz789",
///     LastSeenAt = DateTime.UtcNow
/// };
/// </code>
/// </remarks>
public class UserDevice
{
    /// <summary>
    /// Unique identifier for this device registration.
    /// </summary>
    /// <remarks>
    /// Primary key. Auto-generated on insert.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    /// The user who owns this device.
    /// </summary>
    /// <remarks>
    /// Foreign key to ApplicationUser. Required field.
    /// Cascade delete: When user is deleted, all their devices are removed.
    /// </remarks>
    public Guid UserId { get; set; }

    /// <summary>
    /// User-friendly name for this device (e.g., "Windows Laptop", "Android Phone - Pixel 7").
    /// </summary>
    /// <remarks>
    /// <para>Maximum length: 200 characters.</para>
    /// <para>Examples:</para>
    /// <list type="bullet">
    ///   <item><description>"Windows Desktop - Office"</description></item>
    ///   <item><description>"MacBook Pro - Home"</description></item>
    ///   <item><description>"Android Phone - Pixel 7"</description></item>
    ///   <item><description>"iPad - Personal"</description></item>
    /// </list>
    /// <para>Users can rename devices for easier identification.</para>
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of device (Desktop, Mobile, Tablet, Web, etc.).
    /// </summary>
    /// <remarks>
    /// <para>Maximum length: 50 characters.</para>
    /// <para>Supported values:</para>
    /// <list type="bullet">
    ///   <item><description><strong>Desktop:</strong> Windows, macOS, Linux desktop clients</description></item>
    ///   <item><description><strong>Mobile:</strong> Android, iOS mobile apps</description></item>
    ///   <item><description><strong>Tablet:</strong> iPad, Android tablet apps</description></item>
    ///   <item><description><strong>Web:</strong> Browser-based access (logged for auditing)</description></item>
    ///   <item><description><strong>CLI:</strong> Command-line tool access</description></item>
    /// </list>
    /// <para>Used to determine appropriate UI/UX and notification delivery methods.</para>
    /// </remarks>
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>
    /// Push notification token for this device (FCM for Android, APNs for iOS, or UnifiedPush endpoint).
    /// Null if device doesn't support push notifications.
    /// </summary>
    /// <remarks>
    /// <para>Maximum length: 500 characters.</para>
    /// <para>Token Types:</para>
    /// <list type="bullet">
    ///   <item><description><strong>FCM (Firebase Cloud Messaging):</strong> For Android devices</description></item>
    ///   <item><description><strong>APNs (Apple Push Notification service):</strong> For iOS/macOS devices</description></item>
    ///   <item><description><strong>UnifiedPush:</strong> Self-hosted push notification endpoint URL</description></item>
    ///   <item><description><strong>Web Push:</strong> Browser push notification subscription</description></item>
    /// </list>
    /// <para>Token is encrypted at rest for security. Null for desktop clients without push support.</para>
    /// <para><strong>Security Note:</strong> Push tokens should be refreshed periodically and invalidated when device is revoked.</para>
    /// </remarks>
    public string? PushToken { get; set; }

    /// <summary>
    /// Timestamp of the last time this device communicated with the server.
    /// Used for presence tracking (online/offline status) and stale device cleanup.
    /// </summary>
    /// <remarks>
    /// <para>Updated by:</para>
    /// <list type="bullet">
    ///   <item><description>Sync client heartbeat (every 5 minutes)</description></item>
    ///   <item><description>API requests from this device</description></item>
    ///   <item><description>Push notification acknowledgments</description></item>
    ///   <item><description>WebSocket/SignalR connection activity</description></item>
    /// </list>
    /// <para>Used to determine:</para>
    /// <list type="bullet">
    ///   <item><description>Online/offline status (last seen &lt; 10 minutes = online)</description></item>
    ///   <item><description>Stale device cleanup (last seen &gt; 90 days = prompt user to remove)</description></item>
    ///   <item><description>Security auditing (detect compromised accounts)</description></item>
    /// </list>
    /// </remarks>
    public DateTime LastSeenAt { get; set; }

    /// <summary>
    /// Timestamp when this device was first registered.
    /// </summary>
    /// <remarks>
    /// Auto-set on insert. Immutable.
    /// </remarks>
    public DateTime CreatedAt { get; set; }

    // ==================== Navigation Properties ====================

    /// <summary>
    /// Navigation property to the user who owns this device.
    /// </summary>
    /// <remarks>
    /// EF Core relationship: Many UserDevices -> One ApplicationUser.
    /// Cascade delete enabled.
    /// </remarks>
    public virtual ApplicationUser User { get; set; } = null!;
}

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Checks and requests the Android media-library read permissions required to scan the
/// shared photo/video gallery for auto-upload. On Android 13+ (API 33) this is
/// <c>READ_MEDIA_IMAGES</c> + <c>READ_MEDIA_VIDEO</c>; on older versions it is
/// <c>READ_EXTERNAL_STORAGE</c>. The permissions are declared in AndroidManifest.xml but,
/// like all dangerous permissions on modern Android, must also be granted at runtime
/// before <c>ContentResolver</c> queries against MediaStore can see other apps' media.
/// </summary>
public interface IMediaPermissionService
{
    /// <summary>Returns <c>true</c> when the app may read the shared photo/video library on this OS.</summary>
    bool HasMediaReadPermission();

    /// <summary>
    /// Requests the media-library read permissions at runtime (shows the system dialog on
    /// first request). Returns <c>true</c> once granted; returns immediately when the
    /// permission is already held or not required on this OS.
    /// </summary>
    Task<bool> EnsureGrantedAsync(CancellationToken ct = default);

    /// <summary>
    /// Opens the app's system permissions page where the user can grant media access if
    /// they previously denied the runtime prompt.
    /// </summary>
    void OpenMediaPermissionSettings();
}

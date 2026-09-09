namespace DotNetCloud.Client.Android.Platforms.Android;

/// <summary>
/// Custom MAUI permission representing read access to the shared Android photo + video
/// library. Resolves to <c>READ_MEDIA_IMAGES</c> + <c>READ_MEDIA_VIDEO</c> on Android 13+
/// (API 33) and <c>READ_EXTERNAL_STORAGE</c> on older versions. MAUI does not ship a
/// built-in for the Android 13 media permissions, so auto-upload requests this type via
/// <see cref="Permissions"/> to show the system grant dialog.
/// </summary>
public sealed class MediaLibraryReadPermission : Permissions.BasePlatformPermission
{
    /// <inheritdoc />
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        OperatingSystem.IsAndroidVersionAtLeast(33)
            ? new (string, bool)[]
            {
                (global::Android.Manifest.Permission.ReadMediaImages, true),
                (global::Android.Manifest.Permission.ReadMediaVideo, true)
            }
            : new (string, bool)[] { (global::Android.Manifest.Permission.ReadExternalStorage, true) };
}

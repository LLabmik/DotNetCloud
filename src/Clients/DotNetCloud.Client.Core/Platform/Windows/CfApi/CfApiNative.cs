// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace DotNetCloud.Client.Core.Platform.Windows.CfApi;

/// <summary>
/// P/Invoke declarations for the Windows Cloud Filter API (cfapi.dll).
/// These are the same APIs used by OneDrive and other Windows cloud sync providers.
/// </summary>
internal static partial class CfApiNative
{
    private const string CfApiDll = "cfapi.dll";

    /// <summary>
    /// Registers a sync root with the Windows Cloud Filter platform.
    /// This enables shell integration (icon overlays, context menus, status column).
    /// </summary>
    /// <param name="syncRootPath">Absolute path of the sync root directory.</param>
    /// <param name="registration">Sync root registration info.</param>
    /// <param name="policies">Sync root policies (hydration, population, etc.).</param>
    /// <param name="registerFlags">Registration flags.</param>
    /// <returns>HRESULT indicating success or failure.</returns>
    [LibraryImport(CfApiDll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CfRegisterSyncRoot(
        string syncRootPath,
        in CF_SYNC_REGISTRATION registration,
        in CF_SYNC_POLICIES policies,
        uint registerFlags);

    /// <summary>
    /// Unregisters a previously registered sync root.
    /// </summary>
    /// <param name="syncRootPath">Absolute path of the sync root directory.</param>
    /// <param name="registration">Sync root registration info used during registration.</param>
    /// <param name="flags">Unregistration flags.</param>
    /// <returns>HRESULT indicating success or failure.</returns>
    [LibraryImport(CfApiDll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CfUnregisterSyncRoot(
        string syncRootPath,
        in CF_SYNC_REGISTRATION registration,
        uint flags);

    /// <summary>
    /// Creates one or more placeholder files or directories in a sync root.
    /// </summary>
    /// <param name="syncRootPath">Absolute path of the sync root.</param>
    /// <param name="placeholderArray">Array of placeholder creation infos.</param>
    /// <param name="placeholderCount">Number of placeholders to create.</param>
    /// <param name="flags">Creation flags.</param>
    /// <param name="entriesProcessed">[out] Number of entries actually processed.</param>
    /// <returns>HRESULT indicating success or failure.</returns>
    [LibraryImport(CfApiDll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CfCreatePlaceholders(
        string syncRootPath,
        [In, Out] CF_PLACEHOLDER_CREATE_INFO[] placeholderArray,
        uint placeholderCount,
        CF_PLACEHOLDER_CREATE_FLAGS flags,
        out uint entriesProcessed);

    /// <summary>
    /// Updates metadata of an existing placeholder file.
    /// </summary>
    /// <param name="placeholderPath">Absolute path to the placeholder.</param>
    /// <param name="fileIdentity">Optional new file identity.</param>
    /// <param name="fileIdentitySize">Size of file identity.</param>
    /// <param name="fileMetadata">Optional new file metadata.</param>
    /// <param name="updateFlags">Update flags.</param>
    /// <param name="updateResult">[out] Result details.</param>
    /// <returns>HRESULT indicating success or failure.</returns>
    [LibraryImport(CfApiDll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CfUpdatePlaceholder(
        string placeholderPath,
        IntPtr fileIdentity,
        uint fileIdentitySize,
        in CF_FS_METADATA fileMetadata,
        uint updateFlags,
        out uint updateResult);

    /// <summary>
    /// Performs an operation on a placeholder file (transfer data, dehydrate, ack, etc.).
    /// </summary>
    /// <param name="opParams">Operation parameters.</param>
    /// <param name="opParamsSize">Size of the operation parameters structure.</param>
    /// <returns>HRESULT indicating success or failure.</returns>
    [LibraryImport(CfApiDll, SetLastError = true)]
    public static partial int CfExecute(
        IntPtr opParams,
        uint opParamsSize);

    /// <summary>
    /// Sets the pin state of a placeholder file.
    /// </summary>
    /// <param name="filePath">Absolute path to the file.</param>
    /// <param name="pinState">Desired pin state.</param>
    /// <param name="setPinFlags">Set pin flags.</param>
    /// <returns>HRESULT indicating success or failure.</returns>
    [LibraryImport(CfApiDll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CfSetPinState(
        string filePath,
        CF_PIN_STATE pinState,
        uint setPinFlags);

    /// <summary>
    /// Retrieves placeholder information for a file.
    /// </summary>
    /// <param name="filePath">Absolute path to the file.</param>
    /// <param name="infoClass">Type of info to retrieve.</param>
    /// <param name="infoBuffer">Buffer to receive the info.</param>
    /// <param name="infoBufferSize">Size of the buffer.</param>
    /// <param name="returnedInfoSize">[out] Size of info actually returned.</param>
    /// <returns>HRESULT indicating success or failure.</returns>
    [LibraryImport(CfApiDll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CfGetPlaceholderInfo(
        string filePath,
        uint infoClass,
        IntPtr infoBuffer,
        uint infoBufferSize,
        out uint returnedInfoSize);

    /// <summary>
    /// Connects a sync provider to a sync root for receiving callbacks.
    /// </summary>
    /// <param name="syncRootPath">Absolute path of the sync root.</param>
    /// <param name="callbackRegistration">Array of callback registrations (null-terminated).</param>
    /// <param name="callbackTable">Registered callback table.</param>
    /// <param name="connectFlags">Connection flags.</param>
    /// <param name="connectionKey">[out] Connection key for future operations.</param>
    /// <returns>HRESULT indicating success or failure.</returns>
    [LibraryImport(CfApiDll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CfConnectSyncRoot(
        string syncRootPath,
        [In] CF_CALLBACK_REGISTRATION[] callbackRegistration,
        IntPtr callbackTable,
        CF_CONNECT_FLAGS connectFlags,
        out ulong connectionKey);

    /// <summary>
    /// Disconnects a sync provider from a sync root.
    /// </summary>
    /// <param name="connectionKey">Connection key from CfConnectSyncRoot.</param>
    /// <param name="disconnectFlags">Disconnect flags.</param>
    /// <returns>HRESULT indicating success or failure.</returns>
    [LibraryImport(CfApiDll, SetLastError = true)]
    public static partial int CfDisconnectSyncRoot(
        ulong connectionKey,
        uint disconnectFlags);

    /// <summary>
    /// Reports a file that has changed to the sync root provider.
    /// </summary>
    /// <param name="syncRootPath">Absolute path of the sync root.</param>
    /// <param name="relativePath">Relative path of the changed file.</param>
    /// <param name="flags">Report flags.</param>
    /// <returns>HRESULT indicating success or failure.</returns>
    [LibraryImport(CfApiDll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CfReportProviderState(
        string syncRootPath,
        string relativePath,
        uint flags);

    /// <summary>
    /// Reports a sync status to Windows Shell for the sync root.
    /// </summary>
    /// <param name="syncRootPath">Absolute path of the sync root.</param>
    /// <param name="syncStatus">Sync status flags.</param>
    /// <returns>HRESULT indicating success or failure.</returns>
    [LibraryImport(CfApiDll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CfReportSyncStatus(
        string syncRootPath,
        uint syncStatus);
}

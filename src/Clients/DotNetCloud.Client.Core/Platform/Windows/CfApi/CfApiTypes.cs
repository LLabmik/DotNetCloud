// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace DotNetCloud.Client.Core.Platform.Windows.CfApi;

#pragma warning disable SA1310 // Field names may contain underscores (Win32 API conventions)

/// <summary>Sync root registration flags.</summary>
[Flags]
public enum CF_CONNECT_FLAGS : uint
{
    /// <summary>No flags.</summary>
    NONE = 0,

    /// <summary>The caller provides process information for hydration requests.</summary>
    REQUIRE_PROCESS_INFO = 0x00000002,

    /// <summary>The caller provides the full file path for hydration requests.</summary>
    REQUIRE_FULL_FILE_PATH = 0x00000004,

    /// <summary>Block hydration requests from the sync provider's own process.</summary>
    BLOCK_SELF = 0x00000008,
}

/// <summary>Placeholder creation flags.</summary>
[Flags]
public enum CF_PLACEHOLDER_CREATE_FLAGS : uint
{
    /// <summary>No flags.</summary>
    NONE = 0,

    /// <summary>Disable on-demand population of the sync root.</summary>
    DISABLE_ON_DEMAND_POPULATION = 0x00000001,

    /// <summary>Mark the placeholder as in-sync with the cloud.</summary>
    MARK_IN_SYNC = 0x00000002,
}

/// <summary>Hydration policy for a sync root.</summary>
public enum CF_HYDRATION_POLICY : uint
{
    /// <summary>Always hydrate the full file on first access.</summary>
    FULL = 2,

    /// <summary>Allow progressive/partial hydration for streaming scenarios.</summary>
    PROGRESSIVE = 3,
}

/// <summary>Pin state for a placeholder file.</summary>
public enum CF_PIN_STATE : uint
{
    /// <summary>Pin state is not specified.</summary>
    UNSPECIFIED = 0,

    /// <summary>File is pinned — always keep local content.</summary>
    PINNED = 1,

    /// <summary>File is unpinned — may be dehydrated.</summary>
    UNPINNED = 2,

    /// <summary>File is excluded from the sync root.</summary>
    EXCLUDED = 3,

    /// <summary>Inherit pin state from parent.</summary>
    INHERIT = 4,
}

/// <summary>Callback types for Cloud Filter API.</summary>
public enum CF_CALLBACK_TYPE : uint
{
    /// <summary>Fetch data for a placeholder (download content).</summary>
    FETCH_DATA = 0,

    /// <summary>Validate placeholder data integrity.</summary>
    VALIDATE_DATA = 1,

    /// <summary>Fetch directory placeholder enumeration.</summary>
    FETCH_PLACEHOLDERS = 2,

    /// <summary>Cancel an in-progress data fetch.</summary>
    CANCEL_FETCH_DATA = 3,

    /// <summary>Notification: file open completed.</summary>
    NOTIFY_FILE_OPEN_COMPLETION = 4,

    /// <summary>Notification: file close completed.</summary>
    NOTIFY_FILE_CLOSE_COMPLETION = 5,

    /// <summary>Notification: file is about to be dehydrated.</summary>
    NOTIFY_DEHYDRATE = 6,

    /// <summary>Notification: file dehydration completed.</summary>
    NOTIFY_DEHYDRATE_COMPLETION = 7,

    /// <summary>Notification: file is about to be deleted.</summary>
    NOTIFY_DELETE = 8,

    /// <summary>Notification: file deletion completed.</summary>
    NOTIFY_DELETE_COMPLETION = 9,

    /// <summary>Notification: file is about to be renamed.</summary>
    NOTIFY_RENAME = 10,

    /// <summary>Notification: file rename completed.</summary>
    NOTIFY_RENAME_COMPLETION = 11,
}

/// <summary>Operation types for CfExecute.</summary>
public enum CF_OPERATION_TYPE : uint
{
    /// <summary>Transfer data to a placeholder (hydration).</summary>
    TRANSFER_DATA = 0,

    /// <summary>Retrieve data from a placeholder (read-back).</summary>
    RETRIEVE_DATA = 1,

    /// <summary>Acknowledge that data was received.</summary>
    ACK_DATA = 2,

    /// <summary>Restart a failed hydration operation.</summary>
    RESTART_HYDRATION = 3,

    /// <summary>Remove content and leave a placeholder (dehydration).</summary>
    DEHYDRATE = 5,
}

/// <summary>HRESULT values used by Cloud Filter API.</summary>
public static class CfHResult
{
    /// <summary>Success.</summary>
    public const int S_OK = 0;

    /// <summary>The cloud file provider is not running.</summary>
    public const int HR_CLOUD_FILE_NOT_CONNECTED = unchecked((int)0x8000FFFF);

    /// <summary>The cloud file is not available.</summary>
    public const int HR_CLOUD_FILE_NOT_AVAILABLE = unchecked((int)0x8000FFFE);

    /// <summary>The cloud file provider is not supported.</summary>
    public const int HR_CLOUD_FILE_NOT_SUPPORTED = unchecked((int)0x8000FFFD);

    /// <summary>The operation is pending.</summary>
    public const int HR_CLOUD_FILE_PENDING = unchecked((int)0x8000FFFC);

    /// <summary>The request was timed out.</summary>
    public const int HR_CLOUD_FILE_REQUEST_TIMEOUT = unchecked((int)0x8000FFFB);

    /// <summary>The cloud file is already connected.</summary>
    public const int HR_CLOUD_FILE_ALREADY_CONNECTED = unchecked((int)0x8000FFFA);

    /// <summary>Invalid request for the current cloud file state.</summary>
    public const int HR_CLOUD_FILE_NOT_IN_SYNC = unchecked((int)0x8000FFF9);

    /// <summary>The cloud file provider cannot perform the operation on virtual file root.</summary>
    public const int HR_CLOUD_FILE_DEHYDRATION_DISALLOWED = unchecked((int)0x8000FFF8);

    /// <summary>The cloud file provider is throttling the request.</summary>
    public const int HR_CLOUD_FILE_THROTTLED = unchecked((int)0x8000FFF7);

    /// <summary>The operation was incompatible with the cloud file.</summary>
    public const int HR_CLOUD_FILE_INCOMPATIBLE = unchecked((int)0x8000FFF6);

    /// <summary>The cloud file provider's sync root meta data is invalid.</summary>
    public const int HR_CLOUD_FILE_PROTECTED = unchecked((int)0x8000FFF5);

    /// <summary>The cloud file provider's sync root is not found.</summary>
    public const int HR_CLOUD_FILE_SYNC_ROOT_NOT_FOUND = unchecked((int)0x8000FFF4);
}

/// <summary>Represents information about a sync root being registered.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct CF_SYNC_ROOT_BASIC_INFO
{
    /// <summary>Size of this structure.</summary>
    public uint StructSize;

    /// <summary>The sync root ID (a GUID string).</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 39)]
    public string SyncRootId;

    /// <summary>The sync root path.</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string SyncRootPath;
}

/// <summary>Registration information for a sync root.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct CF_SYNC_REGISTRATION
{
    /// <summary>Structure size.</summary>
    public uint StructSize;

    /// <summary>Provider name.</summary>
    public IntPtr ProviderName;

    /// <summary>Provider version.</summary>
    public IntPtr ProviderVersion;

    /// <summary>Sync root identity.</summary>
    public IntPtr SyncRootIdentity;

    /// <summary>Sync root identity size.</summary>
    public uint SyncRootIdentitySize;

    /// <summary>File identity.</summary>
    public IntPtr FileIdentity;

    /// <summary>File identity size.</summary>
    public uint FileIdentitySize;

    /// <summary>Provider ID.</summary>
    public Guid ProviderId;
}

/// <summary>Policy for a sync root.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_SYNC_POLICIES
{
    /// <summary>Structure size.</summary>
    public uint StructSize;

    /// <summary>Hydration policy modifier.</summary>
    public uint HydrationPolicyModifier;

    /// <summary>Hydration policy.</summary>
    public CF_HYDRATION_POLICY HydrationPolicy;

    /// <summary>Population policy modifier.</summary>
    public uint PopulationPolicyModifier;

    /// <summary>Population policy.</summary>
    public uint PopulationPolicy;

    /// <summary>In-sync policy modifier.</summary>
    public uint InSyncPolicyModifier;

    /// <summary>In-sync policy.</summary>
    public uint InSyncPolicy;

    /// <summary>Hard link policy.</summary>
    public uint HardLinkPolicy;
}

/// <summary>Callback registration table entry.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_CALLBACK_REGISTRATION
{
    /// <summary>Callback type.</summary>
    public CF_CALLBACK_TYPE Type;

    /// <summary>Callback function pointer.</summary>
    public IntPtr Callback;
}

/// <summary>Sync root connection info.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_SYNC_ROOT_PROVIDER_INFO
{
    /// <summary>Size of the structure.</summary>
    public uint StructSize;

    /// <summary>Provider status.</summary>
    public uint ProviderStatus;

    /// <summary>Provider name.</summary>
    public IntPtr ProviderName;

    /// <summary>Provider version.</summary>
    public IntPtr ProviderVersion;

    /// <summary>Sync root identity.</summary>
    public IntPtr SyncRootIdentity;

    /// <summary>Sync root identity size.</summary>
    public uint SyncRootIdentitySize;
}

/// <summary>Information needed to create a placeholder file or directory.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct CF_PLACEHOLDER_CREATE_INFO
{
    /// <summary>Relative path of the placeholder.</summary>
    public IntPtr RelativePath;

    /// <summary>Creation flags.</summary>
    public CF_PLACEHOLDER_CREATE_FLAGS Flags;

    /// <summary>File identity.</summary>
    public IntPtr FileIdentity;

    /// <summary>File identity size.</summary>
    public uint FileIdentitySize;

    /// <summary>Platform-specific file information.</summary>
    public CF_FS_METADATA FsMetadata;

    /// <summary>Result of the create operation (HRESULT).</summary>
    public int CreateResult;
}

/// <summary>File system metadata for a placeholder.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_FS_METADATA
{
    /// <summary>File size in bytes. 0 for directories.</summary>
    public long FileSize;

    /// <summary>Creation time (FILETIME).</summary>
    public long CreationTime;

    /// <summary>Last access time (FILETIME).</summary>
    public long LastAccessTime;

    /// <summary>Last write time (FILETIME).</summary>
    public long LastWriteTime;

    /// <summary>Change time (FILETIME).</summary>
    public long ChangeTime;

    /// <summary>File attributes.</summary>
    public uint FileAttributes;
}

/// <summary>Parameters for CfExecute TRANSFER_DATA operation.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_OPERATION_PARAMETERS
{
    /// <summary>Structure size.</summary>
    public uint StructSize;

    /// <summary>Operation type.</summary>
    public CF_OPERATION_TYPE OperationType;

    /// <summary>Union of operation-specific parameters.</summary>
    public CF_OPERATION_PARAM_UNION Parameters;
}

/// <summary>Union of operation-specific parameters for CfExecute.</summary>
[StructLayout(LayoutKind.Explicit)]
public struct CF_OPERATION_PARAM_UNION
{
    /// <summary>Transfer data parameters.</summary>
    [FieldOffset(0)]
    public CF_OPERATION_PARAM_TRANSFER_DATA TransferData;

    /// <summary>Acknowledge data parameters.</summary>
    [FieldOffset(0)]
    public CF_OPERATION_PARAM_ACK_DATA AckData;

    /// <summary>Dehydrate parameters.</summary>
    [FieldOffset(0)]
    public CF_OPERATION_PARAM_DEHYDRATE Dehydrate;
}

/// <summary>Transfer data operation parameters.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_OPERATION_PARAM_TRANSFER_DATA
{
    /// <summary>Completion status (HRESULT).</summary>
    public int CompletionStatus;

    /// <summary>Offset in the file.</summary>
    public long Offset;

    /// <summary>Length of data.</summary>
    public long Length;

    /// <summary>Pointer to the data buffer.</summary>
    public IntPtr Buffer;
}

/// <summary>Acknowledge data operation parameters.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_OPERATION_PARAM_ACK_DATA
{
    /// <summary>Completion status (HRESULT).</summary>
    public int CompletionStatus;

    /// <summary>Offset in the file.</summary>
    public long Offset;

    /// <summary>Length of data.</summary>
    public long Length;

    /// <summary>Flags.</summary>
    public uint Flags;
}

/// <summary>Dehydrate operation parameters.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_OPERATION_PARAM_DEHYDRATE
{
    /// <summary>Completion status (HRESULT).</summary>
    public int CompletionStatus;

    /// <summary>Flags.</summary>
    public uint Flags;
}

/// <summary>Information about a callback transfer data request.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_CALLBACK_INFO
{
    /// <summary>Structure size.</summary>
    public uint StructSize;

    /// <summary>Sync root connection that issued the callback.</summary>
    public IntPtr ConnectionKey;

    /// <summary>Sync root path.</summary>
    public IntPtr SyncRootPath;

    /// <summary>File path relative to sync root.</summary>
    public IntPtr RelativePath;

    /// <summary>Process ID requesting the data.</summary>
    public uint ProcessId;

    /// <summary>File identity.</summary>
    public IntPtr FileIdentity;

    /// <summary>File identity size.</summary>
    public uint FileIdentitySize;

    /// <summary>File key for CfExecute operations.</summary>
    public IntPtr FileKey;

    /// <summary>Required offset for FETCH_DATA.</summary>
    public long RequiredOffset;

    /// <summary>Required length for FETCH_DATA.</summary>
    public long RequiredLength;

    /// <summary>Optional process info.</summary>
    public IntPtr ProcessInfo;

    /// <summary>Optional file path.</summary>
    public IntPtr FilePath;
}

/// <summary>Information about placeholder state.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_PLACEHOLDER_STANDARD_INFO
{
    /// <summary>Structure size.</summary>
    public uint StructSize;

    /// <summary>Sync root identity.</summary>
    public IntPtr SyncRootIdentity;

    /// <summary>Sync root identity size.</summary>
    public uint SyncRootIdentitySize;

    /// <summary>File identity.</summary>
    public IntPtr FileIdentity;

    /// <summary>File identity size.</summary>
    public uint FileIdentitySize;

    /// <summary>Placeholder state flags.</summary>
    public uint PlaceholderState;

    /// <summary>Pin state.</summary>
    public CF_PIN_STATE PinState;

    /// <summary>File size.</summary>
    public long FileSize;

    /// <summary>Creation time.</summary>
    public long CreationTime;

    /// <summary>Last access time.</summary>
    public long LastAccessTime;

    /// <summary>Last write time.</summary>
    public long LastWriteTime;

    /// <summary>Change time.</summary>
    public long ChangeTime;

    /// <summary>File attributes.</summary>
    public uint FileAttributes;
}

/// <summary>Cloud Filter API callback delegate for FETCH_DATA.</summary>
/// <param name="callbackInfo">Callback request info.</param>
/// <param name="callbackData">Callback data (stream, etc.).</param>
/// <returns>HRESULT indicating success or failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int CF_FETCH_DATA_CALLBACK(
    ref CF_CALLBACK_INFO callbackInfo,
    ref CF_CALLBACK_PARAMETERS callbackData);

/// <summary>Cloud Filter API callback delegate for validations.</summary>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int CF_VALIDATE_DATA_CALLBACK(
    ref CF_CALLBACK_INFO callbackInfo,
    ref CF_CALLBACK_PARAMETERS callbackData);

/// <summary>Cloud Filter API callback delegate for placeholder fetching.</summary>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int CF_FETCH_PLACEHOLDERS_CALLBACK(
    ref CF_CALLBACK_INFO callbackInfo,
    ref CF_CALLBACK_PARAMETERS callbackData);

/// <summary>Cloud Filter API callback delegate for cancel fetch.</summary>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int CF_CANCEL_FETCH_DATA_CALLBACK(
    ref CF_CALLBACK_INFO callbackInfo,
    ref CF_CALLBACK_PARAMETERS callbackData);

/// <summary>Cloud Filter API notification callback delegate.</summary>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int CF_NOTIFICATION_CALLBACK(
    ref CF_CALLBACK_INFO callbackInfo,
    ref CF_CALLBACK_PARAMETERS callbackData);

/// <summary>Callback parameters structure.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_CALLBACK_PARAMETERS
{
    /// <summary>Structure size.</summary>
    public uint StructSize;

    /// <summary>Callback type that triggered this call.</summary>
    public CF_CALLBACK_TYPE CallbackType;

    /// <summary>Union of callback-specific parameters.</summary>
    public CF_CALLBACK_PARAM_UNION Parameters;
}

/// <summary>Union of callback-specific parameters.</summary>
[StructLayout(LayoutKind.Explicit)]
public struct CF_CALLBACK_PARAM_UNION
{
    /// <summary>FETCH_DATA parameters.</summary>
    [FieldOffset(0)]
    public CF_CALLBACK_PARAM_FETCH_DATA FetchData;

    /// <summary>FETCH_PLACEHOLDERS parameters.</summary>
    [FieldOffset(0)]
    public CF_CALLBACK_PARAM_FETCH_PLACEHOLDERS FetchPlaceholders;

    /// <summary>Notification parameters.</summary>
    [FieldOffset(0)]
    public CF_CALLBACK_PARAM_NOTIFY Notify;
}

/// <summary>FETCH_DATA callback parameters.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_CALLBACK_PARAM_FETCH_DATA
{
    /// <summary>Sync root identity of the file being fetched.</summary>
    public IntPtr SyncRootIdentity;

    /// <summary>Sync root identity size.</summary>
    public uint SyncRootIdentitySize;

    /// <summary>File identity of the file being fetched.</summary>
    public IntPtr FileIdentity;

    /// <summary>File identity size.</summary>
    public uint FileIdentitySize;

    /// <summary>Optional request key for tracking.</summary>
    public IntPtr RequestKey;

    /// <summary>Completion callback.</summary>
    public IntPtr CompletionCallback;

    /// <summary>Optional completion context.</summary>
    public IntPtr CompletionContext;
}

/// <summary>FETCH_PLACEHOLDERS callback parameters.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_CALLBACK_PARAM_FETCH_PLACEHOLDERS
{
    /// <summary>Pattern for placeholder enumeration.</summary>
    public IntPtr Pattern;

    /// <summary>Pattern length.</summary>
    public uint PatternLength;

    /// <summary>Completion callback.</summary>
    public IntPtr CompletionCallback;

    /// <summary>Completion context.</summary>
    public IntPtr CompletionContext;
}

/// <summary>Notification callback parameters.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CF_CALLBACK_PARAM_NOTIFY
{
    /// <summary>Notification type.</summary>
    public uint NotifyType;

    /// <summary>File path for the notification.</summary>
    public IntPtr FilePath;

    /// <summary>Old file path for rename notifications.</summary>
    public IntPtr OldFilePath;

    /// <summary>Notification-specific data.</summary>
    public IntPtr NotifyData;
}

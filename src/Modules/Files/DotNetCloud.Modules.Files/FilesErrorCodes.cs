namespace DotNetCloud.Modules.Files;

/// <summary>
/// Error codes specific to the Files module.
/// </summary>
public static class FilesErrorCodes
{
    /// <summary>File or folder not found.</summary>
    public const string NodeNotFound = "FILES_NODE_NOT_FOUND";

    /// <summary>Duplicate name in the same parent folder.</summary>
    public const string DuplicateName = "FILES_DUPLICATE_NAME";

    /// <summary>Maximum folder depth exceeded.</summary>
    public const string MaxDepthExceeded = "FILES_MAX_DEPTH_EXCEEDED";

    /// <summary>Cannot move a folder into itself or a descendant.</summary>
    public const string CircularMove = "FILES_CIRCULAR_MOVE";

    /// <summary>Insufficient storage quota.</summary>
    public const string InsufficientQuota = "FILES_INSUFFICIENT_QUOTA";

    /// <summary>Upload session not found or expired.</summary>
    public const string SessionNotFound = "FILES_SESSION_NOT_FOUND";

    /// <summary>Upload session already completed or cancelled.</summary>
    public const string SessionNotActive = "FILES_SESSION_NOT_ACTIVE";

    /// <summary>Chunk hash mismatch during upload.</summary>
    public const string ChunkHashMismatch = "FILES_CHUNK_HASH_MISMATCH";

    /// <summary>Chunk not found in the manifest.</summary>
    public const string ChunkNotInManifest = "FILES_CHUNK_NOT_IN_MANIFEST";

    /// <summary>File version not found.</summary>
    public const string VersionNotFound = "FILES_VERSION_NOT_FOUND";

    /// <summary>Share not found.</summary>
    public const string ShareNotFound = "FILES_SHARE_NOT_FOUND";

    /// <summary>Share link expired.</summary>
    public const string ShareExpired = "FILES_SHARE_EXPIRED";

    /// <summary>Share download limit reached.</summary>
    public const string ShareDownloadLimitReached = "FILES_SHARE_DOWNLOAD_LIMIT";

    /// <summary>Invalid share password.</summary>
    public const string InvalidSharePassword = "FILES_INVALID_SHARE_PASSWORD";

    /// <summary>Tag not found.</summary>
    public const string TagNotFound = "FILES_TAG_NOT_FOUND";

    /// <summary>Duplicate tag on the same node.</summary>
    public const string DuplicateTag = "FILES_DUPLICATE_TAG";

    /// <summary>Comment not found.</summary>
    public const string CommentNotFound = "FILES_COMMENT_NOT_FOUND";

    /// <summary>Cannot perform operation on a folder (file expected).</summary>
    public const string NotAFile = "FILES_NOT_A_FILE";

    /// <summary>Cannot perform operation on a file (folder expected).</summary>
    public const string NotAFolder = "FILES_NOT_A_FOLDER";

    /// <summary>Cannot restore to original parent (deleted or missing).</summary>
    public const string RestoreParentMissing = "FILES_RESTORE_PARENT_MISSING";
}

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Tracks active Collabora document editing sessions to enforce concurrent session limits.
/// </summary>
/// <remarks>
/// A "session" begins when Collabora first calls CheckFileInfo for a file (indicating it is opening
/// the document) and ends when the editor explicitly closes or the session expires due to inactivity.
/// </remarks>
public interface IWopiSessionTracker
{
    /// <summary>
    /// Attempts to register a new editing session for the given file and user.
    /// Returns <c>false</c> if the maximum concurrent session limit would be exceeded.
    /// If the user already has an active session for this file, the session is refreshed
    /// and <c>true</c> is returned without consuming an additional slot.
    /// </summary>
    /// <param name="fileId">The file being opened for editing.</param>
    /// <param name="userId">The user opening the file.</param>
    /// <returns><c>true</c> if the session was registered; <c>false</c> if at capacity.</returns>
    bool TryBeginSession(Guid fileId, Guid userId);

    /// <summary>
    /// Updates the last-activity timestamp for an existing session, preventing expiry.
    /// If no session exists, this is a no-op.
    /// </summary>
    /// <param name="fileId">The file being edited.</param>
    /// <param name="userId">The user editing the file.</param>
    void HeartbeatSession(Guid fileId, Guid userId);

    /// <summary>
    /// Explicitly ends an editing session for the given file and user.
    /// </summary>
    /// <param name="fileId">The file being closed.</param>
    /// <param name="userId">The user closing the file.</param>
    void EndSession(Guid fileId, Guid userId);

    /// <summary>
    /// Returns the current count of active (non-expired) sessions.
    /// </summary>
    int GetActiveSessionCount();
}

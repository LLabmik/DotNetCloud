namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// Thrown when the server returns HTTP 409 with error code <c>NAME_CONFLICT</c>, indicating
/// that a file or folder name conflicts with a case-variant of an existing sibling on the server.
/// This exception must not be retried; the operation should be moved to the failed queue.
/// </summary>
public sealed class NameConflictException : Exception
{
    /// <inheritdoc/>
    public NameConflictException(string message) : base(message) { }
}

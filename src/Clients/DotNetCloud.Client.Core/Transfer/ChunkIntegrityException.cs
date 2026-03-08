namespace DotNetCloud.Client.Core.Transfer;

/// <summary>
/// Thrown when a downloaded chunk's SHA-256 hash does not match the expected hash after all retry attempts.
/// </summary>
public sealed class ChunkIntegrityException : Exception
{
    /// <inheritdoc/>
    public ChunkIntegrityException(string message) : base(message) { }
}

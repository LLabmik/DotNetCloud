namespace DotNetCloud.Client.Core.Platform;

/// <summary>
/// Linux/macOS fallback implementation of <see cref="ILockedFileReader"/> that always
/// returns <see langword="null"/> (no VSS available on these platforms).
/// Tiers 1 and 2 (shared-read open and retry backoff) already handle the common
/// advisory-lock patterns on POSIX systems; Tier 3 (Volume Shadow Copy) is Windows-only.
/// </summary>
public sealed class NoOpLockedFileReader : ILockedFileReader
{
    /// <inheritdoc/>
    public Task<Stream?> TryReadLockedFileAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream?>(null);

    /// <inheritdoc/>
    public void ReleaseSnapshot() { }

    /// <inheritdoc/>
    public void Dispose() { }
}

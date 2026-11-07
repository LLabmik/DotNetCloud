namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Selects the namespace a storage operation addresses.
/// </summary>
public enum StorageScope
{
    /// <summary>
    /// Data owned by the module itself, shared by every user of the module.
    /// </summary>
    Module,

    /// <summary>
    /// Data owned by the acting user. Requires a verified user assertion on the call; the core
    /// derives the user from it and rejects calls without one.
    /// </summary>
    User,
}

/// <summary>
/// Describes one object returned by <see cref="IStorageProvider.ListAsync"/>.
/// </summary>
public sealed record StorageObjectInfo
{
    /// <summary>Gets the object path relative to the container.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the object size in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Gets when the object was last written (UTC).</summary>
    public required DateTimeOffset LastModifiedUtc { get; init; }
}

/// <summary>
/// Provides module-scoped object storage.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Namespace invariant (F5).</strong> Paths are always relative to the caller's namespace.
/// The core composes the full location from the <em>verified</em> module identity and, for
/// <see cref="StorageScope.User"/>, the <em>verified</em> acting user — a module never passes an
/// absolute path, a module id or a user id. Addressing another module's or user's object is
/// therefore not expressible, and traversal segments (<c>..</c>), rooted paths and empty segments
/// are rejected before any filesystem or storage call.
/// </para>
/// <para>
/// Callers must treat <c>container</c> as a fixed, lower-case name (<c>[a-z0-9][a-z0-9._-]*</c>,
/// at most 64 characters) and object paths as forward-slash separated, at most 1024 characters.
/// Control characters, backslashes and drive qualifiers are rejected.
/// </para>
/// <para>
/// The capability is tier <c>Restricted</c>: it must be granted explicitly, and a
/// <see cref="StorageScope.User"/> call fails closed when the call carries no verified user
/// assertion.
/// </para>
/// </remarks>
public interface IStorageProvider : ICapabilityInterface
{
    /// <summary>Tests whether an object exists in the caller's namespace.</summary>
    /// <param name="scope">Module-owned or user-owned data.</param>
    /// <param name="container">The container name.</param>
    /// <param name="path">The object path relative to the container.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the object exists and is readable by the caller.</returns>
    Task<bool> ExistsAsync(
        StorageScope scope,
        string container,
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>Opens an object for reading.</summary>
    /// <param name="scope">Module-owned or user-owned data.</param>
    /// <param name="container">The container name.</param>
    /// <param name="path">The object path relative to the container.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The object content, or <see langword="null"/> when it does not exist.</returns>
    Task<Stream?> OpenReadAsync(
        StorageScope scope,
        string container,
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces an object.</summary>
    /// <param name="scope">Module-owned or user-owned data.</param>
    /// <param name="container">The container name.</param>
    /// <param name="path">The object path relative to the container.</param>
    /// <param name="content">The content to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of bytes written.</returns>
    Task<long> WriteAsync(
        StorageScope scope,
        string container,
        string path,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes an object. Deleting a missing object succeeds.</summary>
    /// <param name="scope">Module-owned or user-owned data.</param>
    /// <param name="container">The container name.</param>
    /// <param name="path">The object path relative to the container.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(
        StorageScope scope,
        string container,
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>Lists objects under a prefix in the caller's namespace.</summary>
    /// <param name="scope">Module-owned or user-owned data.</param>
    /// <param name="container">The container name.</param>
    /// <param name="prefix">An optional path prefix; empty lists the whole container.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The objects found, ordered by path.</returns>
    Task<IReadOnlyList<StorageObjectInfo>> ListAsync(
        StorageScope scope,
        string container,
        string prefix = "",
        CancellationToken cancellationToken = default);
}

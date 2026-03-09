namespace DotNetCloud.Client.Core.SyncIgnore;

/// <summary>
/// Parses <c>.syncignore</c> files and evaluates whether a relative file path
/// should be excluded from synchronisation.  Supports a subset of
/// <c>.gitignore</c> pattern syntax: <c>*</c>, <c>?</c>, <c>**</c>,
/// <c>!</c> (negation), trailing <c>/</c> (directory marker) and <c>#</c>
/// (comments).
/// </summary>
public interface ISyncIgnoreParser
{
    /// <summary>
    /// Loads user-defined patterns from the <c>.syncignore</c> file in
    /// <paramref name="syncRoot"/>.  If the file does not exist only the
    /// built-in defaults are active.  May be called multiple times to reload
    /// after the file changes.
    /// </summary>
    /// <param name="syncRoot">Absolute path of the local sync folder.</param>
    void Initialize(string syncRoot);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="relativePath"/>
    /// matches an active ignore pattern (and is not un-ignored by a negation
    /// rule).
    /// </summary>
    /// <param name="relativePath">
    /// Path relative to the sync root, using either slash direction.
    /// </param>
    bool IsIgnored(string relativePath);

    /// <summary>Built-in default ignore patterns (read-only; never editable).</summary>
    IReadOnlyList<string> BuiltInPatterns { get; }

    /// <summary>User-defined patterns loaded from <c>.syncignore</c>.</summary>
    IReadOnlyList<string> UserPatterns { get; }

    /// <summary>
    /// Replaces the in-memory user pattern list and rebuilds the matchers.
    /// Call <see cref="SaveAsync"/> afterwards to persist the change.
    /// </summary>
    /// <param name="patterns">New set of user patterns.</param>
    void SetUserPatterns(IReadOnlyList<string> patterns);

    /// <summary>
    /// Writes the current <see cref="UserPatterns"/> to <c>.syncignore</c>
    /// in <paramref name="syncRoot"/>.
    /// </summary>
    /// <param name="syncRoot">Absolute path of the local sync folder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(string syncRoot, CancellationToken cancellationToken = default);
}

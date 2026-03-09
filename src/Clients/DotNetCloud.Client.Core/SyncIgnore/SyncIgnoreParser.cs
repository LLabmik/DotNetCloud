using System.Text;
using System.Text.RegularExpressions;

namespace DotNetCloud.Client.Core.SyncIgnore;

/// <summary>
/// <c>.gitignore</c>-compatible ignore pattern parser backed by
/// <see cref="Microsoft.Extensions.FileSystemGlobbing"/>.
/// </summary>
/// <remarks>
/// <para>
/// Built-in default patterns are always active.  A <c>.syncignore</c> file in
/// the sync root adds user-defined rules that can override the defaults via
/// negation (<c>!</c> prefix).
/// </para>
/// <para>
/// The <c>.syncignore</c> file itself IS synced across clients so rules are
/// shared automatically.
/// </para>
/// </remarks>
public sealed class SyncIgnoreParser : ISyncIgnoreParser
{
    // ── Built-in defaults ─────────────────────────────────────────────────

    private static readonly string[] BuiltInDefaults =
    [
        // OS-generated junk
        ".DS_Store",
        "Thumbs.db",
        "desktop.ini",
        "*.swp",
        "*~",
        // Temporary files
        "*.tmp",
        "*.temp",
        "~$*",
        // Version control metadata
        ".git/",
        ".svn/",
        ".hg/",
        // Package manager caches (re-downloadable)
        "node_modules/",
        ".npm/",
        ".yarn/",
        ".pnp.*",
        "packages/",
        ".nuget/",
        // Linux / KDE desktop metadata
        ".directory",
        // macOS extended attributes and special folders
        ".Spotlight-V100/",
        ".Trashes/",
        "._*",
    ];

    // ── State ─────────────────────────────────────────────────────────────

    private readonly List<string> _userPatterns = [];
    private List<Regex> _ignoreRegexes;
    private List<Regex> _unignoreRegexes;

    /// <summary>Initializes a new <see cref="SyncIgnoreParser"/> with built-in defaults active.</summary>
    public SyncIgnoreParser()
    {
        // RebuildMatchers needs both lists initialised first.
        _ignoreRegexes = [];
        _unignoreRegexes = [];
        RebuildMatchers();
    }

    // ── ISyncIgnoreParser ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public IReadOnlyList<string> BuiltInPatterns => BuiltInDefaults;

    /// <inheritdoc/>
    public IReadOnlyList<string> UserPatterns => _userPatterns.AsReadOnly();

    /// <inheritdoc/>
    public void Initialize(string syncRoot)
    {
        _userPatterns.Clear();

        var syncIgnorePath = Path.Combine(syncRoot, ".syncignore");
        if (File.Exists(syncIgnorePath))
        {
            foreach (var line in File.ReadAllLines(syncIgnorePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                    continue;
                _userPatterns.Add(trimmed);
            }
        }

        RebuildMatchers();
    }

    /// <inheritdoc/>
    public bool IsIgnored(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return false;

        // Normalise to forward slashes for pattern matching, then strip any
        // leading slash so the path is always relative.
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0)
            return false;

        if (!_ignoreRegexes.Any(r => r.IsMatch(normalized)))
            return false;

        // Allow negation rules to un-ignore the file.
        return !_unignoreRegexes.Any(r => r.IsMatch(normalized));
    }

    /// <inheritdoc/>
    public void SetUserPatterns(IReadOnlyList<string> patterns)
    {
        _userPatterns.Clear();
        _userPatterns.AddRange(patterns);
        RebuildMatchers();
    }

    /// <inheritdoc/>
    public async Task SaveAsync(string syncRoot, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(syncRoot, ".syncignore");
        await File.WriteAllLinesAsync(path, _userPatterns, cancellationToken);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void RebuildMatchers()
    {
        var ignoreList = new List<string>(BuiltInDefaults.Length + _userPatterns.Count);
        var unignoreList = new List<string>();

        foreach (var p in BuiltInDefaults)
            Classify(p, ignoreList, unignoreList);

        foreach (var p in _userPatterns)
            Classify(p, ignoreList, unignoreList);

        _ignoreRegexes = BuildIgnoreRegexes(ignoreList, []);
        _unignoreRegexes = BuildIgnoreRegexes(unignoreList, []);
    }

    private static void Classify(string pattern, List<string> ignoreList, List<string> unignoreList)
    {
        if (pattern.StartsWith('!'))
            unignoreList.Add(ToGlob(pattern[1..]));
        else
            ignoreList.Add(ToGlob(pattern));
    }

    /// <summary>
    /// Converts a <c>.gitignore</c>-style pattern to a forward-slash glob
    /// suitable for direct regex conversion.
    /// </summary>
    private static string ToGlob(string pattern)
    {
        // Normalise separators and strip any leading slash (anchoring to root
        // is handled below via the **/ prefix logic).
        var p = pattern.Replace('\\', '/').TrimStart('/');

        // Directory-only pattern (trailing slash) — match any file inside
        // the named directory, at any depth.
        if (p.EndsWith('/'))
        {
            var dir = p.TrimEnd('/');
            return $"**/{dir}/**";
        }

        // Pattern with no slash — match anywhere in the tree.
        if (!p.Contains('/'))
            return $"**/{p}";

        // Pattern already contains a slash — treat as relative from root,
        // prepending **/ so it matches at any nesting depth.
        return p.StartsWith("**/", StringComparison.Ordinal) ? p : $"**/{p}";
    }

    /// <summary>
    /// Builds a list of compiled <see cref="Regex"/> objects from glob patterns.
    /// Each glob uses <c>**/</c> prefix, <c>*</c>, <c>?</c>, and <c>**</c>
    /// wildcards; all match is case-insensitive.
    /// </summary>
    private static List<Regex> BuildIgnoreRegexes(IEnumerable<string> globs, IEnumerable<string> _)
    {
        var regexes = new List<Regex>();
        foreach (var glob in globs)
            regexes.Add(GlobToRegex(glob));
        return regexes;
    }

    /// <summary>
    /// Converts a forward-slash glob pattern to a <see cref="Regex"/>.
    /// <list type="bullet">
    ///   <item><c>**/</c> prefix → optional path prefix (zero or more levels)</item>
    ///   <item><c>**</c> alone at end → match any remaining path</item>
    ///   <item><c>*</c> → any characters except <c>/</c></item>
    ///   <item><c>?</c> → any single character except <c>/</c></item>
    /// </list>
    /// </summary>
    private static Regex GlobToRegex(string glob)
    {
        var sb = new StringBuilder("^");
        var p = glob.Replace('\\', '/').TrimStart('/');
        int i = 0;

        while (i < p.Length)
        {
            if (i + 1 < p.Length && p[i] == '*' && p[i + 1] == '*')
            {
                i += 2;
                if (i < p.Length && p[i] == '/')
                {
                    // **/ → optional path prefix (0 or more directories then /)
                    sb.Append("(?:.*/)?");
                    i++;
                }
                else
                {
                    // ** at end of pattern → any remaining path
                    sb.Append(".*");
                }
            }
            else if (p[i] == '*')
            {
                // * → any chars except /
                sb.Append("[^/]*");
                i++;
            }
            else if (p[i] == '?')
            {
                // ? → any single char except /
                sb.Append("[^/]");
                i++;
            }
            else
            {
                sb.Append(Regex.Escape(p[i].ToString()));
                i++;
            }
        }

        sb.Append("$");
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.Singleline,
            matchTimeout: TimeSpan.FromSeconds(1));
    }
}

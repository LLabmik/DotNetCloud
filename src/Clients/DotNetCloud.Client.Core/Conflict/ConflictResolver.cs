using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Conflict;

/// <summary>
/// Creates conflict copies following the pattern:
/// <c>report (conflict - Ben - 2025-07-14).docx</c>
/// </summary>
public sealed class ConflictResolver : IConflictResolver
{
    private readonly ILogger<ConflictResolver> _logger;

    /// <inheritdoc/>
    public event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;

    /// <summary>Initializes a new <see cref="ConflictResolver"/>.</summary>
    public ConflictResolver(ILogger<ConflictResolver> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task ResolveAsync(ConflictInfo conflict, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(conflict.LocalPath))
            return Task.CompletedTask;

        var conflictCopyPath = BuildConflictCopyPath(conflict.LocalPath);

        // Rename the local file to the conflict copy path
        File.Move(conflict.LocalPath, conflictCopyPath, overwrite: false);

        _logger.LogWarning(
            "Conflict detected for {Path}. Local copy saved as {ConflictCopy}.",
            conflict.LocalPath, conflictCopyPath);

        ConflictDetected?.Invoke(this, new ConflictDetectedEventArgs
        {
            OriginalPath = conflict.LocalPath,
            ConflictCopyPath = conflictCopyPath,
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds a conflict copy path using the pattern:
    /// <c>{baseName} (conflict - {user} - {date}){ext}</c>
    /// </summary>
    public static string BuildConflictCopyPath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
        var ext = Path.GetExtension(originalPath);
        var baseName = Path.GetFileNameWithoutExtension(originalPath);
        var user = Environment.UserName;
        var date = DateTime.Now.ToString("yyyy-MM-dd");

        var candidate = Path.Combine(directory, $"{baseName} (conflict - {user} - {date}){ext}");
        var n = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName} (conflict - {user} - {date} {n}){ext}");
            n++;
        }
        return candidate;
    }
}

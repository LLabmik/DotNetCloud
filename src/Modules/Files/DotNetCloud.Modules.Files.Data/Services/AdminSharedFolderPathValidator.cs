using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Validates admin shared-folder source paths against the configured root and existing definitions.
/// </summary>
internal sealed class AdminSharedFolderPathValidator : IAdminSharedFolderPathValidator
{
    private readonly FilesDbContext _db;
    private readonly AdminSharedFolderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSharedFolderPathValidator"/> class.
    /// </summary>
    public AdminSharedFolderPathValidator(FilesDbContext db, IOptions<AdminSharedFolderOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<ValidatedAdminSharedFolderPath> ValidateAsync(string sourcePath, Guid? existingDefinitionId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var rootPath = GetCanonicalRootPath();
        var candidatePath = GetCanonicalCandidatePath(rootPath, sourcePath);

        if (!IsPathWithinRoot(rootPath, candidatePath))
            throw new ValidationException(nameof(sourcePath), "Shared folder path must remain inside the configured admin shared-folder root.");

        if (!Directory.Exists(candidatePath))
        {
            var message = File.Exists(candidatePath)
                ? "Shared folder source path must point to a directory."
                : "Shared folder source path does not exist.";
            throw new ValidationException(nameof(sourcePath), message);
        }

        var existingPaths = await _db.AdminSharedFolders
            .AsNoTracking()
            .Where(folder => !existingDefinitionId.HasValue || folder.Id != existingDefinitionId.Value)
            .Select(folder => folder.SourcePath)
            .ToListAsync(cancellationToken);

        foreach (var existingPath in existingPaths)
        {
            var canonicalExistingPath = Path.GetFullPath(existingPath);

            if (PathsEqual(canonicalExistingPath, candidatePath))
                throw new ValidationException(nameof(sourcePath), "Shared folder source path is already registered.");

            if (PathsOverlap(canonicalExistingPath, candidatePath))
                throw new ValidationException(nameof(sourcePath), "Shared folder source path overlaps an existing registered shared folder.");
        }

        var relativePath = Path.GetRelativePath(rootPath, candidatePath)
            .Replace('\\', '/');
        if (relativePath == ".")
            relativePath = string.Empty;

        return new ValidatedAdminSharedFolderPath
        {
            RootPath = rootPath,
            CanonicalPath = candidatePath,
            RelativePath = relativePath,
        };
    }

    private string GetCanonicalRootPath()
    {
        if (string.IsNullOrWhiteSpace(_options.RootPath))
            throw new ValidationException(nameof(AdminSharedFolderOptions.RootPath), "Admin shared-folder root path is not configured.");

        var rootPath = Path.GetFullPath(_options.RootPath);
        if (!Directory.Exists(rootPath))
            throw new ValidationException(nameof(AdminSharedFolderOptions.RootPath), "Configured admin shared-folder root path does not exist.");

        return Path.TrimEndingDirectorySeparator(rootPath);
    }

    private static string GetCanonicalCandidatePath(string rootPath, string sourcePath)
    {
        var candidatePath = Path.IsPathRooted(sourcePath)
            ? Path.GetFullPath(sourcePath)
            : Path.GetFullPath(Path.Combine(rootPath, sourcePath));

        return Path.TrimEndingDirectorySeparator(candidatePath);
    }

    private static bool PathsOverlap(string firstPath, string secondPath)
    {
        return IsPathWithinRoot(firstPath, secondPath) || IsPathWithinRoot(secondPath, firstPath);
    }

    private static bool IsPathWithinRoot(string rootPath, string candidatePath)
    {
        if (PathsEqual(rootPath, candidatePath))
            return true;

        var comparison = GetPathComparison();
        var normalizedRootPath = EnsureTrailingSeparator(rootPath);
        return EnsureTrailingSeparator(candidatePath).StartsWith(normalizedRootPath, comparison);
    }

    private static bool PathsEqual(string firstPath, string secondPath)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(firstPath),
            Path.TrimEndingDirectorySeparator(secondPath),
            GetPathComparison());
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static StringComparison GetPathComparison() => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
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

        var resolvedPath = await ResolveDirectoryAsync(sourcePath, cancellationToken);

        var existingPaths = await _db.AdminSharedFolders
            .AsNoTracking()
            .Where(folder => !existingDefinitionId.HasValue || folder.Id != existingDefinitionId.Value)
            .Select(folder => folder.SourcePath)
            .ToListAsync(cancellationToken);

        foreach (var existingPath in existingPaths)
        {
            var canonicalExistingPath = Path.GetFullPath(existingPath);

            if (PathsEqual(canonicalExistingPath, resolvedPath.CanonicalPath))
                throw new ValidationException(nameof(sourcePath), "Shared folder source path is already registered.");

            if (PathsOverlap(canonicalExistingPath, resolvedPath.CanonicalPath))
                throw new ValidationException(nameof(sourcePath), "Shared folder source path overlaps an existing registered shared folder.");
        }

        return resolvedPath;
    }

    /// <inheritdoc />
    public Task<ValidatedAdminSharedFolderPath> ResolveDirectoryAsync(string? sourcePath = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolution = ResolvePath(sourcePath);
        var rootPath = resolution.RootPath;
        var candidatePath = resolution.CanonicalPath;

        EnsurePathWithinRoot(candidatePath, nameof(sourcePath), rootPath);
        EnsureExistingDirectory(candidatePath, nameof(sourcePath));

        return Task.FromResult(new ValidatedAdminSharedFolderPath
        {
            RootPath = rootPath,
            CanonicalPath = candidatePath,
            RelativePath = GetNormalizedRelativePath(rootPath, candidatePath),
        });
    }

    private (string RootPath, string CanonicalPath) ResolvePath(string? sourcePath)
    {
        var normalizedSourcePath = sourcePath?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedSourcePath))
        {
            var filesystemRootPath = GetFilesystemRootPath();
            return (filesystemRootPath, filesystemRootPath);
        }

        if (Path.IsPathRooted(normalizedSourcePath))
        {
            var canonicalPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(normalizedSourcePath));
            return (GetFilesystemRootPath(canonicalPath), canonicalPath);
        }

        if (TryGetConfiguredBasePath(out var configuredBasePath))
        {
            var canonicalPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(configuredBasePath, normalizedSourcePath)));
            return (configuredBasePath, canonicalPath);
        }

        var defaultRootPath = GetFilesystemRootPath();
        return (defaultRootPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(defaultRootPath, normalizedSourcePath))));
    }

    private bool TryGetConfiguredBasePath(out string configuredBasePath)
    {
        if (!string.IsNullOrWhiteSpace(_options.RootPath))
        {
            var candidateBasePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_options.RootPath));
            if (Directory.Exists(candidateBasePath))
            {
                configuredBasePath = candidateBasePath;
                return true;
            }
        }

        configuredBasePath = string.Empty;
        return false;
    }

    private static string GetFilesystemRootPath(string? path = null)
    {
        var candidatePath = string.IsNullOrWhiteSpace(path)
            ? Path.GetTempPath()
            : path;

        var filesystemRootPath = Path.GetFullPath(Path.GetPathRoot(candidatePath) ?? Path.DirectorySeparatorChar.ToString());
        return Path.TrimEndingDirectorySeparator(filesystemRootPath);
    }

    private static string GetNormalizedRelativePath(string rootPath, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, candidatePath)
            .Replace('\\', '/');

        return relativePath == "."
            ? string.Empty
            : relativePath;
    }

    private static void EnsurePathWithinRoot(string candidatePath, string parameterName, string rootPath)
    {
        if (!IsPathWithinRoot(rootPath, candidatePath))
        {
            throw new ValidationException(parameterName, "Shared folder path must remain inside the current filesystem root.");
        }
    }

    private static void EnsureExistingDirectory(string candidatePath, string parameterName)
    {
        if (Directory.Exists(candidatePath))
        {
            return;
        }

        var message = File.Exists(candidatePath)
            ? "Shared folder source path must point to a directory."
            : "Shared folder source path does not exist.";
        throw new ValidationException(parameterName, message);
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
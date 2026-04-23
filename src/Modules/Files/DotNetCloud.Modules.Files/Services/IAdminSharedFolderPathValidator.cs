namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Validates and canonicalizes admin shared-folder source paths before they are persisted.
/// </summary>
public interface IAdminSharedFolderPathValidator
{
    /// <summary>
    /// Validates a candidate folder path, resolves it within the configured root, and rejects overlaps with existing definitions.
    /// </summary>
    Task<ValidatedAdminSharedFolderPath> ValidateAsync(string sourcePath, Guid? existingDefinitionId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Canonicalized admin shared-folder path information.
/// </summary>
public sealed record ValidatedAdminSharedFolderPath
{
    /// <summary>Configured canonical root path.</summary>
    public required string RootPath { get; init; }

    /// <summary>Canonical absolute path for the shared folder.</summary>
    public required string CanonicalPath { get; init; }

    /// <summary>Normalized path relative to the configured root.</summary>
    public required string RelativePath { get; init; }
}
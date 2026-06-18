namespace DotNetCloud.Core.Models;

/// <summary>
/// Phases of admin shared folder cleanup.
/// </summary>
public enum CleanupPhase
{
    /// <summary>Deleting the definition record (initial phase).</summary>
    DeletingDefinition = 0,

    /// <summary>Removing search index documents for mounted files.</summary>
    RemovingSearchDocs = 1,

    /// <summary>Cleaning up media library sources in user settings.</summary>
    CleaningMediaSources = 2,

    /// <summary>Cleaning up indexed media entities (tracks, videos, photos).</summary>
    CleaningMediaEntities = 3,

    /// <summary>Cleanup completed successfully.</summary>
    Complete = 4,

    /// <summary>Cleanup failed with an error.</summary>
    Failed = 5,
}

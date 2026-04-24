namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Signals the admin shared-folder maintenance worker to process pending rescan and reindex requests.
/// </summary>
internal interface IAdminSharedFolderMaintenanceScheduler
{
    /// <summary>
    /// Requests an immediate maintenance cycle.
    /// </summary>
    void TriggerProcessing();
}
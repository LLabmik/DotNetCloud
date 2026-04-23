namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Group grant that allows members of a group to access an admin shared folder.
/// </summary>
public sealed class AdminSharedFolderGrant
{
    /// <summary>Unique identifier for the grant.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Shared-folder definition that owns this grant.</summary>
    public Guid AdminSharedFolderId { get; set; }

    /// <summary>Navigation property to the shared-folder definition.</summary>
    public AdminSharedFolderDefinition? AdminSharedFolder { get; set; }

    /// <summary>Granted group identifier from Core identity data.</summary>
    public Guid GroupId { get; set; }

    /// <summary>When the grant was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
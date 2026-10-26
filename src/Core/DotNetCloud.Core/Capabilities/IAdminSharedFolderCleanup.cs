namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Privileged capability that lets an approved module ask the core to clean up the core-side
/// state of a deleted administrator shared folder.
/// </summary>
/// <remarks>
/// This is deliberately separate from the public capability surface: the operation destroys
/// data on behalf of other users, so it requires an explicit privileged grant rather than the
/// free-form module transport.
/// </remarks>
public interface IAdminSharedFolderCleanup : ICapabilityInterface
{
}

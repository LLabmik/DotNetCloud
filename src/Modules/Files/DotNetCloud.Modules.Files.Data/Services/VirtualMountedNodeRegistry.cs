using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DotNetCloud.Modules.Files.Data.Services;

internal static class VirtualMountedNodeRegistry
{
    private static readonly ConcurrentDictionary<Guid, VirtualMountedNodeDescriptor> Entries = new();

    internal static Guid DotNetCloudRootId { get; } = CreateStableGuid("virtual::_dotnetcloud-root");

    internal static Guid SharedWithMeRootId { get; } = CreateStableGuid("virtual::_dotnetcloud-shared-with-me");

    internal static Guid GetAdminSharedFolderRootId(Guid sharedFolderId)
        => CreateStableGuid($"virtual::admin-shared-root::{sharedFolderId:D}");

    internal static Guid GetMountedNodeId(Guid sharedFolderId, string relativePath, bool isDirectory)
        => CreateStableGuid($"virtual::admin-shared-entry::{sharedFolderId:D}::{(isDirectory ? "dir" : "file")}::{NormalizeRelativePath(relativePath)}");

    internal static void Register(VirtualMountedNodeDescriptor descriptor)
    {
        Entries[descriptor.Id] = descriptor;
    }

    internal static bool TryGet(Guid id, out VirtualMountedNodeDescriptor descriptor)
    {
        return Entries.TryGetValue(id, out descriptor!);
    }

    internal static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').Trim('/');
    }

    private static Guid CreateStableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}

internal sealed record VirtualMountedNodeDescriptor(Guid Id, Guid SharedFolderId, string RelativePath, bool IsDirectory);
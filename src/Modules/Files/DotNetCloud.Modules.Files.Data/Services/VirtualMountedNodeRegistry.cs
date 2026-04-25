using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

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

    /// <summary>
    /// Registers the descriptor in memory and persists it to the database so it survives process restarts.
    /// </summary>
    internal static async Task RegisterAndPersistAsync(VirtualMountedNodeDescriptor descriptor, FilesDbContext db, CancellationToken cancellationToken)
    {
        Entries[descriptor.Id] = descriptor;

        if (!await db.MountedNodeEntries.AnyAsync(e => e.Id == descriptor.Id, cancellationToken))
        {
            db.MountedNodeEntries.Add(new MountedNodeEntry
            {
                Id = descriptor.Id,
                SharedFolderId = descriptor.SharedFolderId,
                RelativePath = descriptor.RelativePath,
                IsDirectory = descriptor.IsDirectory,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Tries the in-memory cache first, then falls back to the database.
    /// On a DB hit, the entry is re-registered in memory for subsequent lookups.
    /// </summary>
    internal static async Task<(bool Found, VirtualMountedNodeDescriptor? Descriptor)> TryGetOrLoadAsync(Guid id, FilesDbContext db, CancellationToken cancellationToken)
    {
        if (Entries.TryGetValue(id, out var descriptor))
        {
            return (true, descriptor);
        }

        var entry = await db.MountedNodeEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entry is null)
        {
            return (false, null);
        }

        descriptor = new VirtualMountedNodeDescriptor(entry.Id, entry.SharedFolderId, entry.RelativePath, entry.IsDirectory);
        Entries[entry.Id] = descriptor;
        return (true, descriptor);
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

using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Models;
using DotNetCloud.Modules.Sdk.Api;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Shared helpers for Photos service tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>Creates a fresh InMemory PhotosDbContext.</summary>
    public static PhotosDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PhotosDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        return new PhotosDbContext(options);
    }

    /// <summary>Creates a CallerContext for a user.</summary>
    public static CallerContext CreateCaller(Guid? userId = null)
        => new(userId ?? Guid.CreateVersion7(), ["user"], CallerType.User);

    /// <summary>Seeds a photo in the database.</summary>
    public static async Task<Photo> SeedPhotoAsync(
        PhotosDbContext db,
        Guid ownerId,
        string fileName = "test.jpg",
        string mimeType = "image/jpeg",
        long sizeBytes = 1024)
    {
        var photo = new Photo
        {
            FileNodeId = Guid.CreateVersion7(),
            OwnerId = ownerId,
            FileName = fileName,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            TakenAt = DateTime.UtcNow
        };
        db.Photos.Add(photo);
        await db.SaveChangesAsync();
        return photo;
    }

    /// <summary>Seeds an album in the database.</summary>
    public static async Task<Album> SeedAlbumAsync(
        PhotosDbContext db,
        Guid ownerId,
        string title = "Test Album")
    {
        var album = new Album
        {
            OwnerId = ownerId,
            Title = title
        };
        db.Albums.Add(album);
        await db.SaveChangesAsync();
        return album;
    }

    /// <summary>Seeds a photo with EXIF metadata attached.</summary>
    public static async Task<Photo> SeedPhotoWithMetadataAsync(
        PhotosDbContext db,
        Guid ownerId,
        double? latitude = null,
        double? longitude = null)
    {
        var photo = await SeedPhotoAsync(db, ownerId);
        var metadata = new PhotoMetadata
        {
            PhotoId = photo.Id,
            CameraMake = "Canon",
            CameraModel = "EOS R5",
            Iso = 200,
            Latitude = latitude,
            Longitude = longitude
        };
        db.PhotoMetadata.Add(metadata);
        await db.SaveChangesAsync();
        photo.Metadata = metadata;
        return photo;
    }

    /// <summary>Seeds a photo share.</summary>
    public static async Task<PhotoShare> SeedPhotoShareAsync(
        PhotosDbContext db,
        Guid photoId,
        Guid sharedByUserId,
        Guid sharedWithUserId)
    {
        var share = new PhotoShare
        {
            PhotoId = photoId,
            SharedByUserId = sharedByUserId,
            SharedWithUserId = sharedWithUserId,
            Permission = PhotoSharePermissionLevel.ReadOnly
        };
        db.PhotoShares.Add(share);
        await db.SaveChangesAsync();
        return share;
    }
}

/// <summary>
/// A verified module-user assertion for the current test flow, standing in for the core-signed one.
/// </summary>
internal sealed class FakeModuleUserAssertionAccessor : IModuleUserAssertionAccessor
{
    /// <summary>Initializes a new instance of the <see cref="FakeModuleUserAssertionAccessor"/> class.</summary>
    /// <param name="userId">The user the "core" asserted, or <see langword="null"/> for none.</param>
    public FakeModuleUserAssertionAccessor(Guid? userId) => UserId = userId;

    /// <summary>Gets the asserted user.</summary>
    public Guid? UserId { get; }

    /// <inheritdoc />
    public Guid? GetCurrentUserId() => UserId;
}

/// <summary>
/// An <see cref="IDncModuleApi"/> that serves one file node's bytes from memory.
/// </summary>
internal sealed class FakeFileModuleApi : IDncModuleApi
{
    private readonly byte[]? _content;
    private readonly Exception? _failure;

    /// <summary>Initializes a new instance of the <see cref="FakeFileModuleApi"/> class.</summary>
    /// <param name="content">The bytes to return, or <see langword="null"/> to fail the read.</param>
    public FakeFileModuleApi(byte[]? content) => _content = content;

    /// <summary>
    /// Initializes a fake core that refuses every read with the given exception — used to model the
    /// core's consent gate (<c>PermissionDenied</c> + <c>MODULE_CONSENT_REQUIRED</c>).
    /// </summary>
    /// <param name="failure">The exception the read throws.</param>
    public FakeFileModuleApi(Exception failure)
    {
        _failure = failure;
    }

    /// <summary>Gets the node ids that were requested.</summary>
    public List<string> DownloadedNodeIds { get; } = [];

    /// <inheritdoc />
    public Task<Stream> DownloadFileAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        DownloadedNodeIds.Add(nodeId);

        if (_failure is not null)
        {
            throw _failure;
        }

        if (_content is null)
        {
            throw new InvalidOperationException("The file is not available.");
        }

        return Task.FromResult<Stream>(new MemoryStream(_content, writable: false));
    }

    /// <inheritdoc />
    public Task<DncCurrentUser> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<DncModuleUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<DncModuleUser>> SearchUsersAsync(string query, int maxResults = 20, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<int> SendNotificationAsync(DncNotification notification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> PublishEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> SubmitSearchIndexAsync(string entityId, bool remove = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> SetSettingAsync(string key, string value, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> LogAuditAsync(AuditAction action, string entityType, Guid entityId, string? description = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<DncFileNode>> ListFilesAsync(string? parentId = null, int page = 1, int pageSize = 200, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<DncFileNode> UploadFileAsync(string? parentId, string fileName, string contentType, Stream content, long totalSize = 0, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

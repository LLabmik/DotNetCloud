using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Guards that dropping edits also un-bakes the previews.
/// </summary>
/// <remarks>
/// "Revert all" cleared the edit stack but left the cached previews exactly as the last save rendered
/// them, so a reverted photo still showed the rotation — the reported defect. Undo had the same shape:
/// the stack shrank, the previews kept the removed operation.
/// </remarks>
[TestClass]
public class PhotoEditPreviewRegenerationTests
{
    private PhotosDbContext _db = null!;
    private Mock<IEventBus> _eventBus = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBus = new Mock<IEventBus>();
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task RevertAll_RegeneratesThePreviewsFromTheOriginal()
    {
        var previews = new RecordingThumbnailService();
        var photo = await SeedQueuedRotateAsync();
        var service = CreateService(previews);

        await service.RevertAllAsync(photo.Id, _caller);

        CollectionAssert.AreEqual(new[] { photo.Id }, previews.Regenerated);
        Assert.AreEqual(0, _db.PhotoEditRecords.Count());
    }

    [TestMethod]
    public async Task UndoLastEdit_RegeneratesThePreviews()
    {
        var previews = new RecordingThumbnailService();
        var photo = await SeedQueuedRotateAsync();
        var service = CreateService(previews);

        await service.UndoLastEditAsync(photo.Id, _caller);

        CollectionAssert.AreEqual(new[] { photo.Id }, previews.Regenerated);
    }

    [TestMethod]
    public async Task RevertAll_WhenThePreviewRebuildFails_StillClearsTheStack()
    {
        var photo = await SeedQueuedRotateAsync();
        var service = CreateService(new RecordingThumbnailService(throwOnRegenerate: true));

        await service.RevertAllAsync(photo.Id, _caller);

        Assert.AreEqual(0, _db.PhotoEditRecords.Count());
    }

    private PhotoEditService CreateService(IPhotoThumbnailService previews) =>
        new(_db, _eventBus.Object, NullLogger<PhotoEditService>.Instance, previews);

    private async Task<Photo> SeedQueuedRotateAsync()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        _db.PhotoEditRecords.Add(new PhotoEditRecord
        {
            PhotoId = photo.Id,
            OperationType = "Rotate",
            ParametersJson = """{"value":"90"}""",
            StackOrder = 1,
            EditedByUserId = _caller.UserId,
        });
        await _db.SaveChangesAsync();
        return photo;
    }

    private sealed class RecordingThumbnailService(bool throwOnRegenerate = false) : IPhotoThumbnailService
    {
        public List<Guid> Regenerated { get; } = [];

        public Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
            Guid photoId,
            Guid actingUserId,
            PhotoThumbnailSize size,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(Stream?, string?)>((null, null));

        public Task<bool> GenerateThumbnailsAsync(
            Guid photoId,
            Guid actingUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task DeleteThumbnailsAsync(Guid photoId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> SaveEditsAsync(
            Guid photoId,
            Guid actingUserId,
            CancellationToken cancellationToken = default) =>
            RegeneratePreviewsAsync(photoId, actingUserId, cancellationToken);

        public Task<bool> RegeneratePreviewsAsync(
            Guid photoId,
            Guid actingUserId,
            CancellationToken cancellationToken = default)
        {
            Regenerated.Add(photoId);

            if (throwOnRegenerate)
            {
                throw new InvalidOperationException("the core could not serve the original");
            }

            return Task.FromResult(true);
        }
    }
}

using DotNetCloud.Core.Server.SharedWithMe;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Core.SharedWithMe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.SharedWithMe;

/// <summary>
/// The core cannot read the Photos module's share tables (it owns its own schema), so the Files
/// "Shared with me → Photos" entries come from the module's <c>GetSharedWithMe</c> RPC.
/// </summary>
[TestClass]
public class PhotosSharedWithMeProviderTests
{
    [TestMethod]
    public async Task ListAsync_MapsAlbumAndPhotoSharesToDeepLinks()
    {
        var albumId = Guid.CreateVersion7();
        var photoId = Guid.CreateVersion7();
        var provider = CreateProvider(
        [
            new PhotoSharedItem(Guid.CreateVersion7(), null, albumId, "Summer", "ReadOnly", DateTime.UtcNow),
            new PhotoSharedItem(Guid.CreateVersion7(), photoId, null, "sunset.jpg", "Download", DateTime.UtcNow),
        ]);

        var items = await provider.ListAsync(Guid.CreateVersion7());

        Assert.AreEqual(2, items.Count);

        var album = items.Single(item => item.EntityType == "Album");
        Assert.AreEqual(albumId, album.EntityId);
        Assert.AreEqual("Summer", album.Title);
        Assert.AreEqual($"/modules/dotnetcloud.photos?album={albumId}", album.DeepLink);

        var photo = items.Single(item => item.EntityType == "Photo");
        Assert.AreEqual(photoId, photo.EntityId);
        Assert.AreEqual("sunset.jpg", photo.Title);
        Assert.AreEqual($"/modules/dotnetcloud.photos?photoId={photoId}", photo.DeepLink);
    }

    [TestMethod]
    public async Task ListAsync_SkipsSharesWhoseEntityNoLongerResolves()
    {
        var provider = CreateProvider(
        [
            new PhotoSharedItem(Guid.CreateVersion7(), Guid.CreateVersion7(), null, string.Empty, "ReadOnly", DateTime.UtcNow),
        ]);

        var items = await provider.ListAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, items.Count);
    }

    [TestMethod]
    public async Task ListAsync_ModuleUnavailable_ReturnsEmptyInsteadOfThrowing()
    {
        var provider = CreateProvider([], throwOnList: true);

        var items = await provider.ListAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, items.Count);
    }

    [TestMethod]
    public async Task CountAsync_CountsTheReportedShares()
    {
        var provider = CreateProvider(
        [
            new PhotoSharedItem(Guid.CreateVersion7(), Guid.CreateVersion7(), null, "a.jpg", "ReadOnly", DateTime.UtcNow),
            new PhotoSharedItem(Guid.CreateVersion7(), null, Guid.CreateVersion7(), "Trip", "ReadOnly", DateTime.UtcNow),
        ]);

        Assert.AreEqual(2, await provider.CountAsync(Guid.CreateVersion7()));
    }

    [TestMethod]
    public void ModuleId_IsTheStableShortName()
    {
        var provider = CreateProvider([]);

        Assert.AreEqual("photos", provider.ModuleId);
        Assert.AreEqual("photo_library", provider.IconName);
    }

    private static PhotosSharedWithMeProvider CreateProvider(
        IReadOnlyList<PhotoSharedItem> items,
        bool throwOnList = false)
    {
        var client = new FakePhotosApiClient(items, throwOnList);
        var services = new ServiceCollection();
        services.AddSingleton<IPhotosApiClient>(client);

        return new PhotosSharedWithMeProvider(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PhotosSharedWithMeProvider>.Instance);
    }

    private sealed class FakePhotosApiClient : IPhotosApiClient
    {
        private readonly IReadOnlyList<PhotoSharedItem> _items;
        private readonly bool _throwOnList;

        public FakePhotosApiClient(IReadOnlyList<PhotoSharedItem> items, bool throwOnList)
        {
            _items = items;
            _throwOnList = throwOnList;
        }

        public Task<IReadOnlyList<PhotoSharedItem>> GetSharedWithMeAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            _throwOnList
                ? Task.FromException<IReadOnlyList<PhotoSharedItem>>(new InvalidOperationException("module offline"))
                : Task.FromResult(_items);
    }
}

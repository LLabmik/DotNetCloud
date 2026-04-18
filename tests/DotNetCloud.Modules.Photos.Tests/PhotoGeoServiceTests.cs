using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class PhotoGeoServiceTests
{
    private PhotosDbContext _db;
    private PhotoGeoService _service;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new PhotoGeoService(_db, NullLogger<PhotoGeoService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── GetGeoTaggedPhotos ───────────────────────────────────────────

    [TestMethod]
    public async Task GetGeoTaggedPhotos_ReturnsOnlyGeoTagged()
    {
        await TestHelpers.SeedPhotoWithMetadataAsync(_db, _caller.UserId, latitude: 48.8566, longitude: 2.3522);
        await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "no-geo.jpg");

        var result = await _service.GetGeoTaggedPhotosAsync(_caller.UserId);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task GetGeoTaggedPhotos_EmptyForNoGeoData()
    {
        await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        var result = await _service.GetGeoTaggedPhotosAsync(_caller.UserId);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetGeoTaggedPhotos_MultipleLocations_ReturnsAll()
    {
        await TestHelpers.SeedPhotoWithMetadataAsync(_db, _caller.UserId, latitude: 48.8566, longitude: 2.3522);
        await TestHelpers.SeedPhotoWithMetadataAsync(_db, _caller.UserId, latitude: 40.4168, longitude: -3.7038);
        await TestHelpers.SeedPhotoWithMetadataAsync(_db, _caller.UserId, latitude: 51.5074, longitude: -0.1278);

        var result = await _service.GetGeoTaggedPhotosAsync(_caller.UserId);

        Assert.AreEqual(3, result.Count);
    }

    // ─── GetGeoClusters ───────────────────────────────────────────────

    [TestMethod]
    public async Task GetGeoClusters_ClustersByProximity()
    {
        // Two photos very close together, one far away
        await TestHelpers.SeedPhotoWithMetadataAsync(_db, _caller.UserId, latitude: 48.8566, longitude: 2.3522);
        await TestHelpers.SeedPhotoWithMetadataAsync(_db, _caller.UserId, latitude: 48.8580, longitude: 2.3542);
        await TestHelpers.SeedPhotoWithMetadataAsync(_db, _caller.UserId, latitude: -33.8688, longitude: 151.2093);

        var clusters = await _service.GetGeoClustersAsync(_caller.UserId, gridSizeDegrees: 1.0);

        Assert.IsTrue(clusters.Count >= 2, "Should have at least 2 distinct clusters");
    }

    [TestMethod]
    public async Task GetGeoClusters_EmptyForNoData_ReturnsEmpty()
    {
        var clusters = await _service.GetGeoClustersAsync(_caller.UserId);

        Assert.AreEqual(0, clusters.Count);
    }

    [TestMethod]
    public async Task GetGeoClusters_SingleCluster_ReturnsOneCluster()
    {
        await TestHelpers.SeedPhotoWithMetadataAsync(_db, _caller.UserId, latitude: 48.8566, longitude: 2.3522);

        var clusters = await _service.GetGeoClustersAsync(_caller.UserId, gridSizeDegrees: 10.0);

        Assert.AreEqual(1, clusters.Count);
        Assert.AreEqual(1, clusters[0].PhotoCount);
    }
}

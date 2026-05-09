using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class SwimlaneServiceTests
{
    private TracksDbContext _db = null!;
    private SwimlaneService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new SwimlaneService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateSwimlaneAsync_CreatesSwimlaneOnProduct()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var dto = new CreateSwimlaneDto { Title = "To Do" };

        var result = await _service.CreateSwimlaneAsync(SwimlaneContainerType.Product, product.Id, dto, CancellationToken.None);

        Assert.AreEqual("To Do", result.Title);
        Assert.AreEqual(SwimlaneContainerType.Product, result.ContainerType);
        Assert.AreEqual(product.Id, result.ContainerId);
    }

    [TestMethod]
    public async Task GetSwimlanesAsync_ReturnsSwimlanesForContainer()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        await _service.CreateSwimlaneAsync(SwimlaneContainerType.Product, product.Id, new CreateSwimlaneDto { Title = "A" }, CancellationToken.None);
        await _service.CreateSwimlaneAsync(SwimlaneContainerType.Product, product.Id, new CreateSwimlaneDto { Title = "B" }, CancellationToken.None);

        var result = await _service.GetSwimlanesAsync(SwimlaneContainerType.Product, product.Id, CancellationToken.None);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task UpdateSwimlaneAsync_UpdatesTitle()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var created = await _service.CreateSwimlaneAsync(SwimlaneContainerType.Product, product.Id, new CreateSwimlaneDto { Title = "Old" }, CancellationToken.None);
        var dto = new UpdateSwimlaneDto { Title = "New" };

        var result = await _service.UpdateSwimlaneAsync(created.Id, dto, CancellationToken.None);

        Assert.AreEqual("New", result.Title);
    }

    [TestMethod]
    public async Task DeleteSwimlaneAsync_DeletesSwimlane()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var created = await _service.CreateSwimlaneAsync(SwimlaneContainerType.Product, product.Id, new CreateSwimlaneDto { Title = "To Delete" }, CancellationToken.None);

        await _service.DeleteSwimlaneAsync(created.Id, CancellationToken.None);

        var result = await _service.GetSwimlaneByIdAsync(created.Id, CancellationToken.None);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ReorderSwimlanesAsync_UpdatesPositions()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var a = await _service.CreateSwimlaneAsync(SwimlaneContainerType.Product, product.Id, new CreateSwimlaneDto { Title = "A" }, CancellationToken.None);
        var b = await _service.CreateSwimlaneAsync(SwimlaneContainerType.Product, product.Id, new CreateSwimlaneDto { Title = "B" }, CancellationToken.None);

        var result = await _service.ReorderSwimlanesAsync([b.Id, a.Id], CancellationToken.None);

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.First(s => s.Id == b.Id).Position < result.First(s => s.Id == a.Id).Position);
    }
}

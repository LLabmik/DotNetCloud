using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class AttachmentServiceTests
{
    private TracksDbContext _db = null!;
    private AttachmentService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new AttachmentService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task AddAttachmentAsync_AddsAttachmentToWorkItem()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        var fileNodeId = Guid.NewGuid();

        var result = await _service.AddAttachmentAsync(item.Id, userId, "report.pdf", 1024, "application/pdf", fileNodeId, null, CancellationToken.None);

        Assert.AreEqual("report.pdf", result.FileName);
        Assert.AreEqual(1024, result.FileSize);
        Assert.AreEqual(item.Id, result.WorkItemId);
    }

    [TestMethod]
    public async Task GetAttachmentsByWorkItemAsync_ReturnsAttachments()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        var fileNodeId = Guid.NewGuid();
        await _service.AddAttachmentAsync(item.Id, userId, "a.pdf", 100, "application/pdf", fileNodeId, null, CancellationToken.None);
        await _service.AddAttachmentAsync(item.Id, userId, "b.pdf", 200, "application/pdf", fileNodeId, null, CancellationToken.None);

        var result = await _service.GetAttachmentsByWorkItemAsync(item.Id, CancellationToken.None);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task RemoveAttachmentAsync_DeletesAttachment()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        var fileNodeId = Guid.NewGuid();
        var attachment = await _service.AddAttachmentAsync(item.Id, userId, "tmp.pdf", 50, "application/pdf", fileNodeId, null, CancellationToken.None);

        await _service.RemoveAttachmentAsync(attachment.Id, CancellationToken.None);

        var result = await _service.GetAttachmentsByWorkItemAsync(item.Id, CancellationToken.None);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetAttachmentsByWorkItemAsync_NoAttachments_ReturnsEmptyList()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());

        var result = await _service.GetAttachmentsByWorkItemAsync(item.Id, CancellationToken.None);

        Assert.AreEqual(0, result.Count);
    }
}

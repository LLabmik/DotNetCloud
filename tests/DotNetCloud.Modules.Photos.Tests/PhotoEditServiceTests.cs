using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class PhotoEditServiceTests
{
    private PhotosDbContext _db;
    private PhotoEditService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _service = new PhotoEditService(_db, _eventBusMock.Object, NullLogger<PhotoEditService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── ApplyEdit ────────────────────────────────────────────────────

    [TestMethod]
    public async Task ApplyEdit_Rotate90_Succeeds()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string> { ["degrees"] = "90" }
        };

        var result = await _service.ApplyEditAsync(photo.Id, op, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, _db.PhotoEditRecords.Count());
    }

    [TestMethod]
    public async Task ApplyEdit_Rotate45_ThrowsInvalidPhotoEdit()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string> { ["degrees"] = "45" }
        };

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ApplyEditAsync(photo.Id, op, _caller));
        Assert.AreEqual(ErrorCodes.InvalidPhotoEdit, ex.ErrorCode);
    }

    [TestMethod]
    public async Task ApplyEdit_CropMissingParameter_ThrowsInvalidPhotoEdit()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Crop,
            Parameters = new Dictionary<string, string> { ["x"] = "0", ["y"] = "0" }
        };

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ApplyEditAsync(photo.Id, op, _caller));
        Assert.AreEqual(ErrorCodes.InvalidPhotoEdit, ex.ErrorCode);
    }

    [TestMethod]
    public async Task ApplyEdit_CropValid_Succeeds()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Crop,
            Parameters = new Dictionary<string, string>
            {
                ["x"] = "10", ["y"] = "20", ["width"] = "100", ["height"] = "200"
            }
        };

        var result = await _service.ApplyEditAsync(photo.Id, op, _caller);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task ApplyEdit_FlipHorizontal_Succeeds()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Flip,
            Parameters = new Dictionary<string, string> { ["direction"] = "horizontal" }
        };

        var result = await _service.ApplyEditAsync(photo.Id, op, _caller);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task ApplyEdit_FlipInvalidDirection_ThrowsInvalidPhotoEdit()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Flip,
            Parameters = new Dictionary<string, string> { ["direction"] = "diagonal" }
        };

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ApplyEditAsync(photo.Id, op, _caller));
        Assert.AreEqual(ErrorCodes.InvalidPhotoEdit, ex.ErrorCode);
    }

    [TestMethod]
    public async Task ApplyEdit_BrightnessValid_Succeeds()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Brightness,
            Parameters = new Dictionary<string, string> { ["value"] = "0.5" }
        };

        var result = await _service.ApplyEditAsync(photo.Id, op, _caller);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task ApplyEdit_BrightnessOutOfRange_ThrowsInvalidPhotoEdit()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Brightness,
            Parameters = new Dictionary<string, string> { ["value"] = "2.0" }
        };

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ApplyEditAsync(photo.Id, op, _caller));
        Assert.AreEqual(ErrorCodes.InvalidPhotoEdit, ex.ErrorCode);
    }

    [TestMethod]
    public async Task ApplyEdit_SharpenValid_Succeeds()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Sharpen,
            Parameters = new Dictionary<string, string> { ["radius"] = "50" }
        };

        var result = await _service.ApplyEditAsync(photo.Id, op, _caller);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task ApplyEdit_SharpenOutOfRange_ThrowsInvalidPhotoEdit()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Sharpen,
            Parameters = new Dictionary<string, string> { ["radius"] = "150" }
        };

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ApplyEditAsync(photo.Id, op, _caller));
        Assert.AreEqual(ErrorCodes.InvalidPhotoEdit, ex.ErrorCode);
    }

    [TestMethod]
    public async Task ApplyEdit_NonExistentPhoto_ThrowsBusinessRuleException()
    {
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string> { ["degrees"] = "90" }
        };

        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ApplyEditAsync(Guid.NewGuid(), op, _caller));
    }

    [TestMethod]
    public async Task ApplyEdit_MultipleEdits_IncreasesStackOrder()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        var op1 = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string> { ["degrees"] = "90" }
        };
        var op2 = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Brightness,
            Parameters = new Dictionary<string, string> { ["value"] = "0.3" }
        };

        await _service.ApplyEditAsync(photo.Id, op1, _caller);
        await _service.ApplyEditAsync(photo.Id, op2, _caller);

        var records = _db.PhotoEditRecords.OrderBy(r => r.StackOrder).ToList();
        Assert.AreEqual(2, records.Count);
        Assert.IsTrue(records[0].StackOrder < records[1].StackOrder);
    }

    // ─── GetEditStack ─────────────────────────────────────────────────

    [TestMethod]
    public async Task GetEditStack_ReturnsAllEdits()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string> { ["degrees"] = "180" }
        };
        await _service.ApplyEditAsync(photo.Id, op, _caller);

        var stack = await _service.GetEditStackAsync(photo.Id);

        Assert.AreEqual(1, stack.Count);
    }

    [TestMethod]
    public async Task GetEditStack_NoEdits_ReturnsEmpty()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        var stack = await _service.GetEditStackAsync(photo.Id);

        Assert.AreEqual(0, stack.Count);
    }

    // ─── UndoLastEdit ─────────────────────────────────────────────────

    [TestMethod]
    public async Task UndoLastEdit_RemovesLastEdit()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string> { ["degrees"] = "90" }
        };
        await _service.ApplyEditAsync(photo.Id, op, _caller);

        await _service.UndoLastEditAsync(photo.Id, _caller);

        Assert.AreEqual(0, _db.PhotoEditRecords.Count());
    }

    [TestMethod]
    public async Task UndoLastEdit_NonExistentPhoto_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.UndoLastEditAsync(Guid.NewGuid(), _caller));
    }

    // ─── RevertAll ────────────────────────────────────────────────────

    [TestMethod]
    public async Task RevertAll_RemovesAllEdits()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        for (int i = 0; i < 3; i++)
        {
            var op = new PhotoEditOperationDto
            {
                OperationType = PhotoEditType.Rotate,
                Parameters = new Dictionary<string, string> { ["degrees"] = "90" }
            };
            await _service.ApplyEditAsync(photo.Id, op, _caller);
        }

        await _service.RevertAllAsync(photo.Id, _caller);

        Assert.AreEqual(0, _db.PhotoEditRecords.Count());
    }

    [TestMethod]
    public async Task RevertAll_NonExistentPhoto_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.RevertAllAsync(Guid.NewGuid(), _caller));
    }
}

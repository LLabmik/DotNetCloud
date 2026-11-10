using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

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
            Parameters = new Dictionary<string, string> { ["value"] = "90" }
        };

        var result = await _service.ApplyEditAsync(photo.Id, op, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, _db.PhotoEditRecords.Count());
    }

    [TestMethod]
    public async Task ApplyEdit_ReturnsABoundaryDtoNotTheEntityGraph()
    {
        // Regression: the endpoint used to return the PhotoEditRecord entity, whose loaded `Photo`
        // navigation serialised every cached thumbnail as base64 — a 27 MB body for a tiny edit, which
        // the UI proxy reset mid-stream (the browser reported ERR_HTTP2_PROTOCOL_ERROR).
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        photo.ThumbnailGrid = new byte[512 * 1024];
        photo.ThumbnailDetail = new byte[1024 * 1024];
        await _db.SaveChangesAsync();

        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string> { ["value"] = "90" }
        };

        var result = await _service.ApplyEditAsync(photo.Id, op, _caller);

        Assert.IsInstanceOfType<PhotoEditRecordDto>(result);
        Assert.AreEqual("Rotate", result.OperationType);
        Assert.AreEqual("90", result.Parameters["value"]);
        Assert.AreEqual(1, result.StackOrder);

        var json = JsonSerializer.Serialize(result);
        Assert.IsFalse(json.Contains("thumbnail", StringComparison.OrdinalIgnoreCase),
            "The edit response must not carry the photo, and with it the thumbnail blobs.");
        Assert.IsTrue(json.Length < 1024, $"The edit response should be tiny, was {json.Length} chars.");
    }

    [TestMethod]
    public async Task ApplyEdit_Rotate270_Succeeds()
    {
        // Quarter turn anticlockwise. ImageSharp has no negative rotate mode, so this is 270 and not -90.
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string> { ["value"] = "270" }
        };

        var result = await _service.ApplyEditAsync(photo.Id, op, _caller);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task ApplyEdit_Rotate45_ThrowsInvalidPhotoEdit()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string> { ["value"] = "45" }
        };

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ApplyEditAsync(photo.Id, op, _caller));
        Assert.AreEqual(ErrorCodes.InvalidPhotoEdit, ex.ErrorCode);
    }

    [TestMethod]
    public async Task ApplyEdit_RotateWithoutValue_ThrowsInvalidPhotoEdit()
    {
        // A rotation with no value used to be stored and then silently skipped by the renderer.
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string>()
        };

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ApplyEditAsync(photo.Id, op, _caller));
        Assert.AreEqual(ErrorCodes.InvalidPhotoEdit, ex.ErrorCode);
        Assert.AreEqual(0, _db.PhotoEditRecords.Count(), "A rejected edit must not be stored.");
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
                ["x"] = "10",
                ["y"] = "20",
                ["width"] = "100",
                ["height"] = "200"
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
            Parameters = new Dictionary<string, string> { ["value"] = "0" }
        };

        var result = await _service.ApplyEditAsync(photo.Id, op, _caller);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task ApplyEdit_FlipVertical_Succeeds()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Flip,
            Parameters = new Dictionary<string, string> { ["value"] = "1" }
        };

        var result = await _service.ApplyEditAsync(photo.Id, op, _caller);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task ApplyEdit_FlipInvalidValue_ThrowsInvalidPhotoEdit()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var op = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Flip,
            Parameters = new Dictionary<string, string> { ["value"] = "diagonal" }
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
            Parameters = new Dictionary<string, string> { ["value"] = "200.0" }
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
            Parameters = new Dictionary<string, string> { ["value"] = "90" }
        };

        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ApplyEditAsync(Guid.CreateVersion7(), op, _caller));
    }

    [TestMethod]
    public async Task ApplyEdit_MultipleEdits_IncreasesStackOrder()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        var op1 = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string> { ["value"] = "90" }
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
            Parameters = new Dictionary<string, string> { ["value"] = "180" }
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
            Parameters = new Dictionary<string, string> { ["value"] = "90" }
        };
        await _service.ApplyEditAsync(photo.Id, op, _caller);

        await _service.UndoLastEditAsync(photo.Id, _caller);

        Assert.AreEqual(0, _db.PhotoEditRecords.Count());
    }

    [TestMethod]
    public async Task UndoLastEdit_NonExistentPhoto_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.UndoLastEditAsync(Guid.CreateVersion7(), _caller));
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
                Parameters = new Dictionary<string, string> { ["value"] = "90" }
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
            () => _service.RevertAllAsync(Guid.CreateVersion7(), _caller));
    }
}

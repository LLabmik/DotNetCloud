using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Host.Controllers;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class PhotosControllerSaveEditsTests
{
    private PhotosDbContext _db = null!;
    private Mock<IPhotoThumbnailService> _thumbnailServiceMock = null!;
    private PhotosController _controller = null!;
    private Guid _userId;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _userId = Guid.NewGuid();
        var eventBus = Mock.Of<IEventBus>();

        var photoService = new PhotoService(_db, eventBus, NullLogger<PhotoService>.Instance);
        var albumService = new AlbumService(_db, eventBus, NullLogger<AlbumService>.Instance);
        var metadataService = new PhotoMetadataService(_db, NullLogger<PhotoMetadataService>.Instance);
        var geoService = new PhotoGeoService(_db, NullLogger<PhotoGeoService>.Instance);
        var shareService = new PhotoShareService(_db, eventBus, NullLogger<PhotoShareService>.Instance);
        var editService = new PhotoEditService(_db, eventBus, NullLogger<PhotoEditService>.Instance);
        var slideshowService = new SlideshowService(_db, NullLogger<SlideshowService>.Instance);
        _thumbnailServiceMock = new Mock<IPhotoThumbnailService>();

        _controller = new PhotosController(
            photoService,
            albumService,
            metadataService,
            geoService,
            shareService,
            editService,
            slideshowService,
            _thumbnailServiceMock.Object,
            NullLogger<PhotosController>.Instance);

        SetupAuthenticatedUser(_controller, _userId);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private static void SetupAuthenticatedUser(ControllerBase controller, Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "user")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // ─── Photo Not Found ─────────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_PhotoNotFound_ReturnsNotFound()
    {
        var result = await _controller.SaveEditsAsync(Guid.NewGuid());

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task SaveEditsAsync_PhotoNotFound_DoesNotCallThumbnailService()
    {
        await _controller.SaveEditsAsync(Guid.NewGuid());

        _thumbnailServiceMock.Verify(
            t => t.SaveEditsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Photo Belongs To Another User ───────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_PhotoBelongsToOtherUser_ReturnsNotFound()
    {
        var otherUserId = Guid.NewGuid();
        var photo = await TestHelpers.SeedPhotoAsync(_db, otherUserId);

        var result = await _controller.SaveEditsAsync(photo.Id);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // ─── Save Succeeds ──────────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_ValidPhoto_ReturnsOk()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _userId);
        _thumbnailServiceMock
            .Setup(t => t.SaveEditsAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.SaveEditsAsync(photo.Id);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task SaveEditsAsync_ValidPhoto_CallsThumbnailService()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _userId);
        _thumbnailServiceMock
            .Setup(t => t.SaveEditsAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _controller.SaveEditsAsync(photo.Id);

        _thumbnailServiceMock.Verify(
            t => t.SaveEditsAsync(photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Save Fails ─────────────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_ThumbnailServiceFails_Returns500()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _userId);
        _thumbnailServiceMock
            .Setup(t => t.SaveEditsAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.SaveEditsAsync(photo.Id);

        var objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(500, objectResult.StatusCode);
    }

    // ─── Unauthenticated User ────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_UnauthenticatedUser_ThrowsUnauthorized()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _userId);

        // Clear the authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => _controller.SaveEditsAsync(photo.Id));
    }

    // ─── Response Body Verification ──────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_Success_ResponseContainsSavedTrue()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _userId);
        _thumbnailServiceMock
            .Setup(t => t.SaveEditsAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.SaveEditsAsync(photo.Id);

        var okResult = Assert.IsInstanceOfType<OkObjectResult>(result);
        Assert.IsNotNull(okResult.Value);

        var valueType = okResult.Value.GetType();
        var successProp = valueType.GetProperty("success");
        Assert.IsNotNull(successProp);
        Assert.AreEqual(true, successProp.GetValue(okResult.Value));
    }

    [TestMethod]
    public async Task SaveEditsAsync_Failure_ResponseContainsErrorCode()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _userId);
        _thumbnailServiceMock
            .Setup(t => t.SaveEditsAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.SaveEditsAsync(photo.Id);

        var objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.IsNotNull(objectResult.Value);

        var valueType = objectResult.Value.GetType();
        var successProp = valueType.GetProperty("success");
        Assert.IsNotNull(successProp);
        Assert.AreEqual(false, successProp.GetValue(objectResult.Value));
    }
}

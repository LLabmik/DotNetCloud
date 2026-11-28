using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Security;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.Email.Host.Controllers;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using DotNetCloud.Modules.Sdk.Api;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DotNetCloud.Modules.Email.Tests;

/// <summary>
/// Tests for <see cref="EmailFilesController"/>: the module's half of the file capability.
/// </summary>
/// <remarks>
/// The controller never touches the Files module. It calls <see cref="IDncModuleApi"/>, which the
/// core evaluates against the capability grant and the user's consent, so these tests only need to
/// prove the module shapes the request correctly and honours the attachment policy.
/// </remarks>
[TestClass]
public class EmailFilesControllerTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();

    [TestMethod]
    public async Task BrowseFiles_WithoutCoreApi_ThrowsUnavailable()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, coreApi: null);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(() => controller.BrowseFiles(null));

        Assert.AreEqual(StatusCode.Unavailable, ex.StatusCode);
    }

    [TestMethod]
    public async Task BrowseFiles_WithoutAssertion_ThrowsUnauthorizedBeforeTouchingTheCore()
    {
        var coreApi = new Mock<IDncModuleApi>(MockBehavior.Strict);

        await using var db = CreateDb();
        var controller = CreateController(db, coreApi.Object, withoutAssertion: true);

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => controller.BrowseFiles(null));

        coreApi.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task BrowseFiles_WithCoreApi_ReturnsTheFolderListing()
    {
        var nodeId = Guid.CreateVersion7();
        var coreApi = new Mock<IDncModuleApi>();
        coreApi
            .Setup(api => api.ListFilesAsync(null, 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DncFileNode(nodeId.ToString(), "Report.pdf", "File", "application/pdf", 2048, string.Empty, string.Empty),
            ]);

        await using var db = CreateDb();
        var controller = CreateController(db, coreApi.Object);

        var result = await controller.BrowseFiles(null) as OkObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(200, result.StatusCode);
    }

    [TestMethod]
    public async Task AttachFromFiles_DisallowedExtension_ReturnsBadRequest()
    {
        var coreApi = new Mock<IDncModuleApi>(MockBehavior.Strict);

        await using var db = CreateDb();
        var controller = CreateController(db, coreApi.Object);

        var result = await controller.AttachFromFiles(
            new AttachFromFileRequest(Guid.CreateVersion7().ToString(), "payload.exe", "application/octet-stream", 10));

        var bad = result as BadRequestObjectResult;
        Assert.IsNotNull(bad, "the attachment whitelist applies to files copied out of the user's drive too");
    }

    [TestMethod]
    public async Task AttachFromFiles_ValidFile_StoresItAndReturnsTheUploadShape()
    {
        var nodeId = Guid.CreateVersion7();
        var coreApi = new Mock<IDncModuleApi>();
        coreApi
            .Setup(api => api.DownloadFileAsync(nodeId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));

        var storage = new Mock<IAttachmentStorage>();
        storage
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), "report.pdf", "application/pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttachmentStorageResult { StorageKey = "abc123", ContentHash = "abc123", Size = 3 });

        await using var db = CreateDb();
        var controller = CreateController(db, coreApi.Object, storage.Object);

        var result = await controller.AttachFromFiles(
            new AttachFromFileRequest(nodeId.ToString(), "report.pdf", "application/pdf", 3)) as OkObjectResult;

        Assert.IsNotNull(result);
        storage.Verify(
            s => s.StoreAsync(It.IsAny<Stream>(), "report.pdf", "application/pdf", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SaveAttachmentToFiles_AttachmentOfAnotherUser_ThrowsForbidden()
    {
        var coreApi = new Mock<IDncModuleApi>(MockBehavior.Strict);

        await using var db = CreateDb();

        var account = new EmailAccount { OwnerId = Guid.CreateVersion7(), DisplayName = "Other", EmailAddress = "o@example.com" };
        var message = new EmailMessage { AccountId = account.Id };
        var attachment = new EmailAttachment
        {
            MessageId = message.Id,
            FileName = "invoice.pdf",
            ContentType = "application/pdf",
            Size = 10,
            StorageKey = "key",
        };

        db.EmailAccounts.Add(account);
        db.EmailMessages.Add(message);
        db.EmailAttachments.Add(attachment);
        await db.SaveChangesAsync();

        var controller = CreateController(db, coreApi.Object);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => controller.SaveAttachmentToFiles(attachment.Id, new SaveToFilesRequest()));
    }

    [TestMethod]
    public async Task SaveAttachmentToFiles_OwnedAttachment_SendsTheContentToTheCore()
    {
        var coreApi = new Mock<IDncModuleApi>();
        coreApi
            .Setup(api => api.UploadFileAsync(
                null,
                "invoice.pdf",
                "application/pdf",
                It.IsAny<Stream>(),
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DncFileNode(
                Guid.CreateVersion7().ToString(), "invoice.pdf", "File", "application/pdf", 10, string.Empty, string.Empty));

        var storage = new Mock<IAttachmentStorage>();
        storage
            .Setup(s => s.OpenReadAsync("key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]));

        await using var db = CreateDb();

        var account = new EmailAccount { OwnerId = UserId, DisplayName = "Me", EmailAddress = "me@example.com" };
        var message = new EmailMessage { AccountId = account.Id };
        var attachment = new EmailAttachment
        {
            MessageId = message.Id,
            FileName = "invoice.pdf",
            ContentType = "application/pdf",
            Size = 10,
            StorageKey = "key",
        };

        db.EmailAccounts.Add(account);
        db.EmailMessages.Add(message);
        db.EmailAttachments.Add(attachment);
        await db.SaveChangesAsync();

        var controller = CreateController(db, coreApi.Object, storage.Object);

        var result = await controller.SaveAttachmentToFiles(attachment.Id, new SaveToFilesRequest()) as OkObjectResult;

        Assert.IsNotNull(result);
        coreApi.Verify(
            api => api.UploadFileAsync(
                null,
                "invoice.pdf",
                "application/pdf",
                It.IsAny<Stream>(),
                10,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static EmailDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<EmailDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options);

    private static EmailFilesController CreateController(
        EmailDbContext db,
        IDncModuleApi? coreApi,
        IAttachmentStorage? storage = null,
        bool withoutAssertion = false)
    {
        var accessor = new Mock<IModuleUserAssertionAccessor>();

        if (!withoutAssertion)
        {
            accessor.Setup(a => a.GetCurrentUserId()).Returns(UserId);
        }

        var services = new ServiceCollection()
            .AddSingleton(accessor.Object)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };

        return new EmailFilesController(
            db,
            storage ?? Mock.Of<IAttachmentStorage>(),
            new FileValidationService(),
            coreApi)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }
}

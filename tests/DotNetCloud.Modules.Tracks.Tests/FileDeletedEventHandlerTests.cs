using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Events;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class FileDeletedEventHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_CallsCleanupServiceWithCorrectFileNodeId()
    {
        // Arrange
        var fileNodeId = Guid.NewGuid();
        var cleanupService = new Mock<ICardAttachmentCleanupService>();
        cleanupService.Setup(s => s.ClearFileReferencesAsync(fileNodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var services = new ServiceCollection();
        services.AddScoped(_ => cleanupService.Object);
        var provider = services.BuildServiceProvider();

        var handler = new FileDeletedEventHandler(
            provider,
            NullLogger<FileDeletedEventHandler>.Instance);

        var @event = new FileDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileNodeId,
            FileName = "test.pdf",
            DeletedByUserId = Guid.NewGuid(),
            IsPermanent = true
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        cleanupService.Verify(s => s.ClearFileReferencesAsync(fileNodeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_CleanupServiceThrows_DoesNotPropagate()
    {
        // Arrange
        var cleanupService = new Mock<ICardAttachmentCleanupService>();
        cleanupService.Setup(s => s.ClearFileReferencesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var services = new ServiceCollection();
        services.AddScoped(_ => cleanupService.Object);
        var provider = services.BuildServiceProvider();

        var handler = new FileDeletedEventHandler(
            provider,
            NullLogger<FileDeletedEventHandler>.Instance);

        var @event = new FileDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "test.pdf",
            DeletedByUserId = Guid.NewGuid(),
            IsPermanent = false
        };

        // Act — should not throw
        await handler.HandleAsync(@event, CancellationToken.None);
    }
}

[TestClass]
public class CardAttachmentCleanupServiceTests
{
    [TestMethod]
    public async Task ClearFileReferences_MatchingAttachments_ClearsFileNodeId()
    {
        // Arrange
        var db = TestHelpers.CreateDb();
        var caller = TestHelpers.CreateCaller();
        var board = await TestHelpers.SeedBoardAsync(db, caller.UserId);
        var list = await TestHelpers.SeedListAsync(db, board.Id);
        var card = await TestHelpers.SeedCardAsync(db, list.Id, caller.UserId);

        var fileNodeId = Guid.NewGuid();
        db.CardAttachments.Add(new CardAttachment
        {
            CardId = card.Id,
            FileNodeId = fileNodeId,
            FileName = "test.pdf",
            UploadedByUserId = caller.UserId
        });
        await db.SaveChangesAsync();

        ICardAttachmentCleanupService service = new CardAttachmentCleanupService(
            db, NullLogger<CardAttachmentCleanupService>.Instance);

        // Act
        var count = await service.ClearFileReferencesAsync(fileNodeId);

        // Assert
        Assert.AreEqual(1, count);
        var attachment = db.CardAttachments.First();
        Assert.IsNull(attachment.FileNodeId);
        Assert.AreEqual("test.pdf", attachment.FileName);
    }

    [TestMethod]
    public async Task ClearFileReferences_NoMatchingAttachments_ReturnsZero()
    {
        // Arrange
        var db = TestHelpers.CreateDb();
        var caller = TestHelpers.CreateCaller();
        var board = await TestHelpers.SeedBoardAsync(db, caller.UserId);
        var list = await TestHelpers.SeedListAsync(db, board.Id);
        var card = await TestHelpers.SeedCardAsync(db, list.Id, caller.UserId);

        db.CardAttachments.Add(new CardAttachment
        {
            CardId = card.Id,
            FileNodeId = Guid.NewGuid(),
            FileName = "other.pdf",
            UploadedByUserId = caller.UserId
        });
        await db.SaveChangesAsync();

        ICardAttachmentCleanupService service = new CardAttachmentCleanupService(
            db, NullLogger<CardAttachmentCleanupService>.Instance);

        // Act
        var count = await service.ClearFileReferencesAsync(Guid.NewGuid());

        // Assert
        Assert.AreEqual(0, count);
        var attachment = db.CardAttachments.First();
        Assert.IsNotNull(attachment.FileNodeId);
    }

    [TestMethod]
    public async Task ClearFileReferences_MultipleAttachments_ClearsAll()
    {
        // Arrange
        var db = TestHelpers.CreateDb();
        var caller = TestHelpers.CreateCaller();
        var board = await TestHelpers.SeedBoardAsync(db, caller.UserId);
        var list = await TestHelpers.SeedListAsync(db, board.Id);
        var card1 = await TestHelpers.SeedCardAsync(db, list.Id, caller.UserId, "Card 1");
        var card2 = await TestHelpers.SeedCardAsync(db, list.Id, caller.UserId, "Card 2");

        var fileNodeId = Guid.NewGuid();
        db.CardAttachments.Add(new CardAttachment
        {
            CardId = card1.Id,
            FileNodeId = fileNodeId,
            FileName = "shared.pdf",
            UploadedByUserId = caller.UserId
        });
        db.CardAttachments.Add(new CardAttachment
        {
            CardId = card2.Id,
            FileNodeId = fileNodeId,
            FileName = "shared.pdf",
            UploadedByUserId = caller.UserId
        });
        await db.SaveChangesAsync();

        ICardAttachmentCleanupService service = new CardAttachmentCleanupService(
            db, NullLogger<CardAttachmentCleanupService>.Instance);

        // Act
        var count = await service.ClearFileReferencesAsync(fileNodeId);

        // Assert
        Assert.AreEqual(2, count);
        Assert.IsTrue(db.CardAttachments.All(a => a.FileNodeId == null));
    }
}

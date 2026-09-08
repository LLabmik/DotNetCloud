using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.SharedWithMe;
using DotNetCloud.Core.Server.SharedWithMe;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Data.Services;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.SharedWithMe;

/// <summary>
/// Tests for <see cref="NotesSharedWithMeProvider"/>: the Notes module adapter that feeds Files'
/// virtual "_DotNetCloud/SharedWithMe/Notes" folder.
/// </summary>
[TestClass]
public class NotesSharedWithMeProviderTests
{
    private static CallerContext UserCaller(Guid userId) => new(userId, ["user"], CallerType.User);

    /// <summary>
    /// Builds a provider whose DI scope can resolve a real <see cref="NoteService"/> backed by an
    /// in-memory <see cref="NotesDbContext"/> sharing the given store name.
    /// </summary>
    private static NotesSharedWithMeProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<NotesDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<INoteService, NoteService>();
        services.AddSingleton<DotNetCloud.Core.Events.IEventBus>(Mock.Of<DotNetCloud.Core.Events.IEventBus>());
        services.AddSingleton<IAuditLogger>(Mock.Of<IAuditLogger>());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<NoteService>>(NullLogger<NoteService>.Instance);

        var teamDirectory = new Mock<ITeamDirectory>();
        teamDirectory
            .Setup(directory => directory.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TeamInfo>());
        services.AddSingleton(teamDirectory.Object);

        services.AddSingleton<NotesSharedWithMeProvider>();
        // Note: the built provider must stay alive for the returned provider's DI scopes to work,
        // so it is intentionally not disposed here (test-lifetime singleton).
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<NotesSharedWithMeProvider>();
    }

    /// <summary>Seeds an owner note shared with the recipient (user share) and returns the note id.</summary>
    private static async Task<Guid> SeedSharedNoteAsync(string dbName, Guid ownerId, Guid recipientId, string title)
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseInMemoryDatabase(dbName).Options;
        using var db = new NotesDbContext(options);
        var noteService = new NoteService(
            db, Mock.Of<DotNetCloud.Core.Events.IEventBus>(), Mock.Of<IAuditLogger>(), NullLogger<NoteService>.Instance);
        var owner = UserCaller(ownerId);

        var created = await noteService.CreateNoteAsync(new CreateNoteDto { Title = title }, owner);

        var shareService = new NoteShareService(db, Mock.Of<DotNetCloud.Core.Events.IEventBus>(), NullLogger<NoteShareService>.Instance);
        await shareService.ShareNoteAsync(created.Id, recipientId, null, NoteSharePermission.ReadOnly, owner);
        return created.Id;
    }

    private static async Task SeedOwnedNoteAsync(string dbName, Guid ownerId, string title)
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseInMemoryDatabase(dbName).Options;
        using var db = new NotesDbContext(options);
        var noteService = new NoteService(
            db, Mock.Of<DotNetCloud.Core.Events.IEventBus>(), Mock.Of<IAuditLogger>(), NullLogger<NoteService>.Instance);
        await noteService.CreateNoteAsync(new CreateNoteDto { Title = title }, UserCaller(ownerId));
    }

    [TestMethod]
    public async Task ListAsync_SharedNoteForRecipient_ReturnsMappedDeepLinkItemExcludingOwnedNotes()
    {
        var dbName = Guid.CreateVersion7().ToString();
        var ownerId = Guid.CreateVersion7();
        var recipientId = Guid.CreateVersion7();

        var sharedNoteId = await SeedSharedNoteAsync(dbName, ownerId, recipientId, "Shared Board Notes");
        await SeedOwnedNoteAsync(dbName, recipientId, "My Private Note");

        var provider = BuildProvider(dbName);
        var items = await provider.ListAsync(recipientId);

        Assert.AreEqual(1, items.Count);
        var onlyItem = items.SingleOrDefault();
        Assert.IsNotNull(onlyItem);
        var item = onlyItem!;
        Assert.AreEqual("notes", item.ModuleId);
        Assert.AreEqual("Notes", item.DisplayName);
        Assert.AreEqual("Note", item.EntityType);
        Assert.AreEqual(sharedNoteId, item.EntityId);
        Assert.AreEqual("Shared Board Notes", item.Title);
        Assert.AreEqual($"/apps/notes?noteId={sharedNoteId}", item.DeepLink);
        Assert.AreEqual("edit_note", item.IconName);
        Assert.AreEqual(1, await provider.CountAsync(recipientId));
    }

    [TestMethod]
    public async Task ListAsync_OnlyOwnedNotes_ReturnsEmpty()
    {
        var dbName = Guid.CreateVersion7().ToString();
        var recipientId = Guid.CreateVersion7();

        await SeedOwnedNoteAsync(dbName, recipientId, "Solo Note");

        var provider = BuildProvider(dbName);
        var items = await provider.ListAsync(recipientId);

        Assert.AreEqual(0, items.Count);
        Assert.AreEqual(0, await provider.CountAsync(recipientId));
    }

    [TestMethod]
    public async Task ListAsync_NoShares_ReturnsEmpty()
    {
        var dbName = Guid.CreateVersion7().ToString();
        var recipientId = Guid.CreateVersion7();

        var provider = BuildProvider(dbName);
        var items = await provider.ListAsync(recipientId);

        Assert.AreEqual(0, items.Count);
    }
}

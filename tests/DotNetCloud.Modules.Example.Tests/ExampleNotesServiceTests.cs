using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Example.Data;
using DotNetCloud.Modules.Example.Host.Services;
using DotNetCloud.Modules.Example.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Tests for <see cref="ExampleNotesService"/> — the domain logic behind the module's own web UI.
/// </summary>
/// <remarks>
/// The module UI is served from the module process and every request is authenticated by the
/// core-signed assertion, so the only thing this layer must guarantee is that an operation is
/// confined to the verified user it was given: one user can never list or delete another's notes.
/// </remarks>
[TestClass]
public class ExampleNotesServiceTests
{
    private ServiceProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        // A fixed database name per test: the options lambda runs on every scope resolution.
        var databaseName = Guid.CreateVersion7().ToString();
        var services = new ServiceCollection();
        services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
        services.AddDbContext<ExampleDbContext>(options => options.UseInMemoryDatabase(databaseName));
        _provider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _provider.Dispose();
    }

    [TestMethod]
    public async Task ListAsync_TwoUsers_ReturnsOnlyTheCallersNotes()
    {
        var caller = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await SeedNoteAsync(caller, "mine");
        await SeedNoteAsync(other, "theirs");

        var notes = await CreateService().ListAsync(caller);

        Assert.AreEqual(1, notes.Count);
        Assert.AreEqual("mine", notes[0].Title);
    }

    [TestMethod]
    public async Task ListAsync_SeveralNotes_ReturnsNewestFirst()
    {
        var caller = Guid.CreateVersion7();
        await SeedNoteAsync(caller, "older", DateTime.UtcNow.AddHours(-2));
        await SeedNoteAsync(caller, "newer", DateTime.UtcNow);

        var notes = await CreateService().ListAsync(caller);

        Assert.AreEqual("newer", notes[0].Title);
        Assert.AreEqual("older", notes[1].Title);
    }

    [TestMethod]
    public async Task CreateAsync_ValidNote_StoresItForTheCaller()
    {
        var caller = Guid.CreateVersion7();

        var result = await CreateService().CreateAsync(caller, "Title", "Body");

        Assert.AreEqual(ExampleNoteCreateStatus.Created, result.Status);
        Assert.IsNotNull(result.Note);
        Assert.AreEqual("Title", result.Note.Title);
        Assert.AreEqual("Body", result.Note.Content);

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
        var stored = await db.Notes.SingleAsync();
        Assert.AreEqual(caller, stored.CreatedByUserId, "the owner is the verified caller");
    }

    [TestMethod]
    public async Task CreateAsync_WhitespaceTitle_ReturnsTitleRequired()
    {
        var result = await CreateService().CreateAsync(Guid.CreateVersion7(), "   ", "Body");

        Assert.AreEqual(ExampleNoteCreateStatus.TitleRequired, result.Status);
        Assert.IsNull(result.Note);
    }

    [TestMethod]
    public async Task CreateAsync_TitleIsTrimmed()
    {
        var result = await CreateService().CreateAsync(Guid.CreateVersion7(), "  Padded  ", null);

        Assert.AreEqual(ExampleNoteCreateStatus.Created, result.Status);
        Assert.AreEqual("Padded", result.Note!.Title);
        Assert.AreEqual(string.Empty, result.Note.Content);
    }

    [TestMethod]
    public async Task CreateAsync_OversizedTitle_ReturnsTitleTooLong()
    {
        var title = new string('a', ExampleNotesService.MaxTitleLength + 1);

        var result = await CreateService().CreateAsync(Guid.CreateVersion7(), title, "Body");

        Assert.AreEqual(ExampleNoteCreateStatus.TitleTooLong, result.Status);
        Assert.IsNull(result.Note);
    }

    [TestMethod]
    public async Task CreateAsync_OversizedContent_ReturnsContentTooLong()
    {
        var content = new string('a', ExampleNotesService.MaxContentLength + 1);

        var result = await CreateService().CreateAsync(Guid.CreateVersion7(), "Title", content);

        Assert.AreEqual(ExampleNoteCreateStatus.ContentTooLong, result.Status);
        Assert.IsNull(result.Note);
    }

    [TestMethod]
    public async Task DeleteAsync_AnotherUsersNote_ReturnsFalseAndKeepsTheNote()
    {
        var owner = Guid.CreateVersion7();
        var attacker = Guid.CreateVersion7();
        var note = await SeedNoteAsync(owner, "private");

        var deleted = await CreateService().DeleteAsync(attacker, note);

        Assert.IsFalse(deleted);
        Assert.AreEqual(1, await CountNotesAsync(owner), "another user's note must survive");
    }

    [TestMethod]
    public async Task DeleteAsync_OwnNote_DeletesOnlyThatNote()
    {
        var caller = Guid.CreateVersion7();
        var target = await SeedNoteAsync(caller, "target");
        await SeedNoteAsync(caller, "kept");

        var deleted = await CreateService().DeleteAsync(caller, target);

        Assert.IsTrue(deleted);
        Assert.AreEqual(1, await CountNotesAsync(caller));
    }

    [TestMethod]
    public async Task DeleteAsync_UnknownNote_ReturnsFalse()
    {
        var deleted = await CreateService().DeleteAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.IsFalse(deleted);
    }

    private ExampleNotesService CreateService()
    {
        var scope = _provider.CreateScope();
        return new ExampleNotesService(scope.ServiceProvider.GetRequiredService<ExampleDbContext>());
    }

    private async Task<Guid> SeedNoteAsync(Guid userId, string title, DateTime? createdAt = null)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();

        var note = new ExampleNote
        {
            Title = title,
            Content = "body",
            CreatedByUserId = userId,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };

        db.Notes.Add(note);
        await db.SaveChangesAsync();
        return note.Id;
    }

    private async Task<int> CountNotesAsync(Guid userId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
        return await db.Notes.CountAsync(note => note.CreatedByUserId == userId);
    }
}

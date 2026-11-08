using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Example.Data;
using DotNetCloud.Modules.Example.Host.Services;
using DotNetCloud.Modules.Example.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Tests for <see cref="ExampleUserErasureHandler"/>: the module owns an isolated data store, so it
/// must delete exactly one user's rows and report how many it removed (F12).
/// </summary>
[TestClass]
public class ExampleUserErasureHandlerTests
{
    private ServiceProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        // The database name is fixed per test: the options lambda runs on every scope resolution,
        // so a name generated inside it would give each scope its own empty database.
        var databaseName = Guid.CreateVersion7().ToString();
        var services = new ServiceCollection();
        services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
        services.AddDbContext<ExampleDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        _provider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _provider.Dispose();
    }

    [TestMethod]
    public async Task EraseAsync_UserWithNotes_DeletesOnlyThatUsersNotes()
    {
        var erasedUser = Guid.CreateVersion7();
        var otherUser = Guid.CreateVersion7();
        await SeedNotesAsync(erasedUser, 3);
        await SeedNotesAsync(otherUser, 2);

        var result = await CreateHandler().EraseAsync(Context(erasedUser));

        Assert.IsTrue(result.Acknowledged);
        Assert.IsFalse(result.NotApplicable);
        Assert.AreEqual(3, result.DeletedCounts[ExampleUserErasureHandler.AllScopeKey]);
        Assert.AreEqual(2, await CountNotesAsync(otherUser), "another user's notes must survive");
        Assert.AreEqual(0, await CountNotesAsync(erasedUser));
    }

    [TestMethod]
    public async Task EraseAsync_UserWithoutNotes_SucceedsWithZeroCount()
    {
        await SeedNotesAsync(Guid.CreateVersion7(), 1);

        var result = await CreateHandler().EraseAsync(Context(Guid.CreateVersion7()));

        Assert.IsTrue(result.Acknowledged);
        Assert.AreEqual(0, result.DeletedCounts[ExampleUserErasureHandler.AllScopeKey]);
    }

    [TestMethod]
    public async Task EraseAsync_SameUserTwice_IsIdempotent()
    {
        var erasedUser = Guid.CreateVersion7();
        await SeedNotesAsync(erasedUser, 2);

        var handler = CreateHandler();
        var first = await handler.EraseAsync(Context(erasedUser));
        var second = await handler.EraseAsync(Context(erasedUser));

        Assert.AreEqual(2, first.DeletedCounts[ExampleUserErasureHandler.AllScopeKey]);
        Assert.AreEqual(0, second.DeletedCounts[ExampleUserErasureHandler.AllScopeKey]);
        Assert.IsTrue(second.Acknowledged);
    }

    private ExampleUserErasureHandler CreateHandler() =>
        new(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExampleUserErasureHandler>.Instance);

    private static ModuleUserErasureContext Context(Guid userId) =>
        new(Guid.CreateVersion7(), userId, []);

    private async Task SeedNotesAsync(Guid userId, int count)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();

        for (var index = 0; index < count; index++)
        {
            db.Notes.Add(new ExampleNote
            {
                Title = $"Note {index}",
                Content = "body",
                CreatedByUserId = userId,
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task<int> CountNotesAsync(Guid userId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
        return await db.Notes.CountAsync(note => note.CreatedByUserId == userId);
    }
}

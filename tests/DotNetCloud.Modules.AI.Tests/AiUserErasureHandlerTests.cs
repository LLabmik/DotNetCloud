using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.AI.Host.Services;
using DotNetCloud.Modules.AI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>
/// Tests for <see cref="AiUserErasureHandler"/> (F12). The module owns an isolated data store, so it
/// must delete exactly one account's conversations and messages — including conversations the user
/// had soft-deleted — and report how many rows it removed.
/// </summary>
[TestClass]
public class AiUserErasureHandlerTests
{
    private ServiceProvider _provider = null!;
    private string _databaseName = null!;

    [TestInitialize]
    public void Setup()
    {
        // A fixed database name per test: the options lambda runs on every scope resolution.
        _databaseName = Guid.CreateVersion7().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AiDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        _provider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup() => _provider.Dispose();

    [TestMethod]
    public async Task EraseAsync_UserWithConversations_DeletesOnlyThatUsersData()
    {
        var erased = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await SeedAsync(erased, conversations: 2, messagesEach: 3);
        await SeedAsync(other, conversations: 1, messagesEach: 2);

        var result = await CreateHandler().EraseAsync(Context(erased));

        Assert.IsTrue(result.Acknowledged);
        Assert.AreEqual(2, result.DeletedCounts[AiUserErasureHandler.ConversationsScopeKey]);
        Assert.AreEqual(6, result.DeletedCounts[AiUserErasureHandler.MessagesScopeKey]);
        Assert.AreEqual(0, await CountConversationsAsync(erased));
        Assert.AreEqual(1, await CountConversationsAsync(other), "another user's conversations must survive");
        Assert.AreEqual(2, await CountMessagesAsync(other));
    }

    [TestMethod]
    public async Task EraseAsync_SoftDeletedConversations_AreAlsoErased()
    {
        var erased = Guid.CreateVersion7();

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
            db.Conversations.Add(new Conversation
            {
                Id = Guid.CreateVersion7(),
                OwnerId = erased,
                Title = "Soft deleted",
                Model = "gpt-oss:20b",
                IsDeleted = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await CreateHandler().EraseAsync(Context(erased));

        // Counted with the query filter disabled: a "deleted" conversation is still personal data.
        using var verify = _provider.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AiDbContext>();
        Assert.AreEqual(0, await verifyDb.Conversations.IgnoreQueryFilters().CountAsync(c => c.OwnerId == erased));
    }

    [TestMethod]
    public async Task EraseAsync_SameUserTwice_IsIdempotent()
    {
        var erased = Guid.CreateVersion7();
        await SeedAsync(erased, conversations: 2, messagesEach: 1);

        var handler = CreateHandler();
        var first = await handler.EraseAsync(Context(erased));
        var second = await handler.EraseAsync(Context(erased));

        Assert.AreEqual(2, first.DeletedCounts[AiUserErasureHandler.ConversationsScopeKey]);
        Assert.AreEqual(0, second.DeletedCounts[AiUserErasureHandler.ConversationsScopeKey]);
        Assert.AreEqual(0, second.DeletedCounts[AiUserErasureHandler.MessagesScopeKey]);
        Assert.IsTrue(second.Acknowledged);
    }

    [TestMethod]
    public async Task EraseAsync_UserWithNothing_AnswersWithZeroCount()
    {
        var result = await CreateHandler().EraseAsync(Context(Guid.CreateVersion7()));

        Assert.IsTrue(result.Acknowledged);
        Assert.IsFalse(result.NotApplicable);
        Assert.AreEqual(0, result.DeletedCounts[AiUserErasureHandler.ConversationsScopeKey]);
        Assert.AreEqual(0, result.DeletedCounts[AiUserErasureHandler.MessagesScopeKey]);
    }

    private AiUserErasureHandler CreateHandler() =>
        new(_provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AiUserErasureHandler>.Instance);

    private static ModuleUserErasureContext Context(Guid userId) => new(Guid.CreateVersion7(), userId, []);

    private async Task SeedAsync(Guid ownerId, int conversations, int messagesEach)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();

        for (var index = 0; index < conversations; index++)
        {
            var conversation = new Conversation
            {
                Id = Guid.CreateVersion7(),
                OwnerId = ownerId,
                Title = $"Conversation {index}",
                Model = "gpt-oss:20b",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Conversations.Add(conversation);

            for (var message = 0; message < messagesEach; message++)
            {
                db.ConversationMessages.Add(new ConversationMessage
                {
                    Id = Guid.CreateVersion7(),
                    ConversationId = conversation.Id,
                    Role = message % 2 == 0 ? "user" : "assistant",
                    Content = $"Message {message}",
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task<int> CountConversationsAsync(Guid ownerId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        return await db.Conversations.IgnoreQueryFilters().CountAsync(c => c.OwnerId == ownerId);
    }

    private async Task<int> CountMessagesAsync(Guid ownerId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        var conversationIds = await db.Conversations
            .IgnoreQueryFilters()
            .Where(c => c.OwnerId == ownerId)
            .Select(c => c.Id)
            .ToListAsync();
        return await db.ConversationMessages.CountAsync(m => conversationIds.Contains(m.ConversationId));
    }
}

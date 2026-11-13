using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Host.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Tracks.Tests.Host;

/// <summary>
/// Account-erasure tests for <see cref="TracksUserErasureHandler"/> (F12).
/// </summary>
/// <remarks>
/// The handler must delete everything the module holds for a deleted account — including rows the
/// user had already soft-deleted — while leaving other accounts' data intact, and it must be safe to
/// retry (the core retries for 72 hours).
/// </remarks>
[TestClass]
public class TracksUserErasureHandlerTests
{
    private static readonly Guid DeletedUser = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUser = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Organization = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>
    /// Products the account owns and its participation rows are erased; another account's data is not.
    /// </summary>
    [TestMethod]
    public async Task EraseAsync_DeletesOwnedAndParticipatingData_LeavesOtherAccountsAlone()
    {
        await using var services = BuildServices();
        var ownedProductId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        var otherWorkItemId = Guid.NewGuid();
        var otherUsersCommentId = Guid.NewGuid();

        await SeedAsync(services, db =>
        {
            db.Products.AddRange(
                new Product { Id = ownedProductId, OrganizationId = Organization, Name = "Owned", OwnerId = DeletedUser },
                new Product { Id = otherProductId, OrganizationId = Organization, Name = "Shared", OwnerId = OtherUser });

            db.WorkItems.Add(new WorkItem
            {
                Id = otherWorkItemId,
                ProductId = otherProductId,
                Title = "Shared item",
                Type = WorkItemType.Item,
                CreatedByUserId = OtherUser,
            });

            db.ProductMembers.AddRange(
                new ProductMember { ProductId = otherProductId, UserId = DeletedUser, Role = ProductMemberRole.Member },
                new ProductMember { ProductId = otherProductId, UserId = OtherUser, Role = ProductMemberRole.Owner });

            db.WorkItemAssignments.AddRange(
                new WorkItemAssignment { WorkItemId = otherWorkItemId, UserId = DeletedUser },
                new WorkItemAssignment { WorkItemId = otherWorkItemId, UserId = OtherUser });

            db.WorkItemComments.AddRange(
                new WorkItemComment { WorkItemId = otherWorkItemId, UserId = DeletedUser, Content = "mine" },
                new WorkItemComment
                {
                    Id = otherUsersCommentId,
                    WorkItemId = otherWorkItemId,
                    UserId = OtherUser,
                    Content = "theirs",
                });

            // A reaction the deleted account left on someone else's comment is its personal data too.
            db.CommentReactions.Add(new CommentReaction
            {
                CommentId = otherUsersCommentId,
                UserId = DeletedUser,
                Emoji = "👍",
            });
        });

        var handler = CreateHandler(services);
        var result = await handler.EraseAsync(new ModuleUserErasureContext(Guid.NewGuid(), DeletedUser, []));

        Assert.IsTrue(result.Acknowledged);
        Assert.AreEqual(1, result.DeletedCounts[TracksUserErasureHandler.ProductsScopeKey]);

        await using var verificationScope = services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<TracksDbContext>();

        Assert.IsFalse(await db.Products.IgnoreQueryFilters().AnyAsync(p => p.Id == ownedProductId),
            "the account's product must be hard-deleted, not soft-deleted");
        Assert.IsTrue(await db.Products.IgnoreQueryFilters().AnyAsync(p => p.Id == otherProductId),
            "another account's product must survive");
        Assert.IsFalse(await db.ProductMembers.AnyAsync(m => m.UserId == DeletedUser),
            "the account's memberships must be gone");
        Assert.IsTrue(await db.ProductMembers.AnyAsync(m => m.UserId == OtherUser),
            "another account's membership must survive");
        Assert.IsFalse(await db.WorkItemAssignments.AnyAsync(a => a.UserId == DeletedUser));
        Assert.IsTrue(await db.WorkItemAssignments.AnyAsync(a => a.UserId == OtherUser));
        Assert.IsFalse(await db.WorkItemComments.IgnoreQueryFilters().AnyAsync(c => c.UserId == DeletedUser));
        Assert.IsTrue(await db.WorkItemComments.IgnoreQueryFilters().AnyAsync(c => c.Id == otherUsersCommentId));
        Assert.IsFalse(await db.CommentReactions.AnyAsync(r => r.UserId == DeletedUser));
    }

    /// <summary>
    /// A row the user had already soft-deleted is still personal data and must be erased.
    /// </summary>
    [TestMethod]
    public async Task EraseAsync_IncludesSoftDeletedRows()
    {
        await using var services = BuildServices();
        var productId = Guid.NewGuid();

        await SeedAsync(services, db =>
        {
            db.Products.Add(new Product
            {
                Id = productId,
                OrganizationId = Organization,
                Name = "Shared",
                OwnerId = OtherUser,
            });
            db.WorkItemComments.Add(new WorkItemComment
            {
                WorkItemId = Guid.NewGuid(),
                UserId = DeletedUser,
                Content = "removed by the user",
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
                DeletedByUserId = DeletedUser,
            });
        });

        var handler = CreateHandler(services);
        await handler.EraseAsync(new ModuleUserErasureContext(Guid.NewGuid(), DeletedUser, []));

        await using var verificationScope = services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<TracksDbContext>();

        Assert.IsFalse(await db.WorkItemComments.IgnoreQueryFilters().AnyAsync(c => c.UserId == DeletedUser));
    }

    /// <summary>
    /// Audit columns that reference the deleted account on surviving rows are cleared.
    /// </summary>
    [TestMethod]
    public async Task EraseAsync_ClearsAuditReferencesOnSurvivingRows()
    {
        await using var services = BuildServices();
        var workItemId = Guid.NewGuid();

        await SeedAsync(services, db =>
        {
            db.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                OrganizationId = Organization,
                Name = "Shared",
                OwnerId = OtherUser,
            });
            db.WorkItems.Add(new WorkItem
            {
                Id = workItemId,
                ProductId = Guid.NewGuid(),
                Title = "Deleted by the account",
                Type = WorkItemType.Item,
                CreatedByUserId = OtherUser,
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
                DeletedByUserId = DeletedUser,
            });
        });

        var handler = CreateHandler(services);
        await handler.EraseAsync(new ModuleUserErasureContext(Guid.NewGuid(), DeletedUser, []));

        await using var verificationScope = services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<TracksDbContext>();

        var row = await db.WorkItems.IgnoreQueryFilters().SingleAsync(w => w.Id == workItemId);
        Assert.IsNull(row.DeletedByUserId, "the deleted account id must not linger in an audit column");
        Assert.IsTrue(row.IsDeleted, "the work item itself belongs to the surviving project and must stay");
    }

    /// <summary>
    /// A retried erasure acknowledges and deletes nothing, because the first pass is complete.
    /// </summary>
    [TestMethod]
    public async Task EraseAsync_WhenCalledTwice_AcknowledgesWithoutRepeatingWork()
    {
        await using var services = BuildServices();

        await SeedAsync(services, db =>
        {
            db.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                OrganizationId = Organization,
                Name = "Owned",
                OwnerId = DeletedUser,
            });
        });

        var handler = CreateHandler(services);
        var request = new ModuleUserErasureContext(Guid.NewGuid(), DeletedUser, []);

        var first = await handler.EraseAsync(request);
        var second = await handler.EraseAsync(request);

        Assert.IsTrue(first.Acknowledged);
        Assert.IsTrue(second.Acknowledged);
        Assert.AreEqual(1, first.DeletedCounts[TracksUserErasureHandler.ProductsScopeKey]);
        Assert.AreEqual(0, second.DeletedCounts[TracksUserErasureHandler.ProductsScopeKey]);
    }

    private static ServiceProvider BuildServices()
    {
        // The name must be captured once: AddDbContext re-runs the options callback per scope, and a
        // name generated inside it would give every scope a private empty store.
        var databaseName = $"tracks-erasure-{Guid.NewGuid():N}";

        return new ServiceCollection()
            .AddDbContext<TracksDbContext>(options =>
                options.UseInMemoryDatabase(databaseName, DatabaseRoot))
            .AddLogging()
            .BuildServiceProvider();
    }

    private static readonly Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot DatabaseRoot = new();

    private static TracksUserErasureHandler CreateHandler(IServiceProvider services) =>
        new(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TracksUserErasureHandler>.Instance);

    private static async Task SeedAsync(IServiceProvider services, Action<TracksDbContext> seed)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TracksDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }
}

using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// No-op implementation of <see cref="ITracksRealtimeService"/> for unit tests.
/// </summary>
internal sealed class NullTracksRealtimeService : ITracksRealtimeService
{
    public Task BroadcastWorkItemActionAsync(Guid productId, Guid workItemId, string action, Guid? fromSwimlaneId = null, Guid? toSwimlaneId = null, Guid? targetUserId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastSwimlaneActionAsync(Guid productId, Guid swimlaneId, string action, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastCommentActionAsync(Guid productId, Guid workItemId, Guid commentId, string action, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastSprintActionAsync(Guid epicId, Guid sprintId, string action, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastActivityAsync(Guid productId, Guid userId, string activityAction, string entityType, Guid entityId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastProductMemberActionAsync(Guid productId, Guid userId, string action, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastTeamActionAsync(Guid teamId, string action, Guid? targetUserId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastReviewItemChangedAsync(Guid sessionId, Guid epicId, Guid itemId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastReviewSessionStateAsync(Guid sessionId, Guid epicId, string action, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastPokerVoteStatusAsync(Guid sessionId, Guid pokerId, Guid userId, bool hasVoted, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastReviewPokerStateAsync(Guid sessionId, Guid pokerId, Guid epicId, string action, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastReviewParticipantChangedAsync(Guid sessionId, Guid userId, string action, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddUserToProductGroupAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveUserFromProductGroupAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddUserToReviewGroupAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveUserFromReviewGroupAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Shared helpers for Tracks service tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>Creates a fresh InMemory TracksDbContext.</summary>
    public static TracksDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TracksDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TracksDbContext(options);
    }

    /// <summary>Creates a CallerContext for a user.</summary>
    public static CallerContext CreateCaller(Guid? userId = null)
        => new(userId ?? Guid.NewGuid(), ["user"], CallerType.User);

    /// <summary>Seeds a Product with the given owner as Owner member.</summary>
    public static async Task<Product> SeedProductAsync(TracksDbContext db, Guid organizationId, Guid ownerId, string name = "Test Product")
    {
        var product = new Product { Name = name, OrganizationId = organizationId, OwnerId = ownerId };
        product.Members.Add(new ProductMember
        {
            ProductId = product.Id,
            UserId = ownerId,
            Role = ProductMemberRole.Owner,
            JoinedAt = DateTime.UtcNow
        });
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    /// <summary>Adds a member to a Product.</summary>
    public static async Task<ProductMember> AddMemberAsync(TracksDbContext db, Guid productId, Guid userId, ProductMemberRole role = ProductMemberRole.Member)
    {
        var member = new ProductMember
        {
            ProductId = productId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
        db.ProductMembers.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    /// <summary>Seeds a Swimlane on a Product or WorkItem container.</summary>
    public static async Task<Swimlane> SeedSwimlaneAsync(TracksDbContext db, Guid containerId, SwimlaneContainerType containerType = SwimlaneContainerType.Product, string title = "Test List")
    {
        var swimlane = new Swimlane
        {
            ContainerId = containerId,
            ContainerType = containerType,
            Title = title,
            Position = 1000.0
        };
        db.Swimlanes.Add(swimlane);
        await db.SaveChangesAsync();
        return swimlane;
    }

    /// <summary>Seeds a WorkItem in a Swimlane. Defaults to Item type.</summary>
    public static async Task<WorkItem> SeedWorkItemAsync(TracksDbContext db, Guid productId, Guid? swimlaneId, Guid createdByUserId, string title = "Test Item", WorkItemType type = WorkItemType.Item)
    {
        var item = new WorkItem
        {
            ProductId = productId,
            SwimlaneId = swimlaneId,
            Title = title,
            Type = type,
            Position = 1000.0,
            CreatedByUserId = createdByUserId
        };
        db.WorkItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    /// <summary>Seeds an Epic WorkItem for use with Sprint/SprintPlanning services.</summary>
    public static async Task<WorkItem> SeedEpicAsync(TracksDbContext db, Guid productId, Guid createdByUserId, string title = "Test Epic")
    {
        return await SeedWorkItemAsync(db, productId, null, createdByUserId, title, WorkItemType.Epic);
    }
}

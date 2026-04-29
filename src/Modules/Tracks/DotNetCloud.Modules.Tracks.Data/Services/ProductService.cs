using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class ProductService
{
    private readonly TracksDbContext _db;

    public ProductService(TracksDbContext db)
    {
        _db = db;
    }

    public async Task<ProductDto> CreateProductAsync(
        Guid organizationId, Guid ownerId, CreateProductDto dto, CancellationToken ct)
    {
        var product = new Product
        {
            OrganizationId = organizationId,
            OwnerId = ownerId,
            Name = dto.Name,
            Description = dto.Description,
            Color = dto.Color,
            SubItemsEnabled = dto.SubItemsEnabled
        };

        _db.Products.Add(product);

        var swimlanes = new[]
        {
            new Swimlane
            {
                ContainerType = SwimlaneContainerType.Product,
                ContainerId = product.Id,
                Title = "To Do",
                Position = 1000
            },
            new Swimlane
            {
                ContainerType = SwimlaneContainerType.Product,
                ContainerId = product.Id,
                Title = "In Progress",
                Position = 2000
            },
            new Swimlane
            {
                ContainerType = SwimlaneContainerType.Product,
                ContainerId = product.Id,
                Title = "Done",
                Position = 3000,
                IsDone = true
            }
        };

        _db.Swimlanes.AddRange(swimlanes);

        var member = new ProductMember
        {
            ProductId = product.Id,
            UserId = ownerId,
            Role = ProductMemberRole.Owner
        };

        _db.ProductMembers.Add(member);

        await _db.SaveChangesAsync(ct);

        return MapToDto(product, swimlanes.Length, 0, 1, 0);
    }

    public async Task<ProductDto> GetProductAsync(Guid productId, CancellationToken ct)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        var swimlaneCount = await _db.Swimlanes
            .CountAsync(s => s.ContainerType == SwimlaneContainerType.Product
                          && s.ContainerId == productId
                          && !s.IsArchived, ct);

        var epicCount = await _db.WorkItems
            .CountAsync(wi => wi.ProductId == productId
                           && wi.Type == WorkItemType.Epic, ct);

        var memberCount = await _db.ProductMembers
            .CountAsync(pm => pm.ProductId == productId, ct);

        var labelCount = await _db.Labels
            .CountAsync(l => l.ProductId == productId, ct);

        return MapToDto(product, swimlaneCount, epicCount, memberCount, labelCount);
    }

    public async Task<ProductDto> UpdateProductAsync(Guid productId, UpdateProductDto dto, CancellationToken ct)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        if (!string.IsNullOrEmpty(dto.ETag) && dto.ETag != product.ETag)
            throw new InvalidOperationException("The product has been modified by another user. Please refresh and try again.");

        if (dto.Name is not null)
            product.Name = dto.Name;
        if (dto.Description is not null)
            product.Description = dto.Description;
        if (dto.Color is not null)
            product.Color = dto.Color;
        if (dto.SubItemsEnabled.HasValue)
            product.SubItemsEnabled = dto.SubItemsEnabled.Value;

        product.ETag = Guid.NewGuid().ToString("N");
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetProductAsync(productId, ct);
    }

    public async Task DeleteProductAsync(Guid productId, Guid deletedByUserId, CancellationToken ct)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        product.DeletedByUserId = deletedByUserId;
        product.ETag = Guid.NewGuid().ToString("N");
        product.UpdatedAt = DateTime.UtcNow;

        // Cascade soft-delete: mark all work items in the product as deleted
        await _db.WorkItems
            .Where(wi => wi.ProductId == productId && !wi.IsDeleted)
            .ExecuteUpdateAsync(calls => calls
                .SetProperty(wi => wi.IsDeleted, true)
                .SetProperty(wi => wi.DeletedAt, DateTime.UtcNow), ct);

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Restores a soft-deleted product and all its work items.
    /// </summary>
    public async Task<ProductDto> UndeleteProductAsync(Guid productId, CancellationToken ct)
    {
        var product = await _db.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsDeleted, ct)
            ?? throw new InvalidOperationException($"Deleted product {productId} not found.");

        product.IsDeleted = false;
        product.DeletedAt = null;
        product.DeletedByUserId = null;
        product.ETag = Guid.NewGuid().ToString("N");
        product.UpdatedAt = DateTime.UtcNow;

        // Restore all soft-deleted work items in the product
        await _db.WorkItems
            .IgnoreQueryFilters()
            .Where(wi => wi.ProductId == productId && wi.IsDeleted)
            .ExecuteUpdateAsync(calls => calls
                .SetProperty(wi => wi.IsDeleted, false)
                .SetProperty(wi => wi.DeletedAt, (DateTime?)null), ct);

        await _db.SaveChangesAsync(ct);
        return await GetProductAsync(productId, ct);
    }

    /// <summary>
    /// Lists soft-deleted products for an organization (admin only).
    /// </summary>
    public async Task<List<ProductDto>> ListDeletedProductsAsync(Guid organizationId, CancellationToken ct)
    {
        var products = await _db.Products
            .IgnoreQueryFilters()
            .Where(p => p.OrganizationId == organizationId && p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .ToListAsync(ct);

        // Resolve display names for deleted-by users
        var deletedByUserIds = products
            .Where(p => p.DeletedByUserId.HasValue)
            .Select(p => p.DeletedByUserId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, string>? displayNames = null;
        if (deletedByUserIds.Count > 0)
        {
            // Try to resolve via IUserDirectory (may not be available from data layer)
            displayNames = deletedByUserIds.ToDictionary(id => id, _ => (string?)null as string)!;
        }

        return products.Select(p => new ProductDto
        {
            Id = p.Id,
            OrganizationId = p.OrganizationId,
            Name = p.Name,
            Description = p.Description,
            Color = p.Color,
            OwnerId = p.OwnerId,
            IsArchived = p.IsArchived,
            ETag = p.ETag,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            DeletedAt = p.DeletedAt,
            DeletedByUserId = p.DeletedByUserId,
            SwimlaneCount = 0,
            EpicCount = 0,
            MemberCount = 0,
            LabelCount = 0
        }).ToList();
    }

    /// <summary>
    /// Permanently deletes a product and all its child entities (work items, swimlanes,
    /// labels, members, etc.). Uses bulk ExecuteDelete for efficiency.
    /// Called by the background cleanup service after the 30-day retention period.
    /// </summary>
    public async Task HardDeleteProductAsync(Guid productId, CancellationToken ct)
    {
        // Collect all work item IDs belonging to this product
        var workItemIds = await _db.WorkItems
            .IgnoreQueryFilters()
            .Where(wi => wi.ProductId == productId)
            .Select(wi => wi.Id)
            .ToListAsync(ct);

        // Collect sprint IDs for work items that are epics
        var sprintIds = await _db.Sprints
            .Where(s => workItemIds.Contains(s.EpicId))
            .Select(s => s.Id)
            .ToListAsync(ct);

        // Collect swimlane IDs (both product-level and work-item-level)
        var swimlaneIds = await _db.Swimlanes
            .Where(s => (s.ContainerType == SwimlaneContainerType.Product && s.ContainerId == productId)
                     || (s.ContainerType == SwimlaneContainerType.WorkItem && workItemIds.Contains(s.ContainerId)))
            .Select(s => s.Id)
            .ToListAsync(ct);

        // Delete in reverse-dependency order using ExecuteDelete

        // PokerVotes → PokerSessions
        if (workItemIds.Count > 0)
        {
            var pokerSessionIds = await _db.PokerSessions
                .Where(ps => workItemIds.Contains(ps.EpicId))
                .Select(ps => ps.Id)
                .ToListAsync(ct);

            if (pokerSessionIds.Count > 0)
            {
                await _db.PokerVotes
                    .Where(pv => pokerSessionIds.Contains(pv.PokerSessionId))
                    .ExecuteDeleteAsync(ct);

                await _db.PokerSessions
                    .Where(ps => pokerSessionIds.Contains(ps.Id))
                    .ExecuteDeleteAsync(ct);
            }

            // ReviewSessions
            await _db.Set<ReviewSession>()
                .Where(rs => workItemIds.Contains(rs.EpicId))
                .ExecuteDeleteAsync(ct);

            // SprintItems → Sprints
            if (sprintIds.Count > 0)
            {
                await _db.SprintItems
                    .Where(si => sprintIds.Contains(si.SprintId))
                    .ExecuteDeleteAsync(ct);

                await _db.Sprints
                    .Where(s => sprintIds.Contains(s.Id))
                    .ExecuteDeleteAsync(ct);
            }

            // ChecklistItems → Checklists
            var checklistIds = await _db.Checklists
                .Where(c => workItemIds.Contains(c.WorkItemId))
                .Select(c => c.Id)
                .ToListAsync(ct);

            if (checklistIds.Count > 0)
            {
                await _db.Set<ChecklistItem>()
                    .Where(ci => checklistIds.Contains(ci.ChecklistId))
                    .ExecuteDeleteAsync(ct);

                await _db.Checklists
                    .Where(c => checklistIds.Contains(c.Id))
                    .ExecuteDeleteAsync(ct);
            }

            // WorkItemDependencies (both directions)
            await _db.WorkItemDependencies
                .Where(d => workItemIds.Contains(d.WorkItemId) || workItemIds.Contains(d.DependsOnWorkItemId))
                .ExecuteDeleteAsync(ct);

            // WorkItemAttachments, Comments, Labels, Assignments, TimeEntries
            await _db.WorkItemAttachments
                .Where(a => workItemIds.Contains(a.WorkItemId))
                .ExecuteDeleteAsync(ct);

            await _db.WorkItemComments
                .Where(c => workItemIds.Contains(c.WorkItemId))
                .ExecuteDeleteAsync(ct);

            await _db.WorkItemLabels
                .Where(wl => workItemIds.Contains(wl.WorkItemId))
                .ExecuteDeleteAsync(ct);

            await _db.WorkItemAssignments
                .Where(a => workItemIds.Contains(a.WorkItemId))
                .ExecuteDeleteAsync(ct);

            await _db.TimeEntries
                .Where(te => workItemIds.Contains(te.WorkItemId))
                .ExecuteDeleteAsync(ct);

            // Work items themselves
            await _db.WorkItems
                .Where(wi => workItemIds.Contains(wi.Id))
                .ExecuteDeleteAsync(ct);
        }

        // Swimlanes
        if (swimlaneIds.Count > 0)
        {
            await _db.Swimlanes
                .Where(s => swimlaneIds.Contains(s.Id))
                .ExecuteDeleteAsync(ct);
        }

        // ItemTemplates
        await _db.Set<ItemTemplate>()
            .Where(t => t.ProductId == productId)
            .ExecuteDeleteAsync(ct);

        // Activities
        await _db.Activities
            .Where(a => a.ProductId == productId)
            .ExecuteDeleteAsync(ct);

        // Labels
        await _db.Labels
            .Where(l => l.ProductId == productId)
            .ExecuteDeleteAsync(ct);

        // Product Members
        await _db.ProductMembers
            .Where(pm => pm.ProductId == productId)
            .ExecuteDeleteAsync(ct);

        // Product itself
        await _db.Products
            .IgnoreQueryFilters()
            .Where(p => p.Id == productId)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Gets a user's role in a product. Returns null if the user is not a member.
    /// </summary>
    public async Task<ProductMemberRole?> GetUserProductRoleAsync(Guid productId, Guid userId, CancellationToken ct)
    {
        var member = await _db.ProductMembers
            .FirstOrDefaultAsync(pm => pm.ProductId == productId && pm.UserId == userId, ct);

        return member?.Role;
    }

    public async Task<List<ProductDto>> ListProductsByOrganizationAsync(Guid organizationId, CancellationToken ct)
    {
        var products = await _db.Products
            .Where(p => p.OrganizationId == organizationId)
            .ToListAsync(ct);

        var productIds = products.Select(p => p.Id).ToList();

        var swimlaneCounts = await _db.Swimlanes
            .Where(s => productIds.Contains(s.ContainerId)
                     && s.ContainerType == SwimlaneContainerType.Product
                     && !s.IsArchived)
            .GroupBy(s => s.ContainerId)
            .Select(g => new { ContainerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ContainerId, x => x.Count, ct);

        var epicCounts = await _db.WorkItems
            .Where(wi => productIds.Contains(wi.ProductId) && wi.Type == WorkItemType.Epic)
            .GroupBy(wi => wi.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProductId, x => x.Count, ct);

        var memberCounts = await _db.ProductMembers
            .Where(pm => productIds.Contains(pm.ProductId))
            .GroupBy(pm => pm.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProductId, x => x.Count, ct);

        var labelCounts = await _db.Labels
            .Where(l => productIds.Contains(l.ProductId))
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProductId, x => x.Count, ct);

        return products.Select(p => MapToDto(
            p,
            swimlaneCounts.GetValueOrDefault(p.Id, 0),
            epicCounts.GetValueOrDefault(p.Id, 0),
            memberCounts.GetValueOrDefault(p.Id, 0),
            labelCounts.GetValueOrDefault(p.Id, 0)
        )).ToList();
    }

    public async Task<ProductMemberDto> AddMemberAsync(Guid productId, AddProductMemberDto dto, CancellationToken ct)
    {
        _ = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        var existing = await _db.ProductMembers
            .FirstOrDefaultAsync(pm => pm.ProductId == productId && pm.UserId == dto.UserId, ct);

        if (existing is not null)
            throw new InvalidOperationException("User is already a member of this product.");

        var member = new ProductMember
        {
            ProductId = productId,
            UserId = dto.UserId,
            Role = dto.Role
        };

        _db.ProductMembers.Add(member);

        await _db.SaveChangesAsync(ct);

        return new ProductMemberDto
        {
            UserId = member.UserId,
            Role = member.Role,
            JoinedAt = member.JoinedAt
        };
    }

    public async Task RemoveMemberAsync(Guid productId, Guid userId, CancellationToken ct)
    {
        var member = await _db.ProductMembers
            .FirstOrDefaultAsync(pm => pm.ProductId == productId && pm.UserId == userId, ct)
            ?? throw new InvalidOperationException("User is not a member of this product.");

        if (member.Role == ProductMemberRole.Owner)
        {
            var ownerCount = await _db.ProductMembers
                .CountAsync(pm => pm.ProductId == productId && pm.Role == ProductMemberRole.Owner, ct);

            if (ownerCount <= 1)
                throw new InvalidOperationException("Cannot remove the last Owner from the product.");
        }

        _db.ProductMembers.Remove(member);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<ProductMemberDto> UpdateMemberRoleAsync(
        Guid productId, Guid userId, ProductMemberRole role, CancellationToken ct)
    {
        var member = await _db.ProductMembers
            .FirstOrDefaultAsync(pm => pm.ProductId == productId && pm.UserId == userId, ct)
            ?? throw new InvalidOperationException("User is not a member of this product.");

        if (member.Role == ProductMemberRole.Owner && role != ProductMemberRole.Owner)
        {
            var ownerCount = await _db.ProductMembers
                .CountAsync(pm => pm.ProductId == productId && pm.Role == ProductMemberRole.Owner, ct);

            if (ownerCount <= 1)
                throw new InvalidOperationException("Cannot demote the last Owner of the product.");
        }

        member.Role = role;

        await _db.SaveChangesAsync(ct);

        return new ProductMemberDto
        {
            UserId = member.UserId,
            Role = member.Role,
            JoinedAt = member.JoinedAt
        };
    }

    public async Task<LabelDto> CreateLabelAsync(Guid productId, CreateLabelDto dto, CancellationToken ct)
    {
        _ = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        var label = new Label
        {
            ProductId = productId,
            Title = dto.Title,
            Color = dto.Color
        };

        _db.Labels.Add(label);

        await _db.SaveChangesAsync(ct);

        return new LabelDto
        {
            Id = label.Id,
            ProductId = label.ProductId,
            Title = label.Title,
            Color = label.Color,
            CreatedAt = label.CreatedAt
        };
    }

    public async Task DeleteLabelAsync(Guid productId, Guid labelId, CancellationToken ct)
    {
        var label = await _db.Labels
            .FirstOrDefaultAsync(l => l.Id == labelId && l.ProductId == productId, ct)
            ?? throw new InvalidOperationException($"Label {labelId} not found in product {productId}.");

        _db.Labels.Remove(label);

        await _db.SaveChangesAsync(ct);
    }

    private static ProductDto MapToDto(
        Product product, int swimlaneCount, int epicCount, int memberCount, int labelCount)
    {
        return new ProductDto
        {
            Id = product.Id,
            OrganizationId = product.OrganizationId,
            Name = product.Name,
            Description = product.Description,
            Color = product.Color,
            OwnerId = product.OwnerId,
            SubItemsEnabled = product.SubItemsEnabled,
            IsArchived = product.IsArchived,
            SwimlaneCount = swimlaneCount,
            EpicCount = epicCount,
            MemberCount = memberCount,
            LabelCount = labelCount,
            ETag = product.ETag,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }
}

using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Data service for saved custom views.
/// </summary>
public sealed class CustomViewService
{
    private readonly TracksDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomViewService"/> class.
    /// </summary>
    public CustomViewService(TracksDbContext db)
    {
        _db = db;
    }

    /// <summary>Lists custom views for a product that the user owns or are shared.</summary>
    public async Task<IReadOnlyList<CustomView>> GetViewsForProductAsync(Guid productId, Guid userId, CancellationToken ct = default)
    {
        return await _db.CustomViews
            .Where(cv => cv.ProductId == productId && (cv.UserId == userId || cv.IsShared))
            .OrderBy(cv => cv.Name)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <summary>Gets a single custom view by ID.</summary>
    public async Task<CustomView?> GetViewAsync(Guid viewId, CancellationToken ct = default)
    {
        return await _db.CustomViews
            .AsNoTracking()
            .FirstOrDefaultAsync(cv => cv.Id == viewId, ct);
    }

    /// <summary>Creates a new custom view.</summary>
    public async Task<CustomView> CreateViewAsync(Guid productId, Guid userId, string name, string filterJson, string sortJson, string? groupBy, string layout, bool isShared, CancellationToken ct = default)
    {
        var view = new CustomView
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            UserId = userId,
            Name = name,
            FilterJson = filterJson,
            SortJson = sortJson,
            GroupBy = groupBy,
            Layout = layout,
            IsShared = isShared,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.CustomViews.Add(view);
        await _db.SaveChangesAsync(ct);
        return view;
    }

    /// <summary>Updates an existing custom view.</summary>
    public async Task<CustomView?> UpdateViewAsync(Guid viewId, Guid userId, string? name, string? filterJson, string? sortJson, string? groupBy, string? layout, bool? isShared, CancellationToken ct = default)
    {
        var view = await _db.CustomViews.FirstOrDefaultAsync(cv => cv.Id == viewId, ct);
        if (view is null) return null;
        if (view.UserId != userId) throw new InvalidOperationException("Not authorized to update this view.");

        if (name is not null) view.Name = name;
        if (filterJson is not null) view.FilterJson = filterJson;
        if (sortJson is not null) view.SortJson = sortJson;
        if (groupBy is not null) view.GroupBy = groupBy;
        if (layout is not null) view.Layout = layout;
        if (isShared.HasValue) view.IsShared = isShared.Value;
        view.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return view;
    }

    /// <summary>Deletes a custom view.</summary>
    public async Task<bool> DeleteViewAsync(Guid viewId, Guid userId, CancellationToken ct = default)
    {
        var view = await _db.CustomViews.FirstOrDefaultAsync(cv => cv.Id == viewId, ct);
        if (view is null) return false;
        if (view.UserId != userId) throw new InvalidOperationException("Not authorized to delete this view.");

        _db.CustomViews.Remove(view);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

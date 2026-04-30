using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Implements ICommandPaletteService using direct DbContext queries.
/// </summary>
public sealed class CommandPaletteService : ICommandPaletteService
{
    private readonly TracksDbContext _db;
    private readonly ILogger<CommandPaletteService> _logger;

    public CommandPaletteService(TracksDbContext db, ILogger<CommandPaletteService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CommandPaletteResult> SearchAsync(Guid organizationId, string query, Guid? currentProductId, CancellationToken ct)
    {
        var result = new CommandPaletteResult();
        result.Actions = GetMatchingActions(query);

        try
        {
            result.Products = await _db.Products
                .AsNoTracking()
                .Where(p => p.OrganizationId == organizationId && !p.IsDeleted)
                .Where(p => EF.Functions.Like(p.Name, $"%{query}%") || string.IsNullOrEmpty(query))
                .Take(5)
                .Select(p => new PaletteItem
                {
                    Id = p.Id.ToString(),
                    Title = p.Name,
                    Subtitle = "Product",
                    Action = "navigate",
                    ActionUrl = $"/apps/tracks?product={p.Id}"
                })
                .ToListAsync(ct);
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Failed to search products"); }

        if (currentProductId.HasValue)
        {
            try
            {
                if (int.TryParse(query, out var num))
                {
                    var item = await _db.WorkItems
                        .AsNoTracking()
                        .Where(wi => wi.ProductId == currentProductId.Value && wi.ItemNumber == num && !wi.IsDeleted)
                        .Select(wi => new PaletteItem
                        {
                            Id = wi.Id.ToString(), Title = $"#{wi.ItemNumber} {wi.Title}",
                            Subtitle = wi.Type.ToString(), Action = "navigate",
                            ActionUrl = $"/apps/tracks?product={currentProductId}&workItem={wi.Id}"
                        })
                        .FirstOrDefaultAsync(ct);
                    if (item is not null) result.WorkItems = [item];
                }

                if (result.WorkItems.Count == 0)
                {
                    result.WorkItems = await _db.WorkItems
                        .AsNoTracking()
                        .Where(wi => wi.ProductId == currentProductId.Value && !wi.IsDeleted)
                        .Where(wi => EF.Functions.Like(wi.Title, $"%{query}%") || string.IsNullOrEmpty(query))
                        .Take(8)
                        .Select(wi => new PaletteItem
                        {
                            Id = wi.Id.ToString(), Title = $"#{wi.ItemNumber} {wi.Title}",
                            Subtitle = wi.Type.ToString(), Action = "navigate",
                            ActionUrl = $"/apps/tracks?product={currentProductId}&workItem={wi.Id}"
                        })
                        .ToListAsync(ct);
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Failed to search work items"); }
        }

        return result;
    }

    private static List<PaletteItem> GetMatchingActions(string query)
    {
        var all = new List<PaletteItem>
        {
            new() { Id = "new-epic", Title = "New Epic", Subtitle = "Quick Action", Action = "new-epic", ActionUrl = "" },
            new() { Id = "new-item", Title = "New Work Item", Subtitle = "Quick Action", Action = "new-item", ActionUrl = "" },
            new() { Id = "my-items", Title = "Go to My Items", Subtitle = "Quick Action", Action = "my-items", ActionUrl = "" },
            new() { Id = "dashboard", Title = "Go to Dashboard", Subtitle = "Quick Action", Action = "navigate", ActionUrl = "/apps/tracks" },
            new() { Id = "settings", Title = "Open Product Settings", Subtitle = "Quick Action", Action = "settings", ActionUrl = "" },
            new() { Id = "dark-mode", Title = "Toggle Dark Mode", Subtitle = "Quick Action", Action = "toggle-dark-mode", ActionUrl = "" },
            new() { Id = "shortcuts", Title = "Keyboard Shortcuts", Subtitle = "Quick Action", Action = "shortcuts", ActionUrl = "" },
        };
        if (string.IsNullOrWhiteSpace(query)) return all;
        return all.Where(a => a.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}

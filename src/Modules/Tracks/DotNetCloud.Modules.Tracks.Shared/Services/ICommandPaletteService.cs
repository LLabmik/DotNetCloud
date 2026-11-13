namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Aggregates searchable items for the command palette (Ctrl+K).
/// </summary>
public interface ICommandPaletteService
{
    /// <summary>
    /// Searches across all entity types and returns grouped palette results.
    /// </summary>
    Task<CommandPaletteResult> SearchAsync(Guid organizationId, string query, Guid? currentProductId, CancellationToken ct);
}

/// <summary>
/// Results from a command palette search, grouped by entity type.
/// </summary>
public sealed class CommandPaletteResult
{
    public List<PaletteItem> Actions { get; set; } = [];
    public List<PaletteItem> Products { get; set; } = [];
    public List<PaletteItem> WorkItems { get; set; } = [];
    public List<PaletteItem> Sprints { get; set; } = [];
    public List<PaletteItem> Views { get; set; } = [];
}

/// <summary>
/// A single searchable item in the command palette.
/// </summary>
public sealed class PaletteItem
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Subtitle { get; set; }
    public required string Action { get; set; }
    public required string ActionUrl { get; set; }
}
